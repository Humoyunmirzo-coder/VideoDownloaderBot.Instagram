using VideoDownloaderBot.Entities;

namespace VideoDownloaderBot.Interfaces
{
    public interface IJobRepository
    {
        Task<DownloadJob?> GetByIdAsync(Guid id,CancellationToken cancellationToken);

        Task<DownloadJob?> GetNextPendingAsync(CancellationToken cancellationToken);

        Task AddAsync(DownloadJob job,CancellationToken cancellationToken);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
