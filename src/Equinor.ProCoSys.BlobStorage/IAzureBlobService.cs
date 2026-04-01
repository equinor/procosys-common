using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;

namespace Equinor.ProCoSys.BlobStorage
{
    public interface IAzureBlobService
    {
        Task<bool> DownloadAsync(string container, string blobPath, Stream destination, CancellationToken cancellationToken = default);
        Task UploadAsync(string container, string blobPath, Stream content, string contentType, bool overWrite = false, CancellationToken cancellationToken = default);

        Task<bool> CopyBlobAsync(string container, string srcBlobPath, string destBlobPath, bool waitForCompletion = false, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string container, string blobPath, CancellationToken cancellationToken = default);
        Task<List<string>> ListAsync(string container, CancellationToken cancellationToken = default);

        Uri GetDownloadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey, string startIPAddress = null, string endIPAddress = null);
        Uri GetUploadSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey);
        Uri GetDeleteSasUri(string container, string blobPath, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey);
        Uri GetListSasUri(string container, DateTimeOffset startsOn, DateTimeOffset expiresOn, UserDelegationKey userDelegationKey);
    }
}
