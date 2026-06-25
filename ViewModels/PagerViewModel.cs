namespace DailyPilot.ViewModels;

/// <summary>Drives the reusable _Pager partial.</summary>
public class PagerViewModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Action { get; set; } = "Index";
    public string Controller { get; set; } = "Home";

    /// <summary>Extra query-string values to preserve across pages (e.g. a search term).</summary>
    public Dictionary<string, string?> RouteValues { get; set; } = new();
}
