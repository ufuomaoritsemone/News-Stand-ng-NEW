namespace NigerianNewGrid.Models;

/// <summary>
/// Display model for related stories (articles, video broadcasts, social updates) shown in ArticleWebPage drawer.
/// Extracted from ArticleWebPage.xaml.cs (Issue #19).
/// </summary>
public class RelatedStoryDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Type { get; set; } = "Article"; // Article, Video, Social
    public string Url { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? Summary { get; set; }
    public bool HasAiSummary { get; set; }
    public string? AiSummary { get; set; }
    public string? AiSummaryEngine { get; set; }
    public IReadOnlyList<string> KeyTakeaways { get; set; } = [];
    public IReadOnlyList<string> ContextAndBackground { get; set; } = [];
    public IReadOnlyList<string> KeyFiguresAndEntities { get; set; } = [];
    public string? OutlookOrNextSteps { get; set; }
    public bool IsVideo => Type == "Video";
    public bool IsSocial => Type == "Social";
    public string TimeAgo { get; set; } = "recently";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}
