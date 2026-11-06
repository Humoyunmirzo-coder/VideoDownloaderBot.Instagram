namespace VideoDownloaderBot.Entities
{
    public class DownloadResult
    {
        public bool Success { get; set; }

        public string? FilePath { get; set; }

        public string? FileName { get; set; }

        public long FileSize { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
