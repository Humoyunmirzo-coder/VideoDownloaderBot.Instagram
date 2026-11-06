using VideoDownloaderBot.Entities;

namespace VideoDownloaderBot.Interfaces
{
    public interface IUrlDetector
    {
        bool TryDetect(string url,out SourcePlatform platform);

        bool IsSupported(string url);
    }
}
