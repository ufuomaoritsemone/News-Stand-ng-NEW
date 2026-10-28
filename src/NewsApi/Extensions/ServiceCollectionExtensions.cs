using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Net;
using System.Threading.RateLimiting;

namespace NewsApi.Extensions;

/// <summary>
/// Extension methods to keep Program.cs focused on pipeline configuration only.
/// Each method encapsulates a distinct concern (SRP, Fix #14).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all application-layer services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddHttpClient("ImageProxy", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 (compatible; NewsStandNG-ImageProxy/1.0)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            }
        });

        services.AddHttpClient("FxScraperClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36 (compatible; NewsStandNG-FX/1.0)");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,application/json,*/*;q=0.8");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            }
        });

        // Core application services with interfaces (DIP, Fixes #19, #20, #21)
        services.AddScoped<IFxRatesScraperService, FxRatesScraperService>();
        services.AddScoped<IRelatedContentService, RelatedContentService>();
        services.AddScoped<IYouTubeFeedService,    YouTubeFeedService>();
        services.AddScoped<ISocialFeedService,     SocialFeedService>();
        services.AddScoped<IFeedbackService,       FeedbackService>();
        services.AddScoped<IArticleSearchService,   ArticleSearchService>();
        services.AddSingleton<ICategorizerTrainingService, CategorizerTrainingService>();
        services.AddSingleton<ISlugService,         SlugService>();
        services.AddScoped<IArticleSeoBackfillService, ArticleSeoBackfillService>();
        services.AddScoped<IBriefingService,        BriefingService>();
        services.AddScoped<IArticleIngestionService, ArticleIngestionService>();
        services.AddSingleton<IndexNowBackgroundService>();
        services.AddSingleton<IIndexNowService>(sp => sp.GetRequiredService<IndexNowBackgroundService>());
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();

        // Embeddings and Story Clustering (Phase 6)
        services.AddSingleton<LocalFallbackEmbeddingProvider>();
        services.AddSingleton<IEmbeddingProvider, GeminiEmbeddingProvider>();
        services.AddScoped<IStoryClusteringService, StoryClusteringService>();

        // AI / ML Article Enrichment & Audit Logging (Phase 7)
        services.AddSingleton<IAiAuditLogger, AiAuditLogger>();
        services.AddScoped<IArticleEnrichmentService, ArticleEnrichmentService>();

        // Search Console API & Crawl Diagnostics (Phase 9)
        services.AddScoped<ISearchConsoleService, SearchConsoleService>();

        // Continuous SEO Monitoring & ML.NET Anomaly Feedback Loop (Phase 10)
        services.AddSingleton<ISeoAnomalyEngine, SeoAnomalyEngine>();
        services.AddScoped<ISeoFeedbackLoopService, SeoFeedbackLoopService>();
        services.AddSingleton<SeoMonitoringBackgroundService>();

        // Background services
        services.AddHostedService<BackgroundMediaSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<IndexNowBackgroundService>());
        services.AddHostedService<StoryClusteringBackgroundService>();
        services.AddHostedService<ArticleEnrichmentBackgroundService>();
        services.AddHostedService<MarketDataBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<SeoMonitoringBackgroundService>());

        return services;
    }

    /// <summary>Registers infrastructure-level services (DB, health checks).</summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DB initializer & data seeder (Fix #13, Priority 2.3)
        services.AddScoped<IDataSeeder, DataSeeder>();
        services.AddScoped<IDbInitializer, DbInitializer>();

        // Database context — provider auto-detection via config key or connection string signature
        var connectionString = configuration.GetConnectionString("NewsDb")
                            ?? configuration.GetConnectionString("DefaultConnection")
                            ?? "Data Source=news.db";

        var dbProvider = configuration["DbProvider"] ?? "sqlite";
        var isPostgres = dbProvider.StartsWith("postgre", StringComparison.OrdinalIgnoreCase) ||
                         connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                         connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<NewsDbContext>(options =>
        {
            if (isPostgres)
            {
                options.UseNpgsql(connectionString, b =>
                {
                    b.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                    b.CommandTimeout(30);
                });
            }
            else
            {
                options.UseSqlite(connectionString, b =>
                {
                    b.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName);
                    b.CommandTimeout(30);
                });
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        // Health checks
        services.AddHealthChecks()
                .AddCheck<NewsApi.Health.DatabaseHealthCheck>("db_health_check");

        return services;
    }

    /// <summary>
    /// Adds rate limiting — sliding window policy per IP for API endpoints (Fix #4).
    /// Internal workers (X-Api-Key) and health probes bypass the limiter.
    /// In Development, local loopback requests bypass to allow multi-service local testing.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        IHostEnvironment? environment = null)
    {
        var permitLimit = configuration?.GetValue<int>("RateLimiting:PermitLimit") ?? 600;
        if (permitLimit <= 0)
        {
            permitLimit = 600;
        }

        var configuredApiKey = configuration?["ApiKey"]
            ?? configuration?["API_KEY"]
            ?? Environment.GetEnvironmentVariable("API_KEY");

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                // Never throttle container or cluster health probes
                if (ctx.Request.Path.StartsWithSegments("/health") || ctx.Request.Path.StartsWithSegments("/healthz"))
                {
                    return RateLimitPartition.GetNoLimiter("health_probe");
                }

                // Allow authorized internal services (Scraper, Admin, Worker) or stress testing to bypass if key matches
                if (!string.IsNullOrEmpty(configuredApiKey))
                {
                    if ((ctx.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) && string.Equals(apiKey, configuredApiKey, StringComparison.Ordinal)) ||
                        (ctx.Request.Headers.TryGetValue("X-Bypass-Rate-Limit", out var bypassKey) && string.Equals(bypassKey, configuredApiKey, StringComparison.Ordinal)))
                    {
                        return RateLimitPartition.GetNoLimiter("authorized_bypass");
                    }
                }

                // In Development mode, bypass rate limiting for local loopback connections so multi-service local stacks do not choke
                var remoteIp = ctx.Connection.RemoteIpAddress;
                if ((environment?.IsDevelopment() ?? false) && remoteIp != null && IPAddress.IsLoopback(remoteIp))
                {
                    return RateLimitPartition.GetNoLimiter("dev_loopback");
                }

                // Extract client IP: only trust X-Forwarded-For when the immediate peer is loopback or private Docker network
                string partitionKey = "unknown";
                bool isTrustedProxy = remoteIp == null || IPAddress.IsLoopback(remoteIp) || IsPrivateNetworkIp(remoteIp);
                if (isTrustedProxy &&
                    ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd) &&
                    !string.IsNullOrWhiteSpace(fwd))
                {
                    partitionKey = fwd.ToString().Split(',')[0].Trim();
                }
                else if (remoteIp != null)
                {
                    partitionKey = remoteIp.ToString();
                }

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit          = permitLimit,
                        Window               = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow    = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 0
                    });
            });

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsync(
                    """{"error":{"code":"RATE_LIMIT_EXCEEDED","message":"Too many requests. Please slow down."}}""", ct);
            };
        });

        return services;
    }

    /// <summary>
    /// Adds CORS with a named allowlist from configuration (Fixes #2).
    /// Falls back to localhost-only in development.
    /// </summary>
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowConfiguredOrigins", policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("AllowedOrigins")
                    .Get<string[]>();

                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
                else
                {
                    // Dev fallback — localhost only
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry tracing and metrics (Fix #38).
    /// Console exporter is gated to Development or explicit opt-in to avoid synchronous stdout overhead in production.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        IHostEnvironment? environment = null)
    {
        var enableConsoleExporter = (environment?.IsDevelopment() ?? false)
            || (configuration?.GetValue<bool>("Observability:EnableConsoleExporter") ?? false);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName:    "news-api",
                serviceVersion: "1.0.0"))
            .WithTracing(tracer =>
            {
                tracer
                    .AddAspNetCoreInstrumentation(opts => { opts.RecordException = true; })
                    .AddHttpClientInstrumentation();

                if (enableConsoleExporter)
                {
                    tracer.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (enableConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }

    private static bool IsPrivateNetworkIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   bytes[0] == 127;
        }

        return ip.IsIPv6SiteLocal || ip.IsIPv6LinkLocal;
    }
}
