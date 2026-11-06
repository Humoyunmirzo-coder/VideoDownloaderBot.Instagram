using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using VideoDownloaderBot.DBContext;
using VideoDownloaderBot.Interfaces;
using VideoDownloaderBot.Repository;
using VideoDownloaderBot.Services;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(
        connectionString))
{
    throw new Exception(
        "Database connection string not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>options.UseNpgsql(connectionString));

//builder.Services.AddScoped<IJobRepository,JobRepository>();
//builder.Services.AddSingleton<IUrlDetector,UrlDetector>();
builder.Services.AddSingleton<VideoDownloadService>();
builder.Services.AddHostedService<TelegramBotService>();


var app =
    builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

var outputDirectory =
    builder.Configuration[
        "Downloader:OutputDirectory"]
    ?? "videos";

Directory.CreateDirectory(outputDirectory);

app.UseStaticFiles(
    new StaticFileOptions
    {
        FileProvider =new PhysicalFileProvider(Path.GetFullPath(outputDirectory)),
        RequestPath = "/files"
    });

app.UseHttpsRedirection();

app.MapControllers();

app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "ok",
        time = DateTime.UtcNow
    }));

using (var scope =
       app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await db.Database
        .EnsureCreatedAsync();
}

app.Run();