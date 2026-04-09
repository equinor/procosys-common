namespace Equinor.ProCoSys.BlobStorage
{
    public class BlobStorageOptions
    {
        private readonly string _blobStorageUrlSuffix = ".blob.core.windows.net";
        public string AccountName { get; set; }
        public string AccountDomain => AccountName + _blobStorageUrlSuffix;
        public string AccountUrl => "https://" + AccountDomain;
        public int MaxSizeMb { get; set; }
        public string BlobContainer { get; set; }
        public int BlobClockSkewMinutes { get; set; }
        public string[] BlockedFileSuffixes { get; set; }
    }
}
