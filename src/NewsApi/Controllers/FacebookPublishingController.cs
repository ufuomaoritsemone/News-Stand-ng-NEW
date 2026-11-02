using Microsoft.AspNetCore.Mvc;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/social/facebook")]
public class FacebookPublishingController(
    IFacebookPublishingService facebookService,
    ILogger<FacebookPublishingController> logger) : ControllerBase
{
    public class FacebookPublishRequest
    {
        public string? CustomMessage { get; set; }
        public bool AsPhotoPost { get; set; } = false;
    }

    /// <summary>
    /// Generates a preview of the Facebook post for an article (formatted caption, AI summary, OpenGraph link).
    /// </summary>
    [HttpGet("preview/{articleId}")]
    public async Task<IActionResult> GetPreview(string articleId, CancellationToken ct)
    {
        try
        {
            var preview = await facebookService.GeneratePreviewAsync(articleId, ct);
            return Ok(preview);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Article '{articleId}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate Facebook preview for article '{ArticleId}'.", articleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Publishes an article summary to the configured Facebook Page via Graph API.
    /// </summary>
    [HttpPost("publish/{articleId}")]
    public async Task<IActionResult> PublishPost(
        string articleId,
        [FromBody] FacebookPublishRequest? request = null,
        CancellationToken ct = default)
    {
        var result = await facebookService.PublishArticlePostAsync(
            articleId,
            request?.CustomMessage,
            request?.AsPhotoPost ?? false,
            ct);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                error = result.ErrorMessage
            });
        }

        return Ok(new
        {
            success = true,
            postId = result.PostId,
            permalinkUrl = result.PermalinkUrl
        });
    }

    /// <summary>
    /// Manually triggers Facebook to scrape and refresh the Open Graph cache for a specific URL.
    /// </summary>
    [HttpPost("scrape-og")]
    public async Task<IActionResult> ScrapeOpenGraph([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "Url parameter is required." });
        }

        var success = await facebookService.ScrapeOpenGraphCacheAsync(url, ct);
        return Ok(new { success, url });
    }
}

