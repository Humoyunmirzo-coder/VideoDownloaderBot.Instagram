using Microsoft.EntityFrameworkCore;
using VideoDownloaderBot.DBContext;
using VideoDownloaderBot.Entities;
using VideoDownloaderBot.Interfaces;

namespace VideoDownloaderBot.Repository
{
    public class JobRepository : IJobRepository
    {
        private readonly AppDbContext _db;

        public JobRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DownloadJob?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await _db.DownloadJobs
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<DownloadJob?> GetNextPendingAsync(
            CancellationToken cancellationToken)
        {
            return await _db.DownloadJobs
                .Where(x => x.Status == DownloadStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task AddAsync(
            DownloadJob job,
            CancellationToken cancellationToken)
        {
            await _db.DownloadJobs.AddAsync(
                job,
                cancellationToken);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
