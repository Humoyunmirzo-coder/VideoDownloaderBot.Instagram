namespace VideoDownloaderBot.DTOs
{
    public class CreateDownload
    {

        public sealed record CreateDownloadRequest(
            string Url
        );

        public sealed record CreateDownloadResponse(
            Guid JobId,
            string Status,
            string? DownloadUrl
        );

        public sealed record DownloadJobResponse(
            Guid JobId,
            string Url,
            string Platform,
            string Status,
            int Progress,
            string? Title,
            long? FileSize,
            string? DownloadUrl,
            string? Error
            );
    }
}
