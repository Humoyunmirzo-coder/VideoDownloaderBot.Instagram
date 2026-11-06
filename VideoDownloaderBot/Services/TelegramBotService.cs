using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VideoDownloaderBot.DBContext;
using VideoDownloaderBot.Entities;

namespace VideoDownloaderBot.Services;

public class TelegramBotService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly VideoDownloadService _videoDownloadService;

    private TelegramBotClient? _bot;

    public TelegramBotService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotService> logger,
        VideoDownloadService videoDownloadService)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _videoDownloadService = videoDownloadService;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var token =
            _configuration["BotSettings:Token"];

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogError(
                "Telegram bot token topilmadi. appsettings.json ni tekshiring.");

            return;
        }

        try
        {
            _bot =
                new TelegramBotClient(token);

            var me =
                await _bot.GetMe(
                    cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Telegram bot ishga tushdi: @{Username} | Id: {Id}",
                me.Username,
                me.Id);

            var receiverOptions =
                new ReceiverOptions
                {
                    AllowedUpdates =
                        new[]
                        {
                            UpdateType.Message,
                            UpdateType.CallbackQuery
                        }
                };

            _bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Telegram update kutilyapti...");

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Telegram bot to'xtatildi.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram botni ishga tushirishda xatolik.");
        }
    }

    // =========================================================
    // UPDATE HANDLER
    // =========================================================

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            // =====================================================
            // CALLBACK BUTTON
            // =====================================================

            if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(
                    bot,
                    update.CallbackQuery!,
                    cancellationToken);

                return;
            }

            // =====================================================
            // MESSAGE
            // =====================================================

            if (update.Type != UpdateType.Message)
                return;

            var message =
                update.Message;

            if (message == null)
                return;

            if (message.From == null)
                return;

            if (string.IsNullOrWhiteSpace(
                    message.Text))
            {
                return;
            }

            var telegramUser =
                message.From;

            var text =
                message.Text.Trim();

            _logger.LogInformation(
                "Yangi message: {TelegramId} | @{Username} | {Text}",
                telegramUser.Id,
                telegramUser.Username,
                text);

            await SaveUserAsync(
                telegramUser,
                cancellationToken);

            // =====================================================
            // /START
            // =====================================================

            if (text.Equals(
                    "/start",
                    StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text:
                        "Assalomu alaykum! 👋\n\n" +
                        "Video Downloader Botga xush kelibsiz.\n\n" +
                        "Instagram, YouTube, TikTok yoki Facebook video linkini yuboring.",
                    cancellationToken: cancellationToken);

                return;
            }

            // =====================================================
            // /HELP
            // =====================================================

            if (text.Equals(
                    "/help",
                    StringComparison.OrdinalIgnoreCase))
            {
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text:
                        "📖 Yordam\n\n" +
                        "Video linkini yuboring.\n\n" +
                        "Qo'llab-quvvatlanadi:\n" +
                        "• Instagram\n" +
                        "• YouTube\n" +
                        "• TikTok\n" +
                        "• Facebook",
                    cancellationToken: cancellationToken);

                return;
            }

            // =====================================================
            // PLATFORM
            // =====================================================

            var platform =
                DetectPlatform(text);

            if (platform == null)
            {
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text:
                        "❌ Bu linkni taniy olmadim.\n\n" +
                        "Instagram, YouTube, TikTok yoki Facebook link yuboring.",
                    cancellationToken: cancellationToken);

                return;
            }

            // =====================================================
            // DB HISTORY
            // =====================================================

            var historyId =
                await CreateDownloadHistoryAsync(
                    telegramUser.Id,
                    text,
                    platform,
                    cancellationToken);

            await bot.SendMessage(
                chatId: message.Chat.Id,
                text:
                    $"⏳ {platform} videosi yuklanmoqda...\n\n" +
                    "Biroz kuting.",
                cancellationToken: cancellationToken);

            try
            {
                // =================================================
                // DOWNLOAD VIDEO + AUDIO
                // =================================================

                var result =
                    await _videoDownloadService.DownloadAsync(
                        text,
                        cancellationToken);

                if (!result.Success ||
                    string.IsNullOrWhiteSpace(
                        result.VideoPath))
                {
                    await UpdateDownloadHistoryAsync(
                        historyId,
                        "Failed",
                        null,
                        null,
                        result.ErrorMessage,
                        cancellationToken);

                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text:
                            "❌ Videoni yuklab bo'lmadi.\n\n" +
                            (result.ErrorMessage ??
                             "Noma'lum xatolik."),
                        cancellationToken: cancellationToken);

                    return;
                }

                // =================================================
                // VIDEO CHECK
                // =================================================

                if (!File.Exists(
                        result.VideoPath))
                {
                    await UpdateDownloadHistoryAsync(
                        historyId,
                        "Failed",
                        null,
                        null,
                        "Video file not found.",
                        cancellationToken);

                    await bot.SendMessage(
                        chatId: message.Chat.Id,
                        text:
                            "❌ Video fayli topilmadi.",
                        cancellationToken: cancellationToken);

                    return;
                }

                // =================================================
                // VIDEO
                // =================================================

                await using var videoStream =
                    new System.IO.FileStream(
                        result.VideoPath,
                        System.IO.FileMode.Open,
                        System.IO.FileAccess.Read,
                        System.IO.FileShare.Read);

                var videoFile =
                    new InputFileStream(
                        videoStream,
                        result.VideoFileName ??
                        "video.mp4");

                // =================================================
                // BUTTONS
                // =================================================

                InlineKeyboardMarkup? keyboard = null;

                if (!string.IsNullOrWhiteSpace(
                        result.AudioPath) &&
                    File.Exists(
                        result.AudioPath))
                {
                    keyboard =
                        new InlineKeyboardMarkup(
                            new[]
                            {
                                new[]
                                {
                                    InlineKeyboardButton
                                        .WithCallbackData(
                                            "🎵 Musiqa",
                                            $"music:{historyId}"),

                                    InlineKeyboardButton
                                        .WithCallbackData(
                                            "🔎 Qidirish",
                                            $"search:{historyId}")
                                }
                            });
                }

                await bot.SendVideo(
                    chatId: message.Chat.Id,
                    video: videoFile,
                    caption:
                        $"✅ Tayyor!\n\n" +
                        $"Platforma: {platform}",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                // =================================================
                // DB COMPLETED
                // =================================================

                await UpdateDownloadHistoryAsync(
                    historyId,
                    "Completed",
                    result.VideoFileName,
                    result.VideoFileSize,
                    null,
                    cancellationToken);

                _logger.LogInformation(
                    "Video yuborildi: {TelegramId} | {FileName}",
                    telegramUser.Id,
                    result.VideoFileName);

                // =================================================
                // MUHIM:
                // AUDIO HOZIRCHA SERVERDA QOLADI
                // CALLBACK BOSILGANDA YUBORAMIZ
                // =================================================

                // Video faylni o'chiramiz
                try
                {
                    File.Delete(
                        result.VideoPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Video faylini o'chirib bo'lmadi.");
                }

                // Audio'ni hozir o'chirmaymiz!
                // Chunki 🎵 Musiqa bosilganda kerak bo'ladi.
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Video download/send error.");

                await UpdateDownloadHistoryAsync(
                    historyId,
                    "Failed",
                    null,
                    null,
                    ex.Message,
                    cancellationToken);

                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text:
                        "❌ Video yuklashda xatolik yuz berdi.",
                    cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telegram update ishlashida xatolik.");
        }
    }

    // =========================================================
    // CALLBACK
    // =========================================================

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient bot,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message == null)
            return;

        var data =
            callbackQuery.Data;

        if (string.IsNullOrWhiteSpace(data))
            return;

        await bot.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        // =====================================================
        // MUSIC
        // =====================================================

        if (data.StartsWith(
                "music:",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(
                    data["music:".Length..],
                    out var historyId))
            {
                return;
            }

            await SendMusicAsync(
                bot,
                callbackQuery.Message.Chat.Id,
                historyId,
                cancellationToken);

            return;
        }

        // =====================================================
        // SEARCH
        // =====================================================

        if (data.StartsWith(
                "search:",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(
                    data["search:".Length..],
                    out var historyId))
            {
                return;
            }

            await bot.SendMessage(
                chatId:
                    callbackQuery.Message.Chat.Id,
                text:
                    "🔎 Musiqani qidirish funksiyasi keyingi bosqichda ulanadi.\n\n" +
                    "Hozircha 🎵 Musiqa tugmasi orqali audioni olishingiz mumkin.",
                cancellationToken:
                    cancellationToken);

            return;
        }
    }

    // =========================================================
    // SEND MUSIC
    // =========================================================

    private async Task SendMusicAsync(
        ITelegramBotClient bot,
        long chatId,
        int historyId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Bu yerda hozircha audio pathni
            // history'dan topish uchun alohida saqlash kerak.
            //
            // DownloadHistory entity'da AudioPath bo'lmasa,
            // vaqtinchalik papkadan history ID orqali topish
            // mumkin emas.
            //
            // Shu sababli hozir audio pathni DBga qo'shishni
            // tavsiya qilaman.

            await bot.SendMessage(
                chatId,
                "🎵 Musiqa tayyorlanmoqda...",
                cancellationToken:
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Music send error.");

            await bot.SendMessage(
                chatId,
                "❌ Musiqani yuborishda xatolik.",
                cancellationToken:
                    cancellationToken);
        }
    }

    // =========================================================
    // SAVE USER
    // =========================================================

    private async Task SaveUserAsync(
        Telegram.Bot.Types.User telegramUser,
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var user =
            await db.TelegramUsers
                .FirstOrDefaultAsync(
                    x =>
                        x.TelegramId ==
                        telegramUser.Id,
                    cancellationToken);

        if (user == null)
        {
            user =
                new TelegramUser
                {
                    TelegramId =
                        telegramUser.Id,

                    Username =
                        telegramUser.Username,

                    FirstName =
                        telegramUser.FirstName,

                    LastName =
                        telegramUser.LastName,

                    CreatedAt =
                        DateTime.UtcNow,

                    LastSeenAt =
                        DateTime.UtcNow
                };

            db.TelegramUsers.Add(user);
        }
        else
        {
            user.Username =
                telegramUser.Username;

            user.FirstName =
                telegramUser.FirstName;

            user.LastName =
                telegramUser.LastName;

            user.LastSeenAt =
                DateTime.UtcNow;
        }

        await db.SaveChangesAsync(
            cancellationToken);
    }

    // =========================================================
    // CREATE HISTORY
    // =========================================================

    private async Task<int> CreateDownloadHistoryAsync(
        long telegramUserId,
        string url,
        string platform,
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var history =
            new DownloadHistory
            {
                TelegramUserId =
                    telegramUserId,

                Url =
                    url,

                Platform =
                    platform,

                Status =
                    "Processing",

                CreatedAt =
                    DateTime.UtcNow
            };

        db.DownloadHistories.Add(
            history);

        await db.SaveChangesAsync(
            cancellationToken);

        return history.Id;
    }

    // =========================================================
    // UPDATE HISTORY
    // =========================================================

    private async Task UpdateDownloadHistoryAsync(
        int historyId,
        string status,
        string? fileName,
        long? fileSize,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var history =
            await db.DownloadHistories
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                        historyId,
                    cancellationToken);

        if (history == null)
            return;

        history.Status =
            status;

        history.FileName =
            fileName;

        history.FileSize =
            fileSize;

        history.ErrorMessage =
            errorMessage;

        history.CompletedAt =
            DateTime.UtcNow;

        await db.SaveChangesAsync(
            cancellationToken);
    }

    // =========================================================
    // PLATFORM
    // =========================================================

    private string? DetectPlatform(
        string url)
    {
        if (url.Contains(
                "youtube.com",
                StringComparison.OrdinalIgnoreCase) ||
            url.Contains(
                "youtu.be",
                StringComparison.OrdinalIgnoreCase))
        {
            return "YouTube";
        }

        if (url.Contains(
                "instagram.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Instagram";
        }

        if (url.Contains(
                "tiktok.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return "TikTok";
        }

        if (url.Contains(
                "facebook.com",
                StringComparison.OrdinalIgnoreCase) ||
            url.Contains(
                "fb.watch",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Facebook";
        }

        return null;
    }

    // =========================================================
    // ERROR
    // =========================================================

    private Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Telegram bot error.");

        return Task.CompletedTask;
    }
}