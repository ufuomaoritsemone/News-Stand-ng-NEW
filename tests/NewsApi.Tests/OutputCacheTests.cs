using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Extensions;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApi.Tests;

public class OutputCacheTests
{
    private class MockOutputCacheStore : IOutputCacheStore
    {
        public List<string> EvictedTags { get; } = [];

        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
        {
            EvictedTags.Add(tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult<byte[]?>(null);

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }

    [Fact]
    public void AddApplicationServices_ConfiguresExpectedOutputCachePolicies()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructureServices(config);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<OutputCacheOptions>>().Value;

        Assert.NotNull(options);
    }

    [Fact]
    public async Task ArticleIngestion_EvictsArticlesAndStoriesTags_WhenNewArticlesIngested()
    {
        var dbOptions = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new NewsDbContext(dbOptions);

        var slugService = new SlugService();
        var cacheStore = new MockOutputCacheStore();
        using var memoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var briefingService = new BriefingService(db, memoryCache, NullLogger<BriefingService>.Instance);

        var service = new ArticleIngestionService(
            db,
            slugService,
            briefingService,
            NullLogger<ArticleIngestionService>.Instance,
            outputCacheStore: cacheStore);

        var request = new IngestArticlesRequest
        {
            Articles =
            [
                new ArticleIngestItem
                {
                    Title = "Test Breaking News Headline Across Nigeria",
                    Url = "https://punchng.com/news/test-breaking-news",
                    Source = "Punch",
                    Category = "Politics"
                }
            ]
        };

        var response = await service.IngestArticlesAsync(request);

        Assert.Equal(1, response.IngestedCount);
        Assert.Contains("articles", cacheStore.EvictedTags);
        Assert.Contains("stories", cacheStore.EvictedTags);
    }

    [Fact]
    public async Task TriggerClustering_EvictsStoriesTag_WhenOutputCacheStoreRegistered()
    {
        var dbOptions = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new NewsDbContext(dbOptions);

        var config = new ConfigurationBuilder().Build();
        var cacheStore = new MockOutputCacheStore();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var clusteringService = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance,
            outputCacheStore: cacheStore);

        var controller = new StoriesController(
            db,
            clusteringService,
            config,
            NullLogger<StoriesController>.Instance,
            outputCacheStore: cacheStore);

        var actionResult = await controller.TriggerClustering();

        Assert.NotNull(actionResult);
        Assert.Contains("stories", cacheStore.EvictedTags);
    }
}
