using Microsoft.EntityFrameworkCore;
using VideoDownloaderBot.Entities;

namespace VideoDownloaderBot.DBContext
{
    public class AppDbContext : DbContext
    {
        public DbSet<TelegramUser> TelegramUsers => Set<TelegramUser>();

        public DbSet<DownloadHistory> DownloadHistories => Set<DownloadHistory>();

        public DbSet<DownloadJob> DownloadJobs { get; set; } = null!;
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TelegramUser>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.TelegramId)
                    .IsUnique();

                entity.Property(x => x.Username)
                    .HasMaxLength(255);

                entity.Property(x => x.FirstName)
                    .HasMaxLength(255);

                entity.Property(x => x.LastName)
                    .HasMaxLength(255);
            });

            modelBuilder.Entity<DownloadHistory>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.TelegramUserId);

                entity.Property(x => x.Url)
                    .IsRequired();

                entity.Property(x => x.Status)
                    .HasMaxLength(50);

                entity.Property(x => x.Platform)
                    .HasMaxLength(100);

                entity.HasOne(x => x.User)
                    .WithMany(x => x.Downloads)
                    .HasForeignKey(x => x.TelegramUserId)
                    .HasPrincipalKey(x => x.TelegramId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
