namespace VideoDownloaderBot.Entities
{
    public class DownloadJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Url { get; set; } = null!;

        public SourcePlatform Platform { get; set; }

        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;

        public string? Title { get; set; }

        public string? FileName { get; set; }

        public string? FilePath { get; set; }

        public long? FileSize { get; set; }

        public int Progress { get; set; }

        public string? Error { get; set; }

        public long? TelegramChatId { get; set; }

        public long? TelegramMessageId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }
    public enum DownloadStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }

    public enum SourcePlatform
    {
        Unknown = 0,
        YouTube = 1,
        Instagram = 2,
        Pinterest = 3,
        TikTok = 4
    }
}
