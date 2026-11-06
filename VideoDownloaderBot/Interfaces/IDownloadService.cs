using VideoDownloaderBot.Entities;

namespace VideoDownloaderBot.Interfaces
{
    public interface IDownloadService
    {
        Task ProcessAsync(DownloadJob job,CancellationToken cancellationToken);
    }
}
