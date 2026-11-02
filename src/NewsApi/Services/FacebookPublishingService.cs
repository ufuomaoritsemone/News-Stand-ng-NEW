using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public class FacebookPublishingService(
    NewsDbContext db,
    IHttpClientFactory httpClientFactory,
    ISystemSettingsService systemSettings,
    IArticleEnrichmentService enrichmentService,
    ISlugService slugService,
    IConfiguration config,
    ILogger<FacebookPublishingService> logger) : IFacebookPublishingService
{
    private const string GraphApiVersion = "v21.0";
    private const string GraphApiBaseUrl = $"https://graph.facebook.com/{GraphApiVersion}";

    public async Task<FacebookPreviewResult> GeneratePreviewAsync(string articleId, CancellationToken ct = default)
    {
        var article = await db.Articles
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Id == articleId, ct)
            ?? throw new KeyNotFoundException($"Article with ID '{articleId}' not found.");

        // If article hasn't been enriched yet, attempt enrichment on demand
        if (article.Enrichment == null)
        {
            try
            {
                article.Enrichment = await enrichmentService.EnrichArticleAsync(article.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enrich article '{Id}' during Facebook preview generation.", articleId);
            }
        }

        var canonicalUrl = BuildCanonicalUrl(article);
        var formattedMessage = FormatFacebookMessage(article, article.Enrichment);

        return new FacebookPreviewResult
        {
            Title = article.Title,
            Message = formattedMessage,
            CanonicalUrl = canonicalUrl,
            ImageUrl = article.ImageUrl,
            HasAiSummary = article.Enrichment != null,
            AiEngine = article.Enrichment?.Engine
        };
    }

    public async Task<FacebookPublishResult> PublishArticlePostAsync(
        string articleId,
        string? customMessage = null,
        bool asPhotoPost = false,
        CancellationToken ct = default)
    {
        var pageId = await ResolveSettingAsync("Facebook:PageId", "FACEBOOK_PAGE_ID", ct);
        var pageToken = await ResolveSettingAsync("Facebook:PageAccessToken", "FACEBOOK_PAGE_ACCESS_TOKEN", ct);

        if (string.IsNullOrWhiteSpace(pageId))
        {
            return new FacebookPublishResult
            {
                Success = false,
                ErrorMessage = "Facebook Page ID is not configured in Settings (Facebook:PageId or FACEBOOK_PAGE_ID)."
            };
        }

        if (string.IsNullOrWhiteSpace(pageToken))
        {
            return new FacebookPublishResult
            {
                Success = false,
                ErrorMessage = "Facebook Page Access Token is not configured in Settings (Facebook:PageAccessToken or FACEBOOK_PAGE_ACCESS_TOKEN)."
            };
        }

        var article = await db.Articles
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Id == articleId, ct);

        if (article == null)
        {
            return new FacebookPublishResult
            {
                Success = false,
                ErrorMessage = $"Article with ID '{articleId}' not found."
            };
        }

        if (article.Enrichment == null)
        {
            try
            {
                article.Enrichment = await enrichmentService.EnrichArticleAsync(article.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enrich article '{Id}' prior to Facebook publish.", articleId);
            }
        }

        var canonicalUrl = BuildCanonicalUrl(article);
        var message = !string.IsNullOrWhiteSpace(customMessage)
            ? customMessage.Trim()
            : FormatFacebookMessage(article, article.Enrichment);

        // Pre-scrape URL with Graph API Scrape endpoint to ensure Open Graph cache is hot
        await ScrapeOpenGraphCacheAsync(canonicalUrl, ct);

        using var client = httpClientFactory.CreateClient("FacebookPublishingClient");
        client.Timeout = TimeSpan.FromSeconds(30);

        try
        {
            HttpResponseMessage response;

            // Strategy A: Photo Post via POST /{page-id}/photos if requested and image is present
            if (asPhotoPost && !string.IsNullOrWhiteSpace(article.ImageUrl) && Uri.TryCreate(article.ImageUrl, UriKind.Absolute, out _))
            {
                var photoPayload = new Dictionary<string, string>
                {
                    ["url"] = article.ImageUrl,
                    ["caption"] = message,
                    ["access_token"] = pageToken
                };

                response = await client.PostAsync(
                    $"{GraphApiBaseUrl}/{pageId}/photos",
                    new FormUrlEncodedContent(photoPayload),
                    ct);
            }
            else
            {
                // Strategy B: Standard Feed Link Post via POST /{page-id}/feed (Official Meta recommendation)
                var feedPayload = new Dictionary<string, string>
                {
                    ["message"] = message,
                    ["link"] = canonicalUrl,
                    ["access_token"] = pageToken
                };

                response = await client.PostAsync(
                    $"{GraphApiBaseUrl}/{pageId}/feed",
                    new FormUrlEncodedContent(feedPayload),
                    ct);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Facebook Graph API publish failed ({StatusCode}): {Body}", response.StatusCode, responseBody);
                string errDetail;
                try
                {
                    using var errDoc = JsonDocument.Parse(responseBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var msgProp))
                    {
                        errDetail = msgProp.GetString() ?? responseBody;
                    }
                    else
                    {
                        errDetail = responseBody;
                    }
                }
                catch
                {
                    errDetail = responseBody;
                }

                return new FacebookPublishResult
                {
                    Success = false,
                    ErrorMessage = $"Facebook Graph API Error ({response.StatusCode}): {errDetail}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var postId = doc.RootElement.TryGetProperty("id", out var idProp)
                ? idProp.GetString()
                : (doc.RootElement.TryGetProperty("post_id", out var pIdProp) ? pIdProp.GetString() : null);

            var permalinkUrl = !string.IsNullOrWhiteSpace(postId)
                ? $"https://www.facebook.com/{postId}"
                : $"https://www.facebook.com/{pageId}";

            logger.LogInformation("Successfully published article '{ArticleId}' to Facebook Page '{PageId}'. Post ID: {PostId}",
                articleId, pageId, postId);

            return new FacebookPublishResult
            {
                Success = true,
                PostId = postId,
                PermalinkUrl = permalinkUrl
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception publishing article '{ArticleId}' to Facebook.", articleId);
            return new FacebookPublishResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> ScrapeOpenGraphCacheAsync(string canonicalUrl, CancellationToken ct = default)
    {
        try
        {
            var pageToken = await ResolveSettingAsync("Facebook:PageAccessToken", "FACEBOOK_PAGE_ACCESS_TOKEN", ct);
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var scrapeEndpoint = $"{GraphApiBaseUrl}/?id={Uri.EscapeDataString(canonicalUrl)}&scrape=true";
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                scrapeEndpoint += $"&access_token={pageToken}";
            }

            var scrapeRes = await client.PostAsync(scrapeEndpoint, null, ct);
            if (scrapeRes.IsSuccessStatusCode)
            {
                logger.LogDebug("Successfully pre-scraped Open Graph metadata for {Url}", canonicalUrl);
                return true;
            }

            logger.LogWarning("Open Graph scrape returned status {Status} for {Url}", scrapeRes.StatusCode, canonicalUrl);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Open Graph pre-scrape request failed for {Url}", canonicalUrl);
            return false;
        }
    }

    private string FormatFacebookMessage(Article article, ArticleEnrichment? enrichment)
    {
        var sb = new StringBuilder();

        // 1. Hook / Headline
        sb.AppendLine($"📰 {article.Title.Trim()}");
        sb.AppendLine();

        // 2. Executive Overview / AI Briefing
        var overview = !string.IsNullOrWhiteSpace(enrichment?.FullSummary?.ExecutiveOverview)
            ? enrichment.FullSummary.ExecutiveOverview
            : (!string.IsNullOrWhiteSpace(article.Excerpt) ? article.Excerpt : (article.Summary ?? string.Empty));

        if (!string.IsNullOrWhiteSpace(overview))
        {
            sb.AppendLine(overview.Trim());
            sb.AppendLine();
        }

        // 3. Key Takeaways (Bullets)
        var takeaways = enrichment?.KeyTakeaways;
        if (takeaways != null && takeaways.Count > 0)
        {
            sb.AppendLine("🔑 Key Takeaways:");
            foreach (var point in takeaways.Take(3))
            {
                if (!string.IsNullOrWhiteSpace(point))
                {
                    sb.AppendLine($"• {point.Trim()}");
                }
            }
            sb.AppendLine();
        }

        // 4. Canonical Call-To-Action Link
        var canonicalUrl = BuildCanonicalUrl(article);
        sb.AppendLine($"👉 Read the full executive briefing: {canonicalUrl}");
        sb.AppendLine();

        // 5. Hashtags
        var categoryTag = (article.Category ?? "Nigeria").Replace(" ", "");
        sb.AppendLine($"#NigeriaNews #NewsStandNG #{categoryTag} #NigerianNews");

        return sb.ToString().TrimEnd();
    }

    private string BuildCanonicalUrl(Article article)
    {
        var host = config["IndexNow:Host"] ?? "newsstand.ng";
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = new Uri(host).Host;
        }

        var category = !string.IsNullOrWhiteSpace(article.Category)
            ? article.Category.Trim().ToLowerInvariant()
            : "general";

        var slug = !string.IsNullOrWhiteSpace(article.Slug)
            ? article.Slug
            : slugService.GenerateSlug(article.Title);

        return $"https://{host}/news/{category}/{slug}";
    }

    private async Task<string?> ResolveSettingAsync(string dbKey, string envKey, CancellationToken ct)
    {
        try
        {
            var dbVal = await systemSettings.GetSettingAsync(dbKey, null, ct);
            if (!string.IsNullOrWhiteSpace(dbVal)) return dbVal.Trim();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve setting '{DbKey}' from systemSettings; checking config/environment.", dbKey);
        }

        var configVal = config[dbKey]
            ?? config[dbKey.Replace(":", "__")]
            ?? Environment.GetEnvironmentVariable(envKey)
            ?? Environment.GetEnvironmentVariable(dbKey.Replace(":", "__"));

        return string.IsNullOrWhiteSpace(configVal) ? null : configVal.Trim();
    }
}

