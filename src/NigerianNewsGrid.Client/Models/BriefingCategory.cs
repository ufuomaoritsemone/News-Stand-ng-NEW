namespace NigerianNewsGrid.Client.Models;

/// <summary>
/// A grouping of top articles within a news category (e.g. "Politics", "Sports").
/// Immutable record for deserialization — per C# best practices, DTOs should be records.
/// </summary>
public sealed record BriefingCategory
{
    public string Category { get; init; } = "General";
    public List<BriefingItem> Top { get; init; } = [];
}

public sealed record BriefingItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? Source { get; init; }
    public string? Category { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Language { get; set; }
    public string? VoiceName { get; set; }
    public string? AudioUrl { get; set; }
    public string? Author { get; init; }
    public string? ContentType { get; init; } = "News";
    public string? Slug { get; init; }

    // Direct In-House Sponsorship Engine Fields
    public bool IsSponsored { get; init; } = false;
    public string? SponsorName { get; init; }
    public string? SponsorUrl { get; init; }
    public bool IsPinned { get; init; } = false;
    public int? TargetPosition { get; init; }
    public int PriorityWeight { get; init; } = 1;
    public string PlacementZone { get; init; } = "MainPage_InFeed";
    public string TargetPlatform { get; init; } = "All";
    public bool IsAdMobPlaceholder { get; init; } = false;
    public bool IsSpecialPlacement => TargetPosition is >= 1 and <= 3;

    // AI Summary & Executive Briefing Fields for Mobile
    private bool _hasAiSummary;
    private string? _aiSummary;
    private string? _aiSummaryEngine;
    private List<string> _keyTakeaways = [];
    private List<string> _contextAndBackground = [];
    private List<string> _keyFiguresAndEntities = [];
    private string? _outlookOrNextSteps;

    public bool HasAiSummary
    {
        get => _hasAiSummary || !string.IsNullOrWhiteSpace(AiSummary) || Enrichment != null;
        init => _hasAiSummary = value;
    }

    public string? AiSummary
    {
        get => !string.IsNullOrWhiteSpace(_aiSummary)
            ? _aiSummary
            : (Enrichment?.FullSummary?.ExecutiveOverview ?? Enrichment?.KeyTakeaways?.FirstOrDefault());
        init => _aiSummary = value;
    }

    public string? AiSummaryEngine
    {
        get => !string.IsNullOrWhiteSpace(_aiSummaryEngine) ? _aiSummaryEngine : Enrichment?.Engine;
        init => _aiSummaryEngine = value;
    }

    public List<string> KeyTakeaways
    {
        get => _keyTakeaways.Count > 0 ? _keyTakeaways : (Enrichment?.KeyTakeaways ?? []);
        init => _keyTakeaways = value ?? [];
    }

    public List<string> ContextAndBackground
    {
        get => _contextAndBackground.Count > 0 ? _contextAndBackground : (Enrichment?.FullSummary?.ContextAndBackground ?? []);
        init => _contextAndBackground = value ?? [];
    }

    public List<string> KeyFiguresAndEntities
    {
        get => _keyFiguresAndEntities.Count > 0 ? _keyFiguresAndEntities : (Enrichment?.FullSummary?.KeyFiguresAndEntities ?? []);
        init => _keyFiguresAndEntities = value ?? [];
    }

    public string? OutlookOrNextSteps
    {
        get => !string.IsNullOrWhiteSpace(_outlookOrNextSteps) ? _outlookOrNextSteps : Enrichment?.FullSummary?.OutlookOrNextSteps;
        init => _outlookOrNextSteps = value;
    }

    public ClientArticleEnrichmentDto? Enrichment { get; init; }
    public int CommentCount { get; init; } = 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeAgoText => PublishedAt.HasValue
        ? FormatTimeAgo(PublishedAt.Value)
        : string.Empty;

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return utcTime.ToLocalTime().ToString("MMM d");
    }
}

public sealed record BriefingAvailabilityResult
{
    public bool Available { get; init; }
    public int ArticleCount { get; init; }
    public DateTime TimestampUtc { get; init; }
}

/// <summary>
/// Discovery DTO returned by GET /api/v1/audio/briefings/latest.
/// Informs mobile and web clients whether a neural audio broadcast is available for streaming.
/// </summary>
public sealed record AudioBriefingMetadata
{
    public string Cycle { get; init; } = string.Empty;
    public string TimeOfDay { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string StreamUrl { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string Provider { get; init; } = string.Empty;
    public bool Available { get; init; }
}

/// <summary>
/// Client-side DTO for structured AI enrichment attached to an article.
/// </summary>
public sealed record ClientArticleEnrichmentDto
{
    public List<string> KeyTakeaways { get; init; } = [];
    public ClientFullSummaryDto? FullSummary { get; init; }
    public string? Engine { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Client-side DTO for full structured AI executive summary returned by /api/v1/articles/{id}/ai-summary.
/// </summary>
public sealed record ClientFullSummaryDto
{
    public string ExecutiveOverview { get; init; } = string.Empty;
    public List<string> ContextAndBackground { get; init; } = [];
    public List<string> KeyFiguresAndEntities { get; init; } = [];
    public string OutlookOrNextSteps { get; init; } = string.Empty;
    public int ReadingTimeSeconds { get; init; } = 45;
}

