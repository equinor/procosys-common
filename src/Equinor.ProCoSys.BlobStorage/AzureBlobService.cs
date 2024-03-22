using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage;
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
        public string ConnectionString { get; private set; }
        public string Endpoint { get; private set; }
        public string AccountName { get; private set; }
        public string AccountKey { get; private set; }

        public AzureBlobService(IOptionsMonitor<BlobStorageOptions> options)
        {
            if (string.IsNullOrEmpty(options.CurrentValue.ConnectionString))
            {
                throw new ArgumentNullException(nameof(options.CurrentValue.ConnectionString));
            }

            ConnectionString = options.CurrentValue.ConnectionString;
            AccountName = Regex.Match(ConnectionString, @"AccountName=(.+?)(;|\z)", RegexOptions.Singleline).Groups[1].Value;
            AccountKey = Regex.Match(ConnectionString, @"AccountKey=(.+?)(;|\z)", RegexOptions.Singleline).Groups[1].Value;
            Endpoint = "blob." + Regex.Match(ConnectionString, @"EndpointSuffix=(.+?)(;|\z)", RegexOptions.Singleline).Groups[1].Value;
        }

        public async Task<bool> DownloadAsync(string container, string blobPath, Stream destination, CancellationToken cancellationToken = default)
        {
            var client = new BlobClient(ConnectionString, container, blobPath);
            var res = await client.DownloadToAsync(destination, cancellationToken);
            return res.Status > 199 && res.Status < 300;
        }

        public async Task UploadAsync(string container, string blobPath, Stream content, string contentType, bool overWrite = false, CancellationToken cancellationToken = default)
        {
            var client = new BlobClient(ConnectionString, container, blobPath);
            var filename = Path.GetFileName(blobPath);

            BlobUploadOptions options = new()
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    ContentDisposition = contentType.StartsWith("image/") ? $"attachment; filename={filename}" : null
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

        public async Task<bool> DeleteAsync(string container, string blobPath, CancellationToken cancellationToken = default)
        {
            var client = new BlobClient(ConnectionString, container, blobPath);
            var res = await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, null, cancellationToken);
            return res.Value;
        }

        public async Task<List<string>> ListAsync(string container, CancellationToken cancellationToken = default)
        {
            var client = new BlobContainerClient(ConnectionString, container);
            var blobNames = new List<string>();
            await foreach (var blob in client.GetBlobsAsync(BlobTraits.None, BlobStates.None, null, cancellationToken))
            {
                blobNames.Add(blob.Name);
            }
            return blobNames;
        }

        public Uri GetDownloadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn,string startIpAddress = null, string endIpAddress = null)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Read, startsOn, expiresOn, startIpAddress, endIpAddress);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = string.Format($"{AccountName}.{Endpoint}"),
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetUploadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Create | BlobAccountSasPermissions.Write, startsOn, expiresOn);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = string.Format($"{AccountName}.{Endpoint}"),
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetDeleteSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn)
        {
            var sasToken = GetSasToken(container, blobPath, ResourceTypes.BLOB, BlobAccountSasPermissions.Delete, startsOn, expiresOn);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = string.Format($"{AccountName}.{Endpoint}"),
                Path = Path.Combine(container, blobPath),
                Query = sasToken
            };
            return fullUri.Uri;
        }

        public Uri GetListSasUri(string container, DateTimeOffset startsOn, DateTimeOffset expiresOn)
        {
            var sasToken = GetSasToken(container, string.Empty, ResourceTypes.CONTAINER, BlobAccountSasPermissions.List, startsOn, expiresOn);
            var fullUri = new UriBuilder
            {
                Scheme = "https",
                Host = string.Format($"{AccountName}.{Endpoint}"),
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
                if (string.IsNullOrEmpty(endIpAddress)) endIpAddress = startIpAddress;
                sasBuilder.IPRange = new SasIPRange(System.Net.IPAddress.Parse(startIpAddress), System.Net.IPAddress.Parse(endIpAddress));
            }
            sasBuilder.SetPermissions(permissions);
            return sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(AccountName, AccountKey)).ToString();
        }
    }
}
