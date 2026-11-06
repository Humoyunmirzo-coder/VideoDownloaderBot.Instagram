using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VideoDownloaderBot.Entities;
using VideoDownloaderBot.Interfaces;
using static VideoDownloaderBot.DTOs.CreateDownload;

namespace VideoDownloaderBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DownloadController : ControllerBase
    {
        private readonly IJobRepository _repository;
        private readonly IUrlDetector _urlDetector;

        public DownloadController(
            IJobRepository repository,
            IUrlDetector urlDetector)
        {
            _repository = repository;
            _urlDetector = urlDetector;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateDownloadRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(
                    request.Url))
            {
                return BadRequest(new
                {
                    error = "URL is required."
                });
            }

            var url =
                request.Url.Trim();

            if (!_urlDetector.TryDetect(
                    url,
                    out var platform))
            {
                return BadRequest(new
                {
                    error =
                        "Unsupported or invalid public video URL."
                });
            }

            var job =
                new DownloadJob
                {
                    Url = url,
                    Platform = platform
                };

            await _repository.AddAsync(
                job,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            return Accepted(
                $"/api/download/{job.Id}",
                new CreateDownloadResponse(
                    job.Id,
                    job.Status.ToString(),
                    null));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(
            Guid id,
            CancellationToken cancellationToken)
        {
            var job =
                await _repository.GetByIdAsync(
                    id,
                    cancellationToken);

            if (job == null)
            {
                return NotFound(new
                {
                    error = "Job not found."
                });
            }

            string? downloadUrl = null;

            if (job.Status ==
                    DownloadStatus.Completed &&
                !string.IsNullOrWhiteSpace(
                    job.FileName))
            {
                downloadUrl =
                    $"{Request.Scheme}://" +
                    $"{Request.Host}" +
                    $"/files/{Uri.EscapeDataString(job.FileName)}";
            }

            return Ok(
                new DownloadJobResponse(
                    job.Id,
                    job.Url,
                    job.Platform.ToString(),
                    job.Status.ToString(),
                    job.Progress,
                    job.Title,
                    job.FileSize,
                    downloadUrl,
                    job.Error));
        }
    }
}
