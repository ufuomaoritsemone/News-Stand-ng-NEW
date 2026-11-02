namespace NewsApi.Services;

public class FacebookPublishResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PermalinkUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FacebookPreviewResult
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool HasAiSummary { get; set; }
    public string? AiEngine { get; set; }
}

public interface IFacebookPublishingService
{
    Task<FacebookPreviewResult> GeneratePreviewAsync(string articleId, CancellationToken ct = default);
    Task<FacebookPublishResult> PublishArticlePostAsync(string articleId, string? customMessage = null, bool asPhotoPost = false, CancellationToken ct = default);
    Task<bool> ScrapeOpenGraphCacheAsync(string canonicalUrl, CancellationToken ct = default);
}

