using System.Diagnostics;

namespace VideoDownloaderBot.Services;

public class VideoDownloadService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoDownloadService> _logger;

    public VideoDownloadService(
        IConfiguration configuration,
        ILogger<VideoDownloadService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DownloadResult> DownloadAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var downloadPath =
            _configuration["BotSettings:DownloadPath"]
            ?? "downloads";

        var fullDownloadPath =
            Path.GetFullPath(downloadPath);

        Directory.CreateDirectory(fullDownloadPath);

        var fileId = Guid.NewGuid().ToString("N");

        var videoPath =
            Path.Combine(
                fullDownloadPath,
                $"{fileId}_video.mp4");

        var audioPath =
            Path.Combine(
                fullDownloadPath,
                $"{fileId}_audio.mp3");

        var ytDlpPath =
            OperatingSystem.IsWindows()
                ? Path.Combine(
                    AppContext.BaseDirectory,
                    "yt-dlp.exe")
                : "yt-dlp";

        _logger.LogInformation(
            "yt-dlp path: {Path}",
            ytDlpPath);

        if (OperatingSystem.IsWindows() &&
            !File.Exists(ytDlpPath))
        {
            return new DownloadResult
            {
                Success = false,

                ErrorMessage =
                    $"yt-dlp.exe topilmadi.\n\n" +
                    $"Kerakli path:\n{ytDlpPath}"
            };
        }

        try
        {
            // =====================================================
            // 1. VIDEO DOWNLOAD
            // =====================================================

            var videoArguments =
                $"-f \"bv*+ba/b\" " +
                $"--merge-output-format mp4 " +
                $"--no-playlist " +
                $"-o \"{videoPath}\" " +
                $"\"{url}\"";

            _logger.LogInformation(
                "Downloading video: {Url}",
                url);

            var videoResult =
                await RunYtDlpAsync(
                    ytDlpPath,
                    videoArguments,
                    cancellationToken);

            if (!videoResult.Success)
            {
                CleanupFile(videoPath);
                CleanupFile(audioPath);

                return new DownloadResult
                {
                    Success = false,

                    ErrorMessage =
                        string.IsNullOrWhiteSpace(
                            videoResult.Error)
                            ? "Video yuklab bo'lmadi."
                            : videoResult.Error
                };
            }

            // yt-dlp ba'zida output filename'ni o'zgartirishi mumkin.
            // Shu sababli kerakli mp4 ni qidiramiz.
            var realVideoPath =
                FindVideoFile(
                    fullDownloadPath,
                    fileId);

            if (realVideoPath == null)
            {
                CleanupFile(videoPath);
                CleanupFile(audioPath);

                return new DownloadResult
                {
                    Success = false,

                    ErrorMessage =
                        "Video fayli topilmadi."
                };
            }

            // =====================================================
            // 2. AUDIO DOWNLOAD
            // =====================================================

            var audioArguments =
                $"-x " +
                $"--audio-format mp3 " +
                $"--audio-quality 0 " +
                $"--no-playlist " +
                $"-o \"{audioPath}\" " +
                $"\"{url}\"";

            _logger.LogInformation(
                "Downloading audio: {Url}",
                url);

            var audioResult =
                await RunYtDlpAsync(
                    ytDlpPath,
                    audioArguments,
                    cancellationToken);

            if (!audioResult.Success)
            {
                _logger.LogWarning(
                    "Audio download failed: {Error}",
                    audioResult.Error);

                // Video bor, audio yo'q.
                var videoInfo =
                    new FileInfo(realVideoPath);

                return new DownloadResult
                {
                    Success = true,

                    VideoPath =
                        realVideoPath,

                    VideoFileName =
                        videoInfo.Name,

                    VideoFileSize =
                        videoInfo.Length,

                    AudioPath = null,

                    AudioFileName = null,

                    AudioFileSize = 0,

                    ErrorMessage =
                        "Video yuklandi, lekin musiqa yuklanmadi."
                };
            }

            // =====================================================
            // 3. AUDIO FILE TOPISH
            // =====================================================

            var ffmpegPath = Path.Combine(AppContext.BaseDirectory,"ffmpeg.exe");

            var realAudioPath =FindAudioFile(fullDownloadPath,fileId);

            if (realAudioPath == null)
            {
                _logger.LogWarning(
                    "Audio file not found after download.");

                var videoInfo =
                    new FileInfo(realVideoPath);

                return new DownloadResult
                {
                    Success = true,

                    VideoPath =
                        realVideoPath,

                    VideoFileName =
                        videoInfo.Name,

                    VideoFileSize =
                        videoInfo.Length,

                    AudioPath = null,

                    AudioFileName = null,

                    AudioFileSize = 0,

                    ErrorMessage =
                        "Video yuklandi, lekin audio fayli topilmadi."
                };
            }

            // =====================================================
            // 4. FILE INFO
            // =====================================================

            var videoFileInfo =
                new FileInfo(realVideoPath);

            var audioFileInfo =
                new FileInfo(realAudioPath);

            _logger.LogInformation(
                "Video downloaded: {VideoPath}",
                realVideoPath);

            _logger.LogInformation(
                "Audio downloaded: {AudioPath}",
                realAudioPath);

            return new DownloadResult
            {
                Success = true,

                VideoPath =
                    realVideoPath,

                VideoFileName =
                    videoFileInfo.Name,

                VideoFileSize =
                    videoFileInfo.Length,

                AudioPath =
                    realAudioPath,

                AudioFileName =
                    audioFileInfo.Name,

                AudioFileSize =
                    audioFileInfo.Length
            };
        }
        catch (OperationCanceledException)
        {
            CleanupFiles(
                fullDownloadPath,
                fileId);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Video/audio download error.");

            CleanupFiles(
                fullDownloadPath,
                fileId);

            return new DownloadResult
            {
                Success = false,

                ErrorMessage =
                    ex.Message
            };
        }
    }

    // =========================================================
    // RUN YT-DLP
    // =========================================================

    private async Task<ProcessResult> RunYtDlpAsync(
        string ytDlpPath,
        string arguments,
        CancellationToken cancellationToken)
    {
        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            ytDlpPath,

                        Arguments =
                            arguments,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        UseShellExecute =
                            false,

                        CreateNoWindow =
                            true
                    }
            };

        try
        {
            process.Start();

            var stdoutTask =
                process.StandardOutput
                    .ReadToEndAsync(
                        cancellationToken);

            var stderrTask =
                process.StandardError
                    .ReadToEndAsync(
                        cancellationToken);

            await process.WaitForExitAsync(
                cancellationToken);

            var stdout =
                await stdoutTask;

            var stderr =
                await stderrTask;

            _logger.LogInformation(
                "yt-dlp stdout:\n{Output}",
                stdout);

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogInformation(
                    "yt-dlp stderr:\n{Error}",
                    stderr);
            }

            return new ProcessResult
            {
                Success =
                    process.ExitCode == 0,

                Output =
                    stdout,

                Error =
                    stderr,

                ExitCode =
                    process.ExitCode
            };
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(
                        entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore
            }

            throw;
        }
        finally
        {
            process.Dispose();
        }
    }

    // =========================================================
    // FIND VIDEO
    // =========================================================

    private string? FindVideoFile(
        string directory,
        string fileId)
    {
        var exactPath =
            Path.Combine(
                directory,
                $"{fileId}_video.mp4");

        if (File.Exists(exactPath))
        {
            return exactPath;
        }

        var files =
            Directory.GetFiles(
                directory,
                $"{fileId}*.mp4");

        return files.FirstOrDefault();
    }

    // =========================================================
    // FIND AUDIO
    // =========================================================

    private string? FindAudioFile(
        string directory,
        string fileId)
    {
        var exactPath =
            Path.Combine(
                directory,
                $"{fileId}_audio.mp3");

        if (File.Exists(exactPath))
        {
            return exactPath;
        }

        var mp3Files =
            Directory.GetFiles(
                directory,
                $"{fileId}*.mp3");

        return mp3Files.FirstOrDefault();
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void CleanupFiles(
        string directory,
        string fileId)
    {
        try
        {
            var files =
                Directory.GetFiles(
                    directory,
                    $"{fileId}*");

            foreach (var file in files)
            {
                CleanupFile(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cleanup failed.");
        }
    }

    private void CleanupFile(
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not delete file: {File}",
                filePath);
        }
    }

    // =========================================================
    // INTERNAL PROCESS RESULT
    // =========================================================

    private class ProcessResult
    {
        public bool Success { get; set; }

        public string? Output { get; set; }

        public string? Error { get; set; }

        public int ExitCode { get; set; }
    }
}

// =============================================================
// DOWNLOAD RESULT
// =============================================================

public class DownloadResult
{
    public bool Success { get; set; }

    public string? VideoPath { get; set; }

    public string? VideoFileName { get; set; }

    public long VideoFileSize { get; set; }

    public string? AudioPath { get; set; }

    public string? AudioFileName { get; set; }

    public long AudioFileSize { get; set; }

    public string? ErrorMessage { get; set; }
}