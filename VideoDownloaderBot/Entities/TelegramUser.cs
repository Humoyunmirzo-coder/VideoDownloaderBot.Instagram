namespace VideoDownloaderBot.Entities
{
    public class TelegramUser
    {
        public int Id { get; set; }

        public long TelegramId { get; set; }

        public string? Username { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public ICollection<DownloadHistory> Downloads { get; set; }
            = new List<DownloadHistory>();
    }
}

