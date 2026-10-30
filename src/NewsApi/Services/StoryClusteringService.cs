using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services.Embeddings;

namespace NewsApi.Services;

public partial class StoryClusteringService : IStoryClusteringService
{
    private readonly NewsDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISlugService _slugService;
    private readonly IConfiguration _config;
    private readonly ILogger<StoryClusteringService> _logger;
    private readonly IMemoryCache? _cache;

    public StoryClusteringService(
        NewsDbContext db,
        IEmbeddingProvider embeddingProvider,
        ISlugService slugService,
        IConfiguration config,
        ILogger<StoryClusteringService> logger,
        IMemoryCache? cache = null)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _slugService = slugService;
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "across", "after", "again", "against", "all", "almost", "along", "also",
        "am", "among", "an", "and", "any", "are", "aren", "around", "as", "at", "back", "be", "because",
        "been", "before", "being", "below", "between", "both", "but", "by", "can", "cannot", "could",
        "despite", "did", "didn", "do", "does", "doesn", "doing", "don", "down", "during", "each",
        "eight", "either", "even", "every", "few", "first", "five", "for", "four", "from", "further",
        "get", "gets", "got", "had", "has", "have", "having", "he", "her", "here", "hers", "herself",
        "him", "himself", "his", "how", "however", "if", "in", "into", "is", "isn", "it", "its", "itself",
        "just", "keep", "last", "less", "let", "like", "made", "make", "makes", "many", "may", "me",
        "might", "more", "most", "much", "must", "my", "myself", "near", "never", "new", "next", "nine",
        "no", "nor", "not", "now", "of", "off", "often", "old", "on", "once", "one", "only", "onto",
        "or", "other", "others", "ought", "our", "ours", "ourselves", "out", "over", "own", "part",
        "per", "put", "puts", "rather", "same", "second", "see", "seen", "seven", "several", "shall",
        "she", "should", "since", "six", "so", "some", "still", "such", "take", "taken", "takes", "ten",
        "than", "that", "the", "their", "theirs", "them", "themselves", "then", "there", "these", "they",
        "this", "those", "three", "through", "thus", "to", "today", "together", "tomorrow", "too",
        "toward", "towards", "two", "under", "until", "up", "upon", "us", "use", "used", "using", "very",
        "via", "want", "wants", "was", "wasn", "way", "ways", "we", "well", "were", "weren", "what",
        "when", "where", "whether", "which", "while", "who", "whom", "whose", "why", "will", "with",
        "within", "without", "won", "would", "year", "years", "yesterday", "yet", "you", "your", "yours"
    };

    /// <summary>
    /// Broad news reporting verbs, generic crime/court/earnings boilerplate, and publisher brand tokens.
    /// Sharing ONLY these tokens must never cause two articles about different subjects to cluster together.
    /// </summary>
    private static readonly HashSet<string> GenericNewsTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        // Editorial & publisher noise
        "breaking", "update", "updated", "exclusive", "watch", "video", "videos", "photo", "photos",
        "news", "report", "reports", "reported", "reporter", "story", "full", "list", "read", "details",
        "nigeria", "nigerian", "nigerians", "punch", "vanguard", "guardian", "channels", "daily", "post",
        "trust", "tribune", "thisday", "thecable", "premium", "times", "nairametrics", "businessday",
        "saharareporters", "linda", "ikeji", "blog", "television", "newspaper", "newspapers", "online",
        // Speech & attribution verbs
        "say", "says", "said", "tell", "tells", "told", "warn", "warns", "warned", "urge", "urges",
        "urged", "call", "calls", "called", "claim", "claims", "claimed", "reveal", "reveals", "revealed",
        "confirm", "confirms", "confirmed", "announce", "announces", "announced", "speak", "speaks",
        "spoke", "react", "reacts", "reacted", "question", "questions", "ask", "asks", "asked", "explain",
        "explains", "explained", "blame", "blames", "blamed", "state", "states", "federal", "government",
        "national", "public", "official", "officials", "agency", "agencies", "ministry", "minister",
        "governor", "president", "senate", "assembly", "house", "reps", "committee",
        // Generic crime/court/police boilerplate (frequently shared by unrelated local incidents)
        "police", "arrest", "arrests", "arrested", "suspect", "suspects", "suspected", "alleged",
        "allegedly", "court", "remand", "remands", "remanded", "dismiss", "dismisses", "dismissed",
        "case", "charge", "charges", "charged", "jail", "prison", "custody", "officer", "officers",
        "operative", "operatives", "command", "inspector", "kill", "kills", "killed", "killing",
        "death", "deaths", "die", "dies", "died", "dead", "murder", "murdered", "kidnap", "kidnaps",
        "kidnapped", "kidnapper", "kidnappers", "kidnapping", "abduct", "abducts", "abducted", "abduction",
        "rescue", "rescues", "rescued", "victim", "victims", "attack", "attacks", "attacked", "violence",
        "gunmen", "bandit", "bandits", "terrorist", "terrorists", "robbery", "robber", "robbers",
        "man", "men", "woman", "women", "boy", "boys", "girl", "girls", "child", "children", "youth",
        "youths", "person", "persons", "people", "community", "resident", "residents", "family",
        // Generic financial / corporate earnings boilerplate (shared across unrelated company 9M/Q3 reports)
        "profit", "profits", "revenue", "revenues", "pre", "tax", "pretax", "income", "earnings",
        "billion", "million", "trillion", "naira", "dollar", "dollars", "percent", "rise", "rises",
        "rose", "rising", "increase", "increases", "increased", "grow", "grows", "grew", "growth",
        "fall", "falls", "fell", "drop", "drops", "dropped", "double", "doubles", "doubled", "nearly",
        "high", "higher", "low", "lower", "quarter", "quarters", "month", "months", "annual", "half",
        "9m", "q1", "q2", "q3", "q4", "fy", "h1", "h2", "2024", "2025", "2026", "2027", "bank", "group",
        "company", "plc", "ltd", "limited", "financial", "market", "markets", "share", "shares"
    };

    private static readonly Dictionary<string, string> CanonicalSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["transnational"] = "transcorp",
        ["pms"] = "petrol",
        ["nnpcl"] = "nnpc",
        ["mpr"] = "monetary",
        ["fx"] = "forex",
        ["cbn"] = "cbn",
        ["nass"] = "senate",
        ["directs"] = "order",
        ["directed"] = "order",
        ["orders"] = "order",
        ["ordered"] = "order",
        ["commence"] = "begin",
        ["commences"] = "begin",
        ["commenced"] = "begin",
        ["commencing"] = "begin",
        ["begins"] = "begin",
        ["began"] = "begin",
        ["beginning"] = "begin",
        ["starts"] = "begin",
        ["started"] = "begin",
        ["starting"] = "begin",
        ["hikes"] = "hike",
        ["hiked"] = "hike",
        ["raises"] = "hike",
        ["raised"] = "hike",
        ["raising"] = "hike",
        ["slashes"] = "cut",
        ["slashed"] = "cut",
        ["reduces"] = "cut",
        ["reduced"] = "cut",
        ["cuts"] = "cut"
    };

    private static readonly Dictionary<string, string> LocationAliasToStateSlug = BuildLocationLookup();

    private static Dictionary<string, string> BuildLocationLookup()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in NigerianStateRegistry.AllStates)
        {
            // Skip "Niger" as a bare single-word state match if ambiguous with "Niger Delta" / "Niger Republic",
            // handled explicitly in ExtractMentionedStates.
            if (!state.Name.Equals("Niger", StringComparison.OrdinalIgnoreCase))
            {
                map[state.Name] = state.Slug;
            }
            map[state.Capital] = state.Slug;
            foreach (var city in state.MajorCities)
            {
                if (!city.Equals("Niger", StringComparison.OrdinalIgnoreCase))
                {
                    map[city] = state.Slug;
                }
            }
        }
        map["fct"] = "fct";
        map["abuja"] = "fct";
        return map;
    }

    [GeneratedRegex(@"\s*[-|–—:]\s*(Punch(\s*Newspapers?)?|Vanguard(\s*News?)?|TheCable|Daily\s*Post(\s*Nigeria)?|Premium\s*Times|Sahara\s*Reporters|The\s*Guardian(\s*Nigeria(\s*News)?)?|Leadership(\s*News?)?|ThisDay(\s*Live)?|Daily\s*Trust|Channels(\s*Television|\s*TV)?|Nairametrics|Tribune(\s*Online)?|Arise(\s*News|\s*TV)?|BusinessDay(\s*Nigeria)?|Linda\s*Ikeji('s)?\s*Blog)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BrandSuffixRegex();

    [GeneratedRegex(@"^\s*(\[?(BREAKING(\s*NEWS)?|JUST\s*IN|UPDATED?|EXCLUSIVE|WATCH|PHOTOS?|VIDEO|SPECIAL\s*REPORT|OPINION|EDITORIAL)\]?\s*[:\-\|]?\s*)+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EditorialPrefixRegex();

    public async Task<int> RunClusteringAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StoryClusteringService: Starting clustering cycle for last 72 hours...");
        var latestPublished = await _db.Articles
            .AsNoTracking()
            .MaxAsync(a => a.PublishedAt, cancellationToken);
        var referenceTime = latestPublished.HasValue && latestPublished.Value < DateTime.UtcNow
            ? latestPublished.Value
            : DateTime.UtcNow;
        var cutoff = referenceTime.AddHours(-72);

        // 1. Generate embeddings for any articles in the 72h window missing an embedding
        await EnsureEmbeddingsAsync(cutoff, cancellationToken);

        // 2. Load all articles and embeddings in the 72h window via direct join
        var articlePairs = await (
            from art in _db.Articles
            join emb in _db.ArticleEmbeddings on art.Id equals emb.ArticleId
            where art.PublishedAt >= cutoff
            orderby art.PublishedAt descending
            select new { Article = art, Embedding = emb }
        ).ToListAsync(cancellationToken);

        // Fallback to a 14-day window if recent 72h articles are sparse
        if (articlePairs.Count < 4)
        {
            cutoff = referenceTime.AddDays(-14);
            await EnsureEmbeddingsAsync(cutoff, cancellationToken);
            articlePairs = await (
                from art in _db.Articles
                join emb in _db.ArticleEmbeddings on art.Id equals emb.ArticleId
                where art.PublishedAt >= cutoff
                orderby art.PublishedAt descending
                select new { Article = art, Embedding = emb }
            ).ToListAsync(cancellationToken);
        }

        if (articlePairs.Count < 2)
        {
            _logger.LogInformation("StoryClusteringService: Insufficient articles with embeddings ({Count}) to form clusters.", articlePairs.Count);
            return 0;
        }

        var defaultThreshold = _embeddingProvider is LocalFallbackEmbeddingProvider
            ? 0.52f
            : 0.68f;
        var threshold = _config.GetValue<float>("Clustering:SimilarityThreshold", defaultThreshold);
        var minRawCosine = _embeddingProvider is LocalFallbackEmbeddingProvider ? 0.24f : 0.50f;

        _logger.LogInformation("Clustering {Count} articles with similarity threshold {Threshold}", articlePairs.Count, threshold);

        // 3. Cluster articles using anchored leader + centroid similarity and strict specific-entity verification
        var candidates = new List<ClusterCandidate>();

        foreach (var item in articlePairs)
        {
            var vector = item.Embedding.Vector;
            if (vector.Length == 0 || item.Article == null) continue;

            ClusterCandidate? bestCandidate = null;
            float bestSim = 0f;

            foreach (var cand in candidates)
            {
                var leadArticle = cand.Items[0].Article;
                var leadVector = cand.Items[0].Vector;

                // Anchor similarity to both the cluster centroid (60%) and the lead article (40%) to prevent centroid drift
                var centroidCosine = VectorMath.CosineSimilarity(vector, cand.Centroid);
                var leadCosine = VectorMath.CosineSimilarity(vector, leadVector);
                var rawCosine = (centroidCosine * 0.6f) + (leadCosine * 0.4f);

                if (rawCosine < minRawCosine)
                    continue;

                // Verify that the candidate article and lead article actually describe the same specific event
                if (!AreSameStoryEvent(item.Article, leadArticle, out var specificOverlapBoost))
                    continue;

                var sim = Math.Min(1.0f, rawCosine + specificOverlapBoost);
                if (sim >= threshold && sim > bestSim)
                {
                    bestSim = sim;
                    bestCandidate = cand;
                }
            }

            if (bestCandidate != null)
            {
                bestCandidate.Add(item.Article, vector, bestSim);
            }
            else
            {
                var newCand = new ClusterCandidate(item.Article, vector);
                candidates.Add(newCand);
            }
        }

        // 4. Filter candidates: Must have >= 2 articles AND >= 2 distinct sources
        var validCandidates = candidates
            .Where(c => c.Items.Count >= 2 && c.Sources.Count >= 2)
            .OrderByDescending(c => c.Sources.Count)
            .ThenByDescending(c => c.Items.Count)
            .ToList();

        _logger.LogInformation("Identified {ClusterCount} multi-source story clusters from {TotalCandidates} candidates",
            validCandidates.Count, candidates.Count);

        // 5. Persist clusters to database and prune stale/polluted clusters
        var existingClusters = await _db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .ToListAsync(cancellationToken);

        var claimedClusterIds = new HashSet<string>(StringComparer.Ordinal);
        int updatedClustersCount = 0;

        foreach (var cand in validCandidates)
        {
            var articleIds = cand.Items.Select(i => i.Article.Id).ToHashSet();
            var repArticle = cand.Items
                .OrderByDescending(i => !string.IsNullOrWhiteSpace(i.Article.ImageUrl))
                .ThenBy(i => i.Article.PublishedAt ?? DateTime.MaxValue)
                .First().Article;
            var cleanTitle = CleanClusterHeadline(repArticle.Title);

            // Match an unclaimed existing cluster that shares articles AND represents the same story event
            var matchedCluster = existingClusters
                .Where(sc => !claimedClusterIds.Contains(sc.Id))
                .Select(sc => new
                {
                    Cluster = sc,
                    OverlapCount = sc.ClusterArticles.Count(ca => articleIds.Contains(ca.ArticleId))
                })
                .Where(x => x.OverlapCount > 0 && AreArticlesRelatedForCluster(cleanTitle, null, x.Cluster.Title, x.Cluster.Summary))
                .OrderByDescending(x => x.OverlapCount)
                .Select(x => x.Cluster)
                .FirstOrDefault();

            if (matchedCluster == null)
            {
                var baseSlug = _slugService.GenerateSlug(cleanTitle);
                var slug = baseSlug;
                int suffix = 1;
                while (await _db.StoryClusters.AnyAsync(s => s.Slug == slug, cancellationToken) ||
                       existingClusters.Any(s => s.Slug == slug))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                matchedCluster = new StoryCluster
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = slug,
                    Title = cleanTitle,
                    Summary = cand.Items.Select(i => i.Article.Summary).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                              ?? cand.Items.Select(i => i.Article.Excerpt).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                    ImageUrl = CategoryImageMap.Resolve(
                        cand.Items.Select(i => i.Article.ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                        cand.Items.Select(i => i.Article.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))),
                    CreatedAt = cand.Items.Min(i => i.Article.PublishedAt ?? DateTime.UtcNow),
                    UpdatedAt = cand.Items.Max(i => i.Article.PublishedAt ?? DateTime.UtcNow),
                    ArticleCount = cand.Items.Count,
                    SourceCount = cand.Sources.Count
                };

                _db.StoryClusters.Add(matchedCluster);
                existingClusters.Add(matchedCluster);
            }
            else
            {
                matchedCluster.Title = cleanTitle;
                matchedCluster.Summary = cand.Items.Select(i => i.Article.Summary).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                                         ?? cand.Items.Select(i => i.Article.Excerpt).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                                         ?? matchedCluster.Summary;
                matchedCluster.ArticleCount = cand.Items.Count;
                matchedCluster.SourceCount = cand.Sources.Count;
                matchedCluster.UpdatedAt = cand.Items.Max(i => i.Article.PublishedAt ?? DateTime.UtcNow);
                if (string.IsNullOrEmpty(matchedCluster.ImageUrl))
                {
                    matchedCluster.ImageUrl = CategoryImageMap.Resolve(
                        cand.Items.Select(i => i.Article.ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                        cand.Items.Select(i => i.Article.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)));
                }
            }

            claimedClusterIds.Add(matchedCluster.Id);

            // Remove stale StoryClusterArticle links that no longer belong to this cluster
            var staleLinks = matchedCluster.ClusterArticles
                .Where(ca => !articleIds.Contains(ca.ArticleId))
                .ToList();
            foreach (var stale in staleLinks)
            {
                matchedCluster.ClusterArticles.Remove(stale);
                _db.StoryClusterArticles.Remove(stale);
            }

            // Upsert current valid links
            var currentLinks = matchedCluster.ClusterArticles.ToDictionary(ca => ca.ArticleId);
            foreach (var (article, _, sim) in cand.Items)
            {
                if (currentLinks.TryGetValue(article.Id, out var existingLink))
                {
                    existingLink.SimilarityScore = sim > 0 ? sim : 1.0f;
                }
                else
                {
                    var newLink = new StoryClusterArticle
                    {
                        ClusterId = matchedCluster.Id,
                        ArticleId = article.Id,
                        SimilarityScore = sim > 0 ? sim : 1.0f,
                        AddedAt = DateTime.UtcNow
                    };
                    matchedCluster.ClusterArticles.Add(newLink);
                    _db.StoryClusterArticles.Add(newLink);
                }
            }

            updatedClustersCount++;
        }

        // Remove unclaimed clusters in the active window whose articles were re-evaluated and did not form a valid cluster
        var obsoleteClusters = existingClusters
            .Where(sc => !claimedClusterIds.Contains(sc.Id))
            .ToList();
        if (obsoleteClusters.Count > 0)
        {
            foreach (var obsolete in obsoleteClusters)
            {
                if (obsolete.ClusterArticles.Count > 0)
                {
                    _db.StoryClusterArticles.RemoveRange(obsolete.ClusterArticles);
                }
                _db.StoryClusters.Remove(obsolete);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("StoryClusteringService: Successfully saved {Count} story clusters (pruned {PrunedCount} stale clusters)",
            updatedClustersCount, obsoleteClusters.Count);
        return updatedClustersCount;
    }

    public async Task<List<Article>> FindRelatedArticlesAsync(string articleId, int limit = 6, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return [];

        var effectiveLimit = Math.Clamp(limit, 1, 20);
        var cacheKey = $"related_articles_v2_{articleId}_{effectiveLimit}";

        if (_cache != null && _cache.TryGetValue(cacheKey, out List<Article>? cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        var result = await ComputeRelatedArticlesCoreAsync(articleId, effectiveLimit, cancellationToken);

        if (_cache != null && result.Count > 0)
        {
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });
        }

        return result;
    }

    private async Task<List<Article>> ComputeRelatedArticlesCoreAsync(string articleId, int limit, CancellationToken cancellationToken)
    {
        var targetArticle = await _db.Articles
            .AsNoTracking()
            .Include(a => a.ArticleTopics)
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);

        if (targetArticle == null) return [];

        var targetSpecificTokens = ExtractSpecificTokens(targetArticle.Title, targetArticle.Summary);

        var targetBlob = await _db.ArticleEmbeddings
            .AsNoTracking()
            .Where(ae => ae.ArticleId == articleId)
            .Select(ae => ae.VectorBlob)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetBlob is { Length: > 0 })
        {
            var targetVector = new float[targetBlob.Length / sizeof(float)];
            Buffer.BlockCopy(targetBlob, 0, targetVector, 0, targetBlob.Length);

            var windowCutoff = DateTime.UtcNow.AddDays(-14);

            var candidateVectors = await _db.ArticleEmbeddings
                .AsNoTracking()
                .Where(ae => ae.ArticleId != articleId && ae.Article != null && ae.Article.PublishedAt >= windowCutoff)
                .OrderByDescending(ae => ae.Article!.PublishedAt)
                .Take(500)
                .Select(ae => new
                {
                    ae.ArticleId,
                    ae.VectorBlob,
                    ae.Article!.Title,
                    ae.Article.Summary
                })
                .ToListAsync(cancellationToken);

            var scoredIds = new List<(string ArticleId, float Score)>();
            var scratchVector = new float[targetVector.Length];

            foreach (var cand in candidateVectors)
            {
                if (cand.VectorBlob is null || cand.VectorBlob.Length != targetBlob.Length)
                    continue;

                if (HasConflictingStates(targetArticle.Title, cand.Title))
                    continue;

                Buffer.BlockCopy(cand.VectorBlob, 0, scratchVector, 0, cand.VectorBlob.Length);
                var sim = VectorMath.CosineSimilarity(targetVector, scratchVector);

                var candTokens = ExtractSpecificTokens(cand.Title, cand.Summary);
                int specificOverlap = targetSpecificTokens.Count > 0 && candTokens.Count > 0
                    ? targetSpecificTokens.Count(candTokens.Contains)
                    : 0;

                var adjustedScore = sim + Math.Min(0.24f, specificOverlap * 0.08f);
                if (specificOverlap >= 1 && adjustedScore >= 0.42f)
                {
                    scoredIds.Add((cand.ArticleId, adjustedScore));
                }
            }

            var topMatches = scoredIds
                .OrderByDescending(s => s.Score)
                .Take(limit)
                .ToList();

            if (topMatches.Count > 0)
            {
                var topIds = topMatches.Select(m => m.ArticleId).ToList();
                var matchedArticles = await _db.Articles
                    .AsNoTracking()
                    .Where(a => topIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                var orderedArticles = topIds
                    .Where(matchedArticles.ContainsKey)
                    .Select(id => matchedArticles[id])
                    .ToList();

                if (orderedArticles.Count >= Math.Min(limit, 3))
                {
                    return orderedArticles;
                }
            }
        }

        // Fallback: Topic matching first, then Category
        var topicIds = targetArticle.ArticleTopics.Select(at => at.TopicId).ToList();

        var query = _db.Articles
            .AsNoTracking()
            .Where(a => a.Id != articleId);

        if (topicIds.Count > 0)
        {
            var topicMatched = await query
                .Where(a => a.ArticleTopics.Any(at => topicIds.Contains(at.TopicId)))
                .OrderByDescending(a => a.PublishedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

            if (topicMatched.Count >= limit)
            {
                return topicMatched;
            }

            var matchedIds = topicMatched.Select(m => m.Id).ToHashSet();
            var remaining = limit - topicMatched.Count;

            var categoryMatched = await query
                .Where(a => !matchedIds.Contains(a.Id) && a.Category == targetArticle.Category)
                .OrderByDescending(a => a.PublishedAt)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            topicMatched.AddRange(categoryMatched);
            return topicMatched;
        }

        return await query
            .Where(a => a.Category == targetArticle.Category)
            .OrderByDescending(a => a.PublishedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureEmbeddingsAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var unEmbedded = await _db.Articles
            .AsNoTracking()
            .Where(a => a.PublishedAt >= cutoff && !_db.ArticleEmbeddings.Any(ae => ae.ArticleId == a.Id))
            .OrderByDescending(a => a.PublishedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (unEmbedded.Count == 0) return;

        _logger.LogInformation("Generating embeddings for {Count} un-embedded articles...", unEmbedded.Count);

        var texts = unEmbedded.Select(a =>
        {
            var text = CleanClusterHeadline(a.Title);
            if (!string.IsNullOrWhiteSpace(a.Summary))
            {
                text += ". " + a.Summary;
            }
            return text;
        }).ToList();

        var vectors = await _embeddingProvider.GenerateEmbeddingsBatchAsync(texts, cancellationToken);

        for (int i = 0; i < unEmbedded.Count && i < vectors.Count; i++)
        {
            var art = unEmbedded[i];
            var vec = vectors[i];
            var embedding = new ArticleEmbedding
            {
                ArticleId = art.Id,
                Dimensions = vec.Length,
                Vector = vec,
                Model = _embeddingProvider is LocalFallbackEmbeddingProvider ? "local-murmur3-768" : "gemini-embedding-001",
                CreatedAt = DateTime.UtcNow
            };
            _db.ArticleEmbeddings.Add(embedding);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Evaluates whether two articles cover the same specific news event, preventing false clustering
    /// from generic crime/court/earnings boilerplate or conflicting Nigerian state locations.
    /// </summary>
    public static bool AreSameStoryEvent(Article articleA, Article articleB, out float keywordBoost)
    {
        keywordBoost = 0f;
        if (string.IsNullOrWhiteSpace(articleA.Title) || string.IsNullOrWhiteSpace(articleB.Title))
            return false;

        // 1. Disjoint Nigerian states in headlines (e.g. "in Kebbi" vs "in Plateau", "Kogi Assembly" vs "Lagos Assembly")
        if (HasConflictingStates(articleA.Title, articleB.Title))
            return false;

        // 2. Extract specific subject/entity tokens from titles (excluding stop words and generic reporting boilerplate)
        var titleTokensA = ExtractSpecificTokens(articleA.Title);
        var titleTokensB = ExtractSpecificTokens(articleB.Title);

        int titleOverlap = 0;
        foreach (var t in titleTokensA)
        {
            if (titleTokensB.Contains(t))
                titleOverlap++;
        }

        int minTitleCount = Math.Min(titleTokensA.Count, titleTokensB.Count);
        float titleOverlapRatio = minTitleCount > 0 ? (float)titleOverlap / minTitleCount : 0f;

        // Strong headline specific token match: >= 2 specific tokens and >= 34% of shorter headline's specific tokens
        if (titleOverlap >= 2 && titleOverlapRatio >= 0.34f)
        {
            keywordBoost = titleOverlap >= 3 && titleOverlapRatio >= 0.50f
                ? 0.26f
                : 0.16f;
            return true;
        }

        // Moderate headline match (at least 1 specific subject token in title) corroborated by summaries
        if (titleOverlap >= 1)
        {
            var fullTokensA = ExtractSpecificTokens(articleA.Title, articleA.Summary ?? articleA.Excerpt);
            var fullTokensB = ExtractSpecificTokens(articleB.Title, articleB.Summary ?? articleB.Excerpt);

            int fullOverlap = 0;
            foreach (var t in fullTokensA)
            {
                if (fullTokensB.Contains(t))
                    fullOverlap++;
            }

            int minFullCount = Math.Min(fullTokensA.Count, fullTokensB.Count);
            float fullOverlapRatio = minFullCount > 0 ? (float)fullOverlap / minFullCount : 0f;

            if (fullOverlap >= 3 && fullOverlapRatio >= 0.34f)
            {
                keywordBoost = 0.14f;
                return true;
            }
        }

        return false;
    }

    public static bool AreArticlesRelatedForCluster(string? titleA, string? summaryA, string? titleB, string? summaryB)
    {
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB))
            return false;

        var tempA = new Article { Title = titleA, Summary = summaryA };
        var tempB = new Article { Title = titleB, Summary = summaryB };
        return AreSameStoryEvent(tempA, tempB, out _);
    }

    public static string CleanClusterHeadline(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var clean = EditorialPrefixRegex().Replace(title.Trim(), string.Empty);
        clean = BrandSuffixRegex().Replace(clean, string.Empty);
        return clean.Trim();
    }

    public static bool HasConflictingStates(string? titleA, string? titleB)
    {
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB))
            return false;

        var statesA = ExtractMentionedStates(titleA);
        if (statesA.Count == 0) return false;

        var statesB = ExtractMentionedStates(titleB);
        if (statesB.Count == 0) return false;

        // If both headlines explicitly name Nigerian states/capitals and share zero overlap, they are distinct local events
        return !statesA.Overlaps(statesB);
    }

    private static HashSet<string> ExtractMentionedStates(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clean = CleanClusterHeadline(text);
        if (string.IsNullOrWhiteSpace(clean))
            return result;

        var words = new List<string>();
        var span = clean.AsSpan();
        int start = -1;
        for (int i = 0; i <= span.Length; i++)
        {
            bool isAlpha = i < span.Length && char.IsLetter(span[i]);
            if (isAlpha)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                words.Add(span.Slice(start, i - start).ToString());
                start = -1;
            }
        }

        for (int i = 0; i < words.Count; i++)
        {
            var w = words[i];
            var next = i + 1 < words.Count ? words[i + 1] : string.Empty;

            if (w.Equals("Niger", StringComparison.OrdinalIgnoreCase))
            {
                if (!next.Equals("Delta", StringComparison.OrdinalIgnoreCase) &&
                    !next.Equals("Republic", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add("niger");
                }
                continue;
            }

            if (!string.IsNullOrEmpty(next))
            {
                var twoWord = $"{w} {next}";
                if (LocationAliasToStateSlug.TryGetValue(twoWord, out var twoWordState))
                {
                    result.Add(twoWordState);
                    i++;
                    continue;
                }
            }

            if (LocationAliasToStateSlug.TryGetValue(w, out var stateSlug))
            {
                result.Add(stateSlug);
            }
        }

        return result;
    }

    private static HashSet<string> ExtractSpecificTokens(string? title, string? summary = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddTokensFromText(set, CleanClusterHeadline(title));
        if (!string.IsNullOrWhiteSpace(summary))
        {
            AddTokensFromText(set, summary);
        }
        return set;
    }

    private static void AddTokensFromText(HashSet<string> set, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var span = text.AsSpan();
        int start = -1;
        for (int i = 0; i <= span.Length; i++)
        {
            bool isAlphaNum = i < span.Length && char.IsLetterOrDigit(span[i]);
            if (isAlphaNum)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                int len = i - start;
                if (len >= 3)
                {
                    var raw = span.Slice(start, len).ToString().ToLowerInvariant();
                    if (!StopWords.Contains(raw) && !GenericNewsTokens.Contains(raw))
                    {
                        var token = NormalizeAndStemToken(raw);
                        if (token.Length >= 3 && !StopWords.Contains(token) && !GenericNewsTokens.Contains(token))
                        {
                            set.Add(token);
                        }
                    }
                }
                start = -1;
            }
        }
    }

    private static string NormalizeAndStemToken(string word)
    {
        // Strip leading Naira currency prefix (e.g. n111 -> 111, n50 -> 50)
        if (word.Length >= 2 && (word[0] == 'n' || word[0] == 'N') && char.IsDigit(word[1]))
            word = word[1..];

        if (CanonicalSynonyms.TryGetValue(word, out var syn))
            return syn;

        var stemmed = word;
        if (stemmed.Length > 5 && stemmed.EndsWith("ers", StringComparison.Ordinal))
            stemmed = stemmed[..^3];
        else if (stemmed.Length > 5 && stemmed.EndsWith("ies", StringComparison.Ordinal))
            stemmed = stemmed[..^3] + "y";
        else if (stemmed.Length > 5 && stemmed.EndsWith("ing", StringComparison.Ordinal))
            stemmed = stemmed[..^3];
        else if (stemmed.Length > 4 && stemmed.EndsWith("ed", StringComparison.Ordinal))
            stemmed = stemmed[..^2];
        else if (stemmed.Length > 4 && stemmed.EndsWith("es", StringComparison.Ordinal))
            stemmed = stemmed[..^2];
        else if (stemmed.Length > 3 && stemmed.EndsWith('s') && !stemmed.EndsWith("ss", StringComparison.Ordinal))
            stemmed = stemmed[..^1];

        if (CanonicalSynonyms.TryGetValue(stemmed, out var canonicalStem))
            return canonicalStem;

        return stemmed;
    }

    private sealed class ClusterCandidate
    {
        public List<(Article Article, float[] Vector, float Similarity)> Items { get; } = [];
        public float[] Centroid { get; private set; }
        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ClusterCandidate(Article initialArticle, float[] initialVector)
        {
            Centroid = (float[])initialVector.Clone();
            Items.Add((initialArticle, initialVector, 1.0f));
            if (!string.IsNullOrWhiteSpace(initialArticle.Source))
            {
                Sources.Add(initialArticle.Source);
            }
        }

        public void Add(Article article, float[] vector, float similarity)
        {
            Items.Add((article, vector, similarity));
            if (!string.IsNullOrWhiteSpace(article.Source))
            {
                Sources.Add(article.Source);
            }

            // Update Centroid: running average and re-normalize
            int count = Items.Count;
            for (int i = 0; i < Centroid.Length && i < vector.Length; i++)
            {
                Centroid[i] = (Centroid[i] * (count - 1) + vector[i]) / count;
            }
            VectorMath.Normalize(Centroid);
        }
    }
}
