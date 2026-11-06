using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VideoDownloaderBot.Entities;
using VideoDownloaderBot.Interfaces;

namespace VideoDownloaderBot.Services
{

    public class YtDlpDownloadService : IDownloadService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<YtDlpDownloadService> _logger;
        private readonly IJobRepository _repository;

        private readonly string _ytDlpPath;
        private readonly string _outputDirectory;
        private readonly long _maxFileSize;

        public YtDlpDownloadService(
            IConfiguration configuration,
            ILogger<YtDlpDownloadService> logger,
            IJobRepository repository)
        {
            _configuration = configuration;
            _logger = logger;
            _repository = repository;

            _ytDlpPath =
                configuration["Downloader:YtDlpPath"]
                ?? "yt-dlp";

            _outputDirectory =
                configuration["Downloader:OutputDirectory"]
                ?? "videos";

            _maxFileSize =
                long.TryParse(
                    configuration["Downloader:MaxFileSizeBytes"],
                    out var size)
                    ? size
                    : 48 * 1024 * 1024;
        }

        public async Task ProcessAsync(
            DownloadJob job,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(
                _outputDirectory);

            job.Status =
                DownloadStatus.Processing;

            job.StartedAtUtc =
                DateTime.UtcNow;

            job.Progress = 0;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var filePrefix =
                job.Id.ToString("N");

            var outputTemplate =
                Path.Combine(
                    _outputDirectory,
                    filePrefix + ".%(ext)s");

            try
            {
                var arguments =
                    $"--no-playlist " +
                    $"--max-filesize {_maxFileSize} " +
                    $"--merge-output-format mp4 " +
                    $"--newline " +
                    $"--print-json " +
                    $"-o \"{outputTemplate}\" " +
                    $"\"{job.Url}\"";

                var processStartInfo =
                    new ProcessStartInfo
                    {
                        FileName = _ytDlpPath,

                        Arguments = arguments,

                        RedirectStandardOutput = true,

                        RedirectStandardError = true,

                        UseShellExecute = false,

                        CreateNoWindow = true
                    };

                using var process =
                    new Process
                    {
                        StartInfo =
                            processStartInfo
                    };

                process.Start();

                var outputLines =
                    new List<string>();

                var errorLines =
                    new List<string>();

                var outputTask =
                    Task.Run(async () =>
                    {
                        while (
                            await process
                                .StandardOutput
                                .ReadLineAsync(
                                    cancellationToken)
                            is { } line)
                        {
                            outputLines.Add(line);

                            ParseProgress(
                                line,
                                job);
                        }
                    }, cancellationToken);

                var errorTask =
                    Task.Run(async () =>
                    {
                        while (
                            await process
                                .StandardError
                                .ReadLineAsync(
                                    cancellationToken)
                            is { } line)
                        {
                            errorLines.Add(line);
                        }
                    }, cancellationToken);

                await Task.WhenAll(
                    outputTask,
                    errorTask,
                    process.WaitForExitAsync(
                        cancellationToken));

                if (process.ExitCode != 0)
                {
                    var error =
                        string.Join(
                            Environment.NewLine,
                            errorLines.TakeLast(20));

                    throw new Exception(
                        string.IsNullOrWhiteSpace(error)
                            ? "yt-dlp failed."
                            : error);
                }

                ParseMetadata(
                    outputLines,
                    job);

                var downloadedFile =
                    Directory
                        .GetFiles(
                            _outputDirectory,
                            filePrefix + ".*")
                        .FirstOrDefault();

                if (downloadedFile == null)
                {
                    throw new FileNotFoundException(
                        "Downloaded file not found.");
                }

                var fileInfo =
                    new FileInfo(
                        downloadedFile);

                if (fileInfo.Length >
                    _maxFileSize)
                {
                    File.Delete(
                        downloadedFile);

                    throw new Exception(
                        "File is too large.");
                }

                job.FilePath =
                    downloadedFile;

                job.FileName =
                    Path.GetFileName(
                        downloadedFile);

                job.FileSize =
                    fileInfo.Length;

                job.Progress = 100;

                job.Status =
                    DownloadStatus.Completed;

                job.CompletedAtUtc =
                    DateTime.UtcNow;

                await _repository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Download failed. JobId: {JobId}",
                    job.Id);

                job.Status =
                    DownloadStatus.Failed;

                job.Error =
                    ex.Message.Length > 3900
                        ? ex.Message[..3900]
                        : ex.Message;

                await _repository.SaveChangesAsync(
                    CancellationToken.None);
            }
        }

        private static void ParseProgress(
            string line,
            DownloadJob job)
        {
            if (!line.Contains(
                    "[download]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var percentIndex =
                line.IndexOf('%');

            if (percentIndex <= 0)
            {
                return;
            }

            var start =
                line.LastIndexOf(
                    ' ',
                    percentIndex - 1);

            if (start < 0)
            {
                return;
            }

            var percentText =
                line[
                    start..percentIndex
                ].Trim();

            if (double.TryParse(
                    percentText,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var progress))
            {
                job.Progress =
                    Math.Clamp(
                        (int)progress,
                        0,
                        99);
            }
        }

        private static void ParseMetadata(
            List<string> lines,
            DownloadJob job)
        {
            var jsonLine =
                lines.LastOrDefault(
                    x => x.TrimStart()
                        .StartsWith("{"));

            if (string.IsNullOrWhiteSpace(
                    jsonLine))
            {
                return;
            }

            try
            {
                using var document =
                    JsonDocument.Parse(
                        jsonLine);

                if (document.RootElement
                    .TryGetProperty(
                        "title",
                        out var title))
                {
                    job.Title =
                        title.GetString();
                }
            }
            catch
            {
                // Metadata is optional.
            }
        }
    }
}
