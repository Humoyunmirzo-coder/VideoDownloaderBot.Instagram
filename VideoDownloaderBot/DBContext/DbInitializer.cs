namespace VideoDownloaderBot.DBContext
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
