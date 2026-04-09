using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace Equinor.ProCoSys.BlobStorage
{
    public class AzureBlobService : IAzureBlobService
    {
        private class ResourceTypes
        {
            public const string BLOB = "b";
            public const string CONTAINER = "c";
        }

        public string AccountDomain { get; private set; }
        public string AccountName { get; private set; }
        public string AccountUrl { get; private set; }
        public TokenCredential Credential { get; private set; }

        public AzureBlobService(IOptionsMonitor<BlobStorageOptions> options, TokenCredential credential)
        {

            if (string.IsNullOrEmpty(options.CurrentValue.AccountName))
            {
                throw new ArgumentNullException(nameof(options.CurrentValue.AccountName));
            }

            AccountName = options.CurrentValue.AccountName;
            AccountDomain = options.CurrentValue.AccountDomain;
            AccountUrl = options.CurrentValue.AccountUrl;
            Credential = credential;
        }

        private BlobClient GetBlobClient(string container, string blobPath)
        {
            var blobUri = new Uri($"{AccountUrl}/{container}/{blobPath}");
            return new BlobClient(blobUri, Credential);
        }

        public async Task<bool> DownloadAsync(string container, string blobPath, Stream destination, CancellationToken cancellationToken = default)
        {
            var client = GetBlobClient(container, blobPath);
            var res = await client.DownloadToAsync(destination, cancellationToken);
            return res.Status > 199 && res.Status < 300;
        }

        public async Task UploadAsync(string container, string blobPath, Stream content, string contentType, bool overWrite = false, CancellationToken cancellationToken = default)
        {
            var client = GetBlobClient(container, blobPath);
            var filename = Path.GetFileName(blobPath);

            // Encode the filename using URL encoding (for non-ASCII characters).
            var encodedFilename = Uri.EscapeDataString(filename);

            BlobUploadOptions options = new()
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentDisposition = contentType.StartsWith("image/") ? $"attachment; filename*=UTF-8''{encodedFilename}" : null
                }
            };

            // Set conditions to ensure overwrite is false, default is true
            if (!overWrite)
            {
                options.Conditions = new BlobRequestConditions
                {
                    IfNoneMatch = new ETag("*")
                };
            }

            await client.UploadAsync(content, options, cancellationToken);
        }

        /// <summary>
        /// Copy a blob from a source path to a new location, destination path.
        /// </summary>
        /// <param name="container">Blob storage container</param>
        /// <param name="srcBlobPath">Path of blob to be copied to a new location</param>
        /// <param name="destBlobPath">Destination path for new blob copy</param>
        /// <param name="waitForCompletion">Wait for the operation to complete before returning</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Indication whether the operation has completed</returns>
        public async Task<bool> CopyBlobAsync(string container, string srcBlobPath, string destBlobPath, bool waitForCompletion = false, CancellationToken cancellationToken = default)
        {
            // Get source blob client
            var srcBlobClient = GetBlobClient(container, srcBlobPath);

            // Get destination blob client
            var destBlobClient = GetBlobClient(container, destBlobPath);

            var operation = await destBlobClient.StartCopyFromUriAsync(srcBlobClient.Uri, null, cancellationToken);
            if (waitForCompletion)
            {
                await operation.WaitForCompletionAsync(cancellationToken);
            }

            return operation.HasCompleted;
        }

        public async Task<bool> DeleteAsync(string container, string blobPath, CancellationToken cancellationToken = default)
        {
            var client = GetBlobClient(container, blobPath);
            var res = await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, null, cancellationToken);
            return res.Value;
        }

        public async Task<List<string>> ListAsync(string container, CancellationToken cancellationToken = default)
        {
            var client = new BlobContainerClient(new Uri($"{AccountUrl}/{container}"), Credential);
            var blobNames = new List<string>();
            await foreach (var blob in client.GetBlobsAsync(BlobTraits.None, BlobStates.None, null, cancellationToken))
            {
                blobNames.Add(blob.Name);
            }
            return blobNames;
        }

        public Uri GetDownloadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey, string startIpAddress = null, string endIpAddress = null)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Read, startsOn, expiresOn, userDelegationKey, startIpAddress, endIpAddress);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = AccountDomain,
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetUploadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Create | BlobAccountSasPermissions.Write, startsOn, expiresOn, userDelegationKey);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = AccountDomain,
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetDeleteSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Delete, startsOn, expiresOn, userDelegationKey);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = AccountDomain,
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetListSasUri(string container, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey)
        {
            var sasToken = GetSasToken(container, string.Empty, ResourceTypes.CONTAINER, BlobAccountSasPermissions.List, startsOn, expiresOn, userDelegationKey);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = AccountDomain,
                Path = container,
                Query = $"restype=container&comp=list&{sasToken}"
            };
            return fullUri.Uri;
        }

        private string GetSasToken(
            string containerName,
            string blobName,
            string resourceType,
            BlobAccountSasPermissions permissions,
            DateTimeOffset startsOn,
            DateTimeOffset expiresOn,
            UserDelegationKey userDelegationKey,
            string startIpAddress = null,
            string endIpAddress = null)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = resourceType,
                StartsOn = startsOn,
                ExpiresOn = expiresOn
            };

            // Set the IP range for the SAS token
            if (!string.IsNullOrEmpty(startIpAddress))
            {
                if (string.IsNullOrEmpty(endIpAddress))
                {
                    endIpAddress = startIpAddress;
                }
                sasBuilder.IPRange = new SasIPRange(System.Net.IPAddress.Parse(startIpAddress), System.Net.IPAddress.Parse(endIpAddress));
            }
            sasBuilder.SetPermissions(permissions);

            return sasBuilder.ToSasQueryParameters(userDelegationKey, AccountName).ToString();
        }
    }
}
