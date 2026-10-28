using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Data;
using NewsApi.Infrastructure;
using Xunit;

namespace NewsApiClient.Tests;

public class DbInitializerTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;

    public DbInitializerTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"news_init_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_testDbPath}";
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
    }

    private NewsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseSqlite(_connectionString, b => b.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName))
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task DbInitializer_FreshDatabase_RunsMigrationsAndSeeds()
    {
        using var db = CreateContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync();

        // Verify tables and seeds created
        var articleCount = await db.Articles.CountAsync();
        var sourceCount = await db.Sources.CountAsync();

        Assert.True(articleCount > 0, "Expected seeded articles in fresh database");
        Assert.True(sourceCount > 0, "Expected seeded sources in fresh database");
    }

    [Fact]
    public async Task DbInitializer_LegacyDatabaseWithoutMigrationHistory_BaseliningSucceeds()
    {
        // 1. Create a legacy database manually with EnsureCreated (simulating pre-migration DB)
        {
            using var legacyDb = CreateContext();
            await legacyDb.Database.EnsureCreatedAsync();

            // Verify __EFMigrationsHistory does not exist or has 0 rows
            var conn = legacyDb.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DROP TABLE IF EXISTS \"__EFMigrationsHistory\";";
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Run DbInitializer on the legacy database — should baseline and succeed without throwing
        using var db = CreateContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);

        var ex = await Record.ExceptionAsync(() => initializer.InitializeAsync());
        Assert.Null(ex);

        // Verify the database is healthy and readable
        var sources = await db.Sources.ToListAsync();
        Assert.NotEmpty(sources);
    }

    [Fact]
    public async Task NewsDbContext_DateTimeValueConverters_NormalizeUnspecifiedAndLocalToUtc()
    {
        using var db = CreateContext();
        var initializer = new DbInitializer(db, NullLogger<DbInitializer>.Instance);
        await initializer.InitializeAsync();

        var unspecifiedDate = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Unspecified);
        var articleId = Guid.NewGuid().ToString("N");

        db.Articles.Add(new NewsApi.Models.Article
        {
            Id = articleId,
            Title = "UTC ValueConverter Verification Headline",
            Slug = $"utc-converter-test-{articleId[..8]}",
            PublishedAt = unspecifiedDate,
            UpdatedAt = unspecifiedDate
        });
        await db.SaveChangesAsync();

        var loaded = await db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.PublishedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.PublishedAt.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.UpdatedAt!.Value.Kind);
    }
}
