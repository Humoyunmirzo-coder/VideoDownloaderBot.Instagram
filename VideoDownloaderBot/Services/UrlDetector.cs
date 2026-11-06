using VideoDownloaderBot.Entities;
using VideoDownloaderBot.Interfaces;

namespace VideoDownloaderBot.Services
{
    public class UrlDetector : IUrlDetector
    {
        private static readonly Dictionary<
            SourcePlatform,
            string[]> SupportedHosts = new()
        {
        {
            SourcePlatform.YouTube,
            new[]
            {
                "youtube.com",
                "www.youtube.com",
                "m.youtube.com",
                "youtu.be"
            }
        },

        {
            SourcePlatform.Instagram,
            new[]
            {
                "instagram.com",
                "www.instagram.com"
            }
        },

        {
            SourcePlatform.Pinterest,
            new[]
            {
                "pinterest.com",
                "www.pinterest.com",
                "pin.it"
            }
        },

        {
            SourcePlatform.TikTok,
            new[]
            {
                "tiktok.com",
                "www.tiktok.com",
                "vm.tiktok.com"
            }
        }
        };

        public bool TryDetect(
            string url,
            out SourcePlatform platform)
        {
            platform = SourcePlatform.Unknown;

            if (!Uri.TryCreate(
                    url,
                    UriKind.Absolute,
                    out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeHttp)
            {
                return false;
            }

            foreach (var item in SupportedHosts)
            {
                foreach (var host in item.Value)
                {
                    if (uri.Host.Equals(
                            host,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        platform = item.Key;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsSupported(string url)
        {
            return TryDetect(
                url,
                out _);
        }
    }
}
