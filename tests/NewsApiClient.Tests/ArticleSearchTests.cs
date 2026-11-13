using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NigerianNewsGrid.Client.Helpers;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Integration and unit tests for Full-Text Search and BM25 relevance ranking (Problem 6).
/// </summary>
public class ArticleSearchTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ArticleSearchTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void FormatFts5Query_SanitizesPunctuationAndAddsPrefixWildcards()
    {
        var input = "Tinubu & (Economy) + \"oil\"? *** ::::";
        var result = ArticleSearchService.FormatFts5Query(input);

        Assert.Contains("\"Tinubu\"*", result);
        Assert.Contains("\"Economy\"*", result);
        Assert.Contains("\"oil\"*", result);
        Assert.DoesNotContain("&", result);
        Assert.DoesNotContain("(", result);
        Assert.DoesNotContain(")", result);
        Assert.DoesNotContain(":", result);
    }

    [Fact]
    public void FormatFts5Query_EmptyOrWhitespace_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, ArticleSearchService.FormatFts5Query("   "));
        Assert.Equal(string.Empty, ArticleSearchService.FormatFts5Query(null!));
        Assert.Equal(string.Empty, ArticleSearchService.FormatFts5Query("??? !!!"));
    }

    [Fact]
    public async Task Search_ExactTitleMatch_ReturnsRankedResult()
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var title = $"Presidential Address on Fiscal Reform {uniqueToken}";
        var articleUrl = $"https://example.com/search-exact-{uniqueToken}";

        await IngestTestArticleAsync(title, "Summary of fiscal policy", "Full content of reform speech", articleUrl, "Punch", "Politics");

        var response = await _client.GetAsync($"/api/v1/articles?search={uniqueToken}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
        Assert.NotEmpty(articles);
        Assert.Contains(articles, a => a.Title == title);
    }

    [Fact]
    public async Task Search_MultiWordAndPrefixMatch_FindsRelevantArticles()
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..8];
        var title = $"Dangote Petrochemical Complex In Lagos {uniqueToken}";
        var articleUrl = $"https://example.com/search-prefix-{uniqueToken}";

        await IngestTestArticleAsync(title, "Production ramps up", "Full story on crude processing", articleUrl, "Vanguard", "Business");

        // Prefix query: "Petrochem" matches "Petrochemical"
        var response = await _client.GetAsync($"/api/v1/articles?search=Petrochem {uniqueToken}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
        Assert.Contains(articles, a => a.Title == title);
    }

    [Fact]
    public async Task Search_TitleMatchRanksHigherThanContentMatch_ViaBM25()
    {
        var tag = "NairaFloat" + Guid.NewGuid().ToString("N")[..6];

        // Article A has keyword in TITLE
        var titleA = $"Breaking: {tag} Guidelines Announced by CBN";
        var urlA = $"https://example.com/art-a-{Guid.NewGuid():N}";
        await IngestTestArticleAsync(titleA, "Foreign exchange policy details.", "Standard economic background notes.", urlA, "TheCable", "Economy");

        // Article B has keyword ONLY in CONTENT (Title is generic)
        var titleB = $"Agriculture Export Expansion Report {Guid.NewGuid():N}";
        var urlB = $"https://example.com/art-b-{Guid.NewGuid():N}";
        await IngestTestArticleAsync(titleB, "Cocoa and cashew production figures.", $"Farmers discuss how the {tag} impacts export revenues.", urlB, "BusinessDay", "Economy");

        var response = await _client.GetAsync($"/api/v1/articles?search={tag}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
        Assert.True(articles.Count >= 2, "Expected both articles to be found.");

        var indexA = articles.FindIndex(a => a.Title == titleA);
        var indexB = articles.FindIndex(a => a.Title == titleB);

        Assert.True(indexA >= 0, "Article A must be found.");
        Assert.True(indexB >= 0, "Article B must be found.");
        Assert.True(indexA < indexB, $"Article with title match (index {indexA}) must rank higher than article with content match (index {indexB}).");
    }

    [Fact]
    public async Task Search_CombinedWithCategoryAndSource_FiltersAccurately()
    {
        var tag = "SubsidyCrisis" + Guid.NewGuid().ToString("N")[..6];

        // Article in Politics / Punch
        var titlePolitics = $"House of Reps Debates {tag}";
        await IngestTestArticleAsync(titlePolitics, "Debate on petrol pricing.", "Floor transcripts...", $"https://example.com/p-{Guid.NewGuid():N}", "Punch", "Politics");

        // Article in Business / Premium Times
        var titleBusiness = $"Market Analysis of the {tag}";
        await IngestTestArticleAsync(titleBusiness, "Financial markets respond.", "Market charts...", $"https://example.com/b-{Guid.NewGuid():N}", "Premium Times", "Business");

        // Search for tag filtered by Category=Business and Source=Premium Times
        var response = await _client.GetAsync($"/api/v1/articles?search={tag}&category=Business&source=Premium Times");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
        Assert.Contains(articles, a => a.Title == titleBusiness);
        Assert.DoesNotContain(articles, a => a.Title == titlePolitics);
    }

    [Fact]
    public async Task Search_SpecialCharactersAndPunctuation_DoesNotThrow()
    {
        // Query with dangerous FTS characters: unmatched quotes, colons, asterisks, ampersands
        var trickyQuery = "Tinubu & (Forex) + \"oil\"? *** :::: OR NOT";
        var response = await _client.GetAsync($"/api/v1/articles?search={Uri.EscapeDataString(trickyQuery)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsStandardListing()
    {
        var response = await _client.GetAsync("/api/v1/articles?search=%20%20%20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
    }

    [Fact]
    public async Task Search_WithSortByDate_ReturnsMostCurrentStoryOnTop()
    {
        var tag = "DateSortTopic" + Guid.NewGuid().ToString("N")[..6];

        // Older article has keyword in TITLE (higher BM25 relevance, but published 3 days ago)
        var olderTitle = $"Breaking: {tag} Policy Announced";
        var olderUrl = $"https://example.com/art-old-{Guid.NewGuid():N}";
        await IngestTestArticleAsync(
            olderTitle,
            "Older summary.",
            "Older content.",
            olderUrl,
            "TheCable",
            "Politics",
            DateTime.UtcNow.AddDays(-3));

        // Newer article has keyword ONLY in CONTENT (lower BM25 relevance, but published just now)
        var newerTitle = $"Latest Economic Update From Abuja {Guid.NewGuid():N}";
        var newerUrl = $"https://example.com/art-new-{Guid.NewGuid():N}";
        await IngestTestArticleAsync(
            newerTitle,
            "Recent developments.",
            $"Analysts weigh in on how {tag} affects today's market.",
            newerUrl,
            "Punch",
            "Business",
            DateTime.UtcNow);

        var response = await _client.GetAsync($"/api/v1/articles?search={tag}&sortBy=date");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(articles);
        Assert.True(articles.Count >= 2, "Expected both articles to be found.");

        var indexNewer = articles.FindIndex(a => a.Title == newerTitle);
        var indexOlder = articles.FindIndex(a => a.Title == olderTitle);

        Assert.True(indexNewer >= 0, "Newer article must be found.");
        Assert.True(indexOlder >= 0, "Older article must be found.");
        Assert.True(indexNewer < indexOlder, $"Most current story (index {indexNewer}) must appear before older story (index {indexOlder}) when sortBy=date.");
    }

    [Fact]
    public async Task Search_ReturnedArticle_HasValidSlugAndResolvesViaGetBySlugWithout404()
    {
        var uniqueToken = "ShareSlug" + Guid.NewGuid().ToString("N")[..8];
        var title = $"CBN Unveils Digital Remittance Framework {uniqueToken}";
        var articleUrl = $"https://example.com/share-search-{uniqueToken}";

        await IngestTestArticleAsync(
            title,
            "Summary of remittance framework.",
            "Full details of the remittance policy.",
            articleUrl,
            "Channels TV",
            "Business",
            DateTime.UtcNow);

        var searchResponse = await _client.GetAsync($"/api/v1/articles?search={uniqueToken}&sortBy=date");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<ArticleDto>>();
        Assert.NotNull(searchResults);
        var matched = Assert.Single(searchResults, a => a.Title == title);
        Assert.False(string.IsNullOrWhiteSpace(matched.Slug), "Searched article must include a non-empty canonical Slug.");

        var sharedWebUrl = NewsStandUrlHelper.GetArticleWebUrl(matched.Slug, matched.Id, matched.Title, matched.Url);
        var sharedSlug = sharedWebUrl.Split("/news/").Last();

        var slugResponse = await _client.GetAsync($"/api/v1/articles/by-slug/{Uri.EscapeDataString(sharedSlug)}");
        Assert.Equal(HttpStatusCode.OK, slugResponse.StatusCode);

        var detail = await slugResponse.Content.ReadFromJsonAsync<ArticleDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(matched.Id, detail.Id);
        Assert.Equal(title, detail.Title);
    }

    [Fact]
    public async Task GetBySlug_ResolvesSynthesizedAndTitleSlugsWhenDatabaseSlugWasEmpty()
    {
        var uniqueToken = "LegacySlug" + Guid.NewGuid().ToString("N")[..8];
        var articleId = Guid.NewGuid().ToString();
        var title = $"NNPC Announces Crude Supply Expansion {uniqueToken}";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
            db.Articles.Add(new Article
            {
                Id = articleId,
                Title = title,
                Slug = string.Empty, // Simulate legacy/un-backfilled article with empty Slug in DB
                Summary = "Summary of crude supply expansion.",
                Content = "Full content of crude supply expansion.",
                Url = $"https://example.com/legacy-{uniqueToken}",
                Source = "Arise News",
                Category = "Business",
                PublishedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // 1. Resolve via title-only slug (e.g. client/web fallback when slug was missing)
        var titleOnlySlug = NewsStandUrlHelper.Slugify(title);
        var titleSlugResponse = await _client.GetAsync($"/api/v1/articles/by-slug/{Uri.EscapeDataString(titleOnlySlug)}");
        Assert.Equal(HttpStatusCode.OK, titleSlugResponse.StatusCode);

        // 2. Resolve via synthesized SlugService slug (title + 6-char SHA256 hash of ID)
        var slugService = new SlugService();
        var synthesizedSlug = slugService.GenerateArticleSlug(title, articleId);
        var synthesizedResponse = await _client.GetAsync($"/api/v1/articles/by-slug/{Uri.EscapeDataString(synthesizedSlug)}");
        Assert.Equal(HttpStatusCode.OK, synthesizedResponse.StatusCode);

        var detail = await synthesizedResponse.Content.ReadFromJsonAsync<ArticleDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(articleId, detail.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Slug));
    }

    private async Task IngestTestArticleAsync(
        string title,
        string summary,
        string content,
        string url,
        string source,
        string category,
        DateTime? publishedAt = null)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/articles/ingest")
        {
            Content = JsonContent.Create(new IngestArticlesRequest
            {
                Articles =
                [
                    new ArticleIngestItem
                    {
                        Title       = title,
                        Summary     = summary,
                        Content     = content,
                        Url         = url,
                        Source      = source,
                        Category    = category,
                        PublishedAt = publishedAt ?? DateTime.UtcNow
                    }
                ]
            })
        };
        requestMessage.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(requestMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
