namespace VideoDownloaderBot.Entities;

public class DownloadHistory
{
    public int Id { get; set; }

    public long TelegramUserId { get; set; }

    public string Url { get; set; } = null!;

    public string? Platform { get; set; }

    public string? FileName { get; set; }

    public long? FileSize { get; set; }

    public string? AudioPath { get; set; }

    public string? AudioFileName { get; set; }

    public long? AudioFileSize { get; set; }

    public string Status { get; set; } = null!;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TelegramUser? User { get; set; }
}