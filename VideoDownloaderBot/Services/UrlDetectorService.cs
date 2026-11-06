namespace VideoDownloaderBot.Services
{
    public class UrlDetectorService
    {
        private static readonly string[] SupportedDomains =
        {
        "youtube.com",
        "youtu.be",
        "youtube-nocookie.com",

        "instagram.com",
        "instagr.am",

        "tiktok.com",
        "vm.tiktok.com",

        "facebook.com",
        "fb.watch",

        "twitter.com",
        "x.com"
    };

        public bool IsSupportedUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();

            return SupportedDomains.Any(domain =>
                host == domain ||
                host.EndsWith("." + domain));
        }

        public string DetectPlatform(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return "Unknown";

            var host = uri.Host.ToLowerInvariant();

            if (host.Contains("youtube") || host.Contains("youtu.be"))
                return "YouTube";

            if (host.Contains("instagram"))
                return "Instagram";

            if (host.Contains("tiktok"))
                return "TikTok";

            if (host.Contains("facebook") || host.Contains("fb.watch"))
                return "Facebook";

            if (host.Contains("twitter") || host.Contains("x.com"))
                return "Twitter/X";

            return "Unknown";
        }
    }
}
