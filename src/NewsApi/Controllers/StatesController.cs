using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class StatesController(
    NewsDbContext db,
    ILogger<StatesController> logger,
    ISlugService? slugService = null) : ControllerBase
{
    /// <summary>
    /// Returns all 36 states + FCT grouped by geopolitical zone with article counts.
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "PublicStates")]
    [ProducesResponseType(typeof(List<StateZoneGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<ActionResult<List<StateZoneGroupDto>>> GetStates(CancellationToken ct = default)
    {
        var allStates = NigerianStateRegistry.AllStates;

        // Collect all candidate topic slugs and names for the 36 states + FCT
        var allCandidateSlugs = allStates
            .SelectMany(NigerianStateRegistry.GetCandidateSlugs)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allCandidateNames = allStates
            .SelectMany(NigerianStateRegistry.GetCandidateTopicNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Load topics that match any state slug, name, or Place type along with their distinct ArticleIds
        var matchedTopics = await db.Topics
            .AsNoTracking()
            .Where(t => t.Type == TopicType.Place ||
                        allCandidateSlugs.Contains(t.Slug) ||
                        allCandidateNames.Contains(t.Name))
            .Select(t => new
            {
                t.Id,
                Slug = t.Slug.ToLower(),
                Name = t.Name.ToLower(),
                ArticleIds = t.ArticleTopics.Select(at => at.ArticleId).ToList()
            })
            .ToListAsync(ct);

        // If any state has 0 topic-linked articles, also check recent article titles/summaries in a single pass
        var statesMissingTopics = allStates
            .Where(s =>
            {
                var slugs = NigerianStateRegistry.GetCandidateSlugs(s).Select(x => x.ToLowerInvariant()).ToHashSet();
                var names = NigerianStateRegistry.GetCandidateTopicNames(s).Select(x => x.ToLowerInvariant()).ToHashSet();
                return !matchedTopics.Any(t => (slugs.Contains(t.Slug) || names.Contains(t.Name)) && t.ArticleIds.Count > 0);
            })
            .ToList();

        List<(string Id, string TitleLower, string SummaryLower)> recentArticleTexts = [];
        if (statesMissingTopics.Count > 0)
        {
            var rawTexts = await db.Articles
                .AsNoTracking()
                .OrderByDescending(a => a.PublishedAt)
                .Take(500)
                .Select(a => new { a.Id, a.Title, a.Summary })
                .ToListAsync(ct);

            recentArticleTexts = rawTexts
                .Select(a => (a.Id, (a.Title ?? string.Empty).ToLowerInvariant(), (a.Summary ?? string.Empty).ToLowerInvariant()))
                .ToList();
        }

        var dtos = allStates.Select(s =>
        {
            var candidateSlugs = NigerianStateRegistry.GetCandidateSlugs(s)
                .Select(x => x.ToLowerInvariant())
                .ToHashSet();
            var candidateNames = NigerianStateRegistry.GetCandidateTopicNames(s)
                .Select(x => x.ToLowerInvariant())
                .ToHashSet();

            var distinctArticleIds = matchedTopics
                .Where(t => candidateSlugs.Contains(t.Slug) || candidateNames.Contains(t.Name))
                .SelectMany(t => t.ArticleIds)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (distinctArticleIds.Count == 0 && recentArticleTexts.Count > 0)
            {
                var stateTerm = s.Slug == "fct" ? "abuja" : s.Name.ToLowerInvariant();
                var capitalTerm = s.Capital.ToLowerInvariant();

                foreach (var art in recentArticleTexts)
                {
                    if (art.TitleLower.Contains(stateTerm) ||
                        art.SummaryLower.Contains(stateTerm) ||
                        art.TitleLower.Contains(capitalTerm))
                    {
                        distinctArticleIds.Add(art.Id);
                    }
                }
            }

            return new StateSummaryDto
            {
                Name = s.Name,
                Slug = s.Slug,
                Capital = s.Capital,
                GeopoliticalZone = s.GeopoliticalZone,
                Slogan = s.Slogan,
                ArticleCount = distinctArticleIds.Count,
                MajorCities = [.. s.MajorCities]
            };
        }).ToList();

        var totalArticlesAcrossStates = dtos.Sum(d => d.ArticleCount);
        var eTag = $"\"states_dir_{dtos.Count}_{totalArticlesAcrossStates}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=180");

        var grouped = dtos
            .GroupBy(s => s.GeopoliticalZone)
            .OrderBy(g => ZoneOrder(g.Key))
            .Select(g => new StateZoneGroupDto
            {
                Zone = g.Key,
                States = g.OrderBy(s => s.Name).ToList()
            })
            .ToList();

        return Ok(grouped);
    }

    /// <summary>
    /// Returns state landing hub details, paginated & category-filtered localized news articles, top topics, and FAQ schema.
    /// </summary>
    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "PublicStates")]
    [ProducesResponseType(typeof(StateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StateDetailDto>> GetStateBySlug(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 18,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var stateInfo = NigerianStateRegistry.FindBySlug(slug);
        if (stateInfo is null)
        {
            return NotFound(new { message = $"State '{slug}' not found." });
        }

        logger.LogDebug("Retrieving state hub details for {StateSlug} (page={Page}, pageSize={PageSize}, category={Category})",
            stateInfo.Slug, page, pageSize, category);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 5, 50);
        var normalizedCategory = string.IsNullOrWhiteSpace(category) || category.Equals("All", StringComparison.OrdinalIgnoreCase)
            ? null
            : category.Trim();

        // Find topics related to this state by canonical/legacy slug, state name, capital, and major cities
        var candidateSlugs = NigerianStateRegistry.GetCandidateSlugs(stateInfo);
        var candidateNames = NigerianStateRegistry.GetCandidateTopicNames(stateInfo);

        var matchingTopicIds = await db.Topics
            .AsNoTracking()
            .Where(t => candidateSlugs.Contains(t.Slug) || candidateNames.Contains(t.Name))
            .Select(t => t.Id)
            .ToListAsync(ct);

        // Hybrid query: combine ArticleTopics links with title/summary keyword matches so seeded-but-empty
        // topics or untagged RSS articles still surface reliably.
        var stateTerm = stateInfo.Slug == "fct" ? "abuja" : stateInfo.Name.ToLowerInvariant();
        var capitalTerm = stateInfo.Capital.ToLowerInvariant();

        IQueryable<Article> baseStateQuery;
        if (matchingTopicIds.Count > 0)
        {
            baseStateQuery = db.Articles
                .AsNoTracking()
                .Where(a => a.ArticleTopics.Any(at => matchingTopicIds.Contains(at.TopicId)) ||
                            a.Title.ToLower().Contains(stateTerm) ||
                            (a.Summary != null && a.Summary.ToLower().Contains(stateTerm)) ||
                            a.Title.ToLower().Contains(capitalTerm));
        }
        else
        {
            baseStateQuery = db.Articles
                .AsNoTracking()
                .Where(a => a.Title.ToLower().Contains(stateTerm) ||
                            (a.Summary != null && a.Summary.ToLower().Contains(stateTerm)) ||
                            a.Title.ToLower().Contains(capitalTerm));
        }

        // Distinct categories available for this state's articles
        var availableCategories = await baseStateQuery
            .Where(a => a.Category != null && a.Category != "")
            .Select(a => a.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        IQueryable<Article> filteredQuery = baseStateQuery;
        if (normalizedCategory is not null)
        {
            var catLower = normalizedCategory.ToLowerInvariant();
            filteredQuery = filteredQuery.Where(a => a.Category != null && a.Category.ToLower() == catLower);
        }

        var totalCount = await filteredQuery.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));

        // Prioritize articles where the state or capital is in the headline or has a high-confidence topic tag,
        // then order chronologically by PublishedAt descending.
        IOrderedQueryable<Article> orderedQuery;
        if (matchingTopicIds.Count > 0)
        {
            orderedQuery = filteredQuery
                .OrderByDescending(a =>
                    a.Title.ToLower().Contains(stateTerm) ||
                    a.Title.ToLower().Contains(capitalTerm) ||
                    a.ArticleTopics.Any(at => matchingTopicIds.Contains(at.TopicId) && at.Confidence >= 0.9f))
                .ThenByDescending(a => a.PublishedAt);
        }
        else
        {
            orderedQuery = filteredQuery
                .OrderByDescending(a =>
                    a.Title.ToLower().Contains(stateTerm) ||
                    a.Title.ToLower().Contains(capitalTerm))
                .ThenByDescending(a => a.PublishedAt);
        }

        var rawArticles = await orderedQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var articles = rawArticles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Slug = !string.IsNullOrEmpty(a.Slug)
                ? a.Slug
                : (slugService?.GenerateArticleSlug(a.Title, a.Id) ?? a.Id),
            Summary = a.Summary,
            Excerpt = a.Excerpt ?? slugService?.GenerateExcerpt(a.Content, a.Summary, 300),
            Source = a.Source ?? string.Empty,
            Category = a.Category ?? string.Empty,
            PublishedAt = a.PublishedAt,
            ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            IsSponsored = a.IsSponsored,
            IsPinned = a.IsPinned
        }).ToList();

        // Top topics associated with this state or co-occurring in its recent articles
        var sampleArticleIds = await baseStateQuery
            .OrderByDescending(a => a.PublishedAt)
            .Take(50)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var topTopics = await db.Topics
            .AsNoTracking()
            .Where(t => candidateSlugs.Contains(t.Slug) ||
                        candidateNames.Contains(t.Name) ||
                        (sampleArticleIds.Count > 0 && t.ArticleTopics.Any(at => sampleArticleIds.Contains(at.ArticleId))))
            .OrderByDescending(t => t.ArticleTopics.Count)
            .Take(8)
            .Select(t => new TopicDto
            {
                Id = t.Id,
                Slug = t.Slug,
                Name = t.Name,
                Type = t.Type.ToString(),
                Description = t.Description,
                ArticleCount = t.ArticleTopics.Count,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        var faqs = new List<FaqItemDto>
        {
            new($"What is the administrative capital of {stateInfo.Name}?",
                $"{stateInfo.Capital} is the official capital city of {stateInfo.Name}, serving as the administrative seat of the state government."),
            new($"Which geopolitical zone does {stateInfo.Name} belong to?",
                $"{stateInfo.Name} is located in the {stateInfo.GeopoliticalZone} geopolitical zone of the Federal Republic of Nigeria."),
            new($"What are the major commercial and metropolitan cities in {stateInfo.Name}?",
                $"The major urban, commercial, and administrative centers in {stateInfo.Name} include {string.Join(", ", stateInfo.MajorCities)}."),
            new($"How often is local news for {stateInfo.Name} updated on News Stand NG?",
                $"News Stand NG aggregates verified news coverage for {stateInfo.Name} in real time from leading national and regional Nigerian newsrooms, updating as breaking stories develop.")
        };

        var latestArticleTicks = articles.FirstOrDefault()?.PublishedAt?.Ticks ?? 0;
        var eTag = $"\"state_{stateInfo.Slug}_p{safePage}_ps{safePageSize}_c{normalizedCategory ?? "all"}_t{totalCount}_{latestArticleTicks}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=180");

        var detail = new StateDetailDto
        {
            Name = stateInfo.Name,
            Slug = stateInfo.Slug,
            Capital = stateInfo.Capital,
            GeopoliticalZone = stateInfo.GeopoliticalZone,
            Slogan = stateInfo.Slogan,
            Intro = stateInfo.Intro,
            ArticleCount = totalCount,
            Page = safePage,
            PageSize = safePageSize,
            TotalPages = totalPages,
            ActiveCategory = normalizedCategory,
            AvailableCategories = availableCategories,
            MajorCities = [.. stateInfo.MajorCities],
            Articles = articles,
            TopTopics = topTopics,
            Faqs = faqs
        };

        return Ok(detail);
    }

    private static int ZoneOrder(string zone) => zone switch
    {
        "South West" => 1,
        "South South" => 2,
        "South East" => 3,
        "North Central" => 4,
        "North West" => 5,
        "North East" => 6,
        _ => 99
    };
}
