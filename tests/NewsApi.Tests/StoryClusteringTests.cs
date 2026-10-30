using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class StoryClusteringTests
{
    private NewsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task RunClustering_FormsCluster_WhenHighSimilarityAndDistinctSources()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clustering:SimilarityThreshold"] = "0.70"
        }).Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        // Add 2 articles covering the same story from 2 distinct sources
        var now = DateTime.UtcNow;
        var art1 = new Article
        {
            Id = "art-1",
            Title = "Dangote Refinery commences direct supply of PMS to Nigerian fuel stations",
            Summary = "Dangote petroleum refinery has begun loading tankers with petrol for nationwide distribution.",
            Source = "Punch",
            Category = "Business",
            PublishedAt = now.AddHours(-2),
            Slug = "dangote-refinery-commences-supply-pms"
        };

        var art2 = new Article
        {
            Id = "art-2",
            Title = "Dangote Refinery begins nationwide distribution of petrol to fuel marketers",
            Summary = "The Dangote petroleum refinery started loading petrol into tankers for supply across Nigeria today.",
            Source = "Vanguard",
            Category = "Business",
            PublishedAt = now.AddHours(-1),
            Slug = "dangote-refinery-nationwide-distribution-marketers"
        };

        // Add 1 unrelated article
        var art3 = new Article
        {
            Id = "art-3",
            Title = "Super Eagles secure victory over Benin Republic in AFCON qualifiers match",
            Summary = "Ademola Lookman scored twice as Nigeria defeated Benin in Uyo.",
            Source = "Premium Times",
            Category = "Sports",
            PublishedAt = now.AddHours(-3),
            Slug = "super-eagles-secure-victory-benin"
        };

        db.Articles.AddRange(art1, art2, art3);
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();

        Assert.Equal(1, count);

        var clusters = await db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .ToListAsync();

        Assert.Single(clusters);
        var cluster = clusters[0];
        Assert.Equal(2, cluster.ArticleCount);
        Assert.Equal(2, cluster.SourceCount);
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "art-1");
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "art-2");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "art-3");
    }

    [Fact]
    public async Task RunClustering_DoesNotCluster_WhenSingleSourceOnly()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clustering:SimilarityThreshold"] = "0.70"
        }).Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var now = DateTime.UtcNow;
        // Two similar articles, but both from the SAME source
        var art1 = new Article
        {
            Id = "art-single-1",
            Title = "President Tinubu arrives in Beijing for high-level bilateral trade summit",
            Summary = "The president landed in China for trade discussions with Chinese authorities.",
            Source = "Punch",
            Category = "Politics",
            PublishedAt = now.AddHours(-4),
            Slug = "tinubu-arrives-beijing-trade-summit"
        };

        var art2 = new Article
        {
            Id = "art-single-2",
            Title = "Tinubu lands in China for state visit and bilateral economic discussions",
            Summary = "President Bola Tinubu has touched down in Beijing for official economic talks.",
            Source = "Punch", // SAME SOURCE
            Category = "Politics",
            PublishedAt = now.AddHours(-2),
            Slug = "tinubu-lands-china-state-visit"
        };

        db.Articles.AddRange(art1, art2);
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();

        // Should NOT form a cluster because SourceCount < 2
        Assert.Equal(0, count);
        var clusters = await db.StoryClusters.ToListAsync();
        Assert.Empty(clusters);
    }

    [Fact]
    public async Task FindRelatedArticlesAsync_UsesVectorProjectionAndCachesResults()
    {
        using var db = CreateInMemoryDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance,
            cache);

        var now = DateTime.UtcNow;
        var target = new Article
        {
            Id = "rel-target",
            Title = "CBN raises monetary policy rate to combat inflation in Nigeria",
            Summary = "The Central Bank of Nigeria increased benchmark interest rates.",
            Category = "Business",
            PublishedAt = now.AddHours(-1),
            Slug = "cbn-raises-mpr-nigeria"
        };

        var sim1 = new Article
        {
            Id = "rel-1",
            Title = "Central Bank of Nigeria hikes interest rates to curb inflation",
            Summary = "CBN monetary policy committee raised the benchmark rate today.",
            Category = "Business",
            PublishedAt = now.AddHours(-2),
            Slug = "cbn-hikes-interest-rates"
        };

        var sim2 = new Article
        {
            Id = "rel-2",
            Title = "Nigerian monetary policy rate raised by Central Bank to tackle inflation",
            Summary = "Interest rate hike announced by CBN Governor in Abuja.",
            Category = "Business",
            PublishedAt = now.AddHours(-3),
            Slug = "nigerian-mpr-raised-cbn"
        };

        var sim3 = new Article
        {
            Id = "rel-3",
            Title = "CBN benchmark interest rate hike impacts commercial lending in Nigeria",
            Summary = "The Central Bank of Nigeria raised interest rates to fight inflation.",
            Category = "Business",
            PublishedAt = now.AddHours(-4),
            Slug = "cbn-benchmark-interest-rate-lending"
        };

        db.Articles.AddRange(target, sim1, sim2, sim3);
        await db.SaveChangesAsync();

        await service.RunClusteringAsync();

        var relatedFirst = await service.FindRelatedArticlesAsync("rel-target", limit: 3);
        Assert.Equal(3, relatedFirst.Count);

        // Remove articles from DB to prove second call hits IMemoryCache
        db.Articles.RemoveRange(sim1, sim2, sim3);
        await db.SaveChangesAsync();

        var relatedCached = await service.FindRelatedArticlesAsync("rel-target", limit: 3);
        Assert.Equal(3, relatedCached.Count);
    }

    [Fact]
    public async Task RunClustering_FormsCluster_WithDefaultConfig_WhenCrossPublisherStoriesShareEvent()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().Build(); // Default threshold (no manual override)

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var now = DateTime.UtcNow;
        db.Articles.AddRange(
            new Article
            {
                Id = "def-1",
                Title = "CBN Governor Cardoso announces new Naira foreign exchange rules for banks",
                Summary = "Central Bank of Nigeria introduces updated FX market guidelines.",
                Source = "BusinessDay",
                Category = "Business",
                PublishedAt = now.AddHours(-2),
                Slug = "cbn-cardoso-naira-fx-rules"
            },
            new Article
            {
                Id = "def-2",
                Title = "Cardoso unveils fresh CBN foreign exchange guidelines to stabilize Naira",
                Summary = "New circular from the apex bank targets liquidity in the FX market.",
                Source = "Premium Times",
                Category = "Business",
                PublishedAt = now.AddHours(-1),
                Slug = "cardoso-unveils-cbn-fx-guidelines"
            });
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RunClustering_DoesNotMerge_UnrelatedPoliceOrStateStories()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var now = DateTime.UtcNow;
        db.Articles.AddRange(
            new Article
            {
                Id = "kebbi-1",
                Title = "Police confirm three deaths, inspector injured in Kebbi violence",
                Summary = "Kebbi State Police Command confirmed three people died in the clash.",
                Source = "Daily Post Nigeria",
                Category = "Crime",
                PublishedAt = now.AddHours(-2),
                Slug = "police-confirm-three-deaths-kebbi"
            },
            new Article
            {
                Id = "kebbi-2",
                Title = "Police confirm three killed, inspector injured in Kebbi violence",
                Summary = "Three persons were killed and a police inspector injured in Kebbi.",
                Source = "Punch Newspaper",
                Category = "Crime",
                PublishedAt = now.AddHours(-1),
                Slug = "police-confirm-three-killed-kebbi"
            },
            new Article
            {
                Id = "plateau-1",
                Title = "Police confirm six killed in Plateau attack",
                Summary = "Six residents lost their lives during an attack in Plateau State.",
                Source = "Vanguard News",
                Category = "Crime",
                PublishedAt = now.AddHours(-1),
                Slug = "police-confirm-six-killed-plateau"
            },
            new Article
            {
                Id = "lagos-1",
                Title = "Police arrest suspected arms courier in Lagos",
                Summary = "Operatives in Lagos intercepted a suspected gunrunner.",
                Source = "Channels Television",
                Category = "Crime",
                PublishedAt = now.AddHours(-1),
                Slug = "police-arrest-arms-courier-lagos"
            });
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();
        Assert.Equal(1, count);

        var cluster = await db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .SingleAsync();

        Assert.Equal(2, cluster.ArticleCount);
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "kebbi-1");
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "kebbi-2");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "plateau-1");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "lagos-1");
    }

    [Fact]
    public async Task RunClustering_DoesNotMerge_UnrelatedCorporateEarningsReports()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var now = DateTime.UtcNow;
        db.Articles.AddRange(
            new Article
            {
                Id = "transcorp-1",
                Title = "Transcorp reports pre-tax profit of N111.39 billion in 9M 2026 as revenue falls",
                Summary = "Transnational Corporation Plc posted N111.39 billion profit before tax in nine months.",
                Source = "Nairametrics",
                Category = "Business",
                PublishedAt = now.AddHours(-2),
                Slug = "transcorp-pretax-profit-9m-2026"
            },
            new Article
            {
                Id = "transcorp-2",
                Title = "Transcorp reports ₦111.4bn profit before tax in 9M 2026 - The Guardian Nigeria News",
                Summary = "Transcorp Plc recorded strong nine-month pre-tax profit of N111.4 billion.",
                Source = "The Guardian Nigeria",
                Category = "Business",
                PublishedAt = now.AddHours(-1),
                Slug = "transcorp-reports-111bn-profit-9m-2026"
            },
            new Article
            {
                Id = "breweries-1",
                Title = "International Breweries’ 9M 2026 pre-tax profit rises 22% to N90.44 billion",
                Summary = "International Breweries Plc grew its pre-tax profit in the first nine months of 2026.",
                Source = "BusinessDay",
                Category = "Business",
                PublishedAt = now.AddHours(-1),
                Slug = "international-breweries-9m-2026-profit"
            },
            new Article
            {
                Id = "ikeja-1",
                Title = "Ikeja Hotel reports N8.19 billion pre-tax profit in 9M 2026, up 15%",
                Summary = "Ikeja Hotel Plc announced N8.19 billion pre-tax profit for 9M 2026.",
                Source = "Punch Newspaper",
                Category = "Business",
                PublishedAt = now.AddHours(-1),
                Slug = "ikeja-hotel-9m-2026-profit"
            });
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();
        Assert.Equal(1, count);

        var cluster = await db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .SingleAsync();

        Assert.Equal(2, cluster.ArticleCount);
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "transcorp-1");
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "transcorp-2");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "breweries-1");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "ikeja-1");
    }
}
