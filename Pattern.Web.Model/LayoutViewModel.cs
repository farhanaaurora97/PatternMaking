namespace Pattern.Web.Model;

/// <summary>Passed to _Layout.cshtml via ViewData to drive sidebar active state and topbar.</summary>
public class LayoutViewModel
{
    public string ActiveController { get; set; } = "Home";
    public string PageTitle { get; set; } = "Dashboard";
    public string CurrentStyle { get; set; } = "skinny";
    public int PendingBadgeCount { get; set; } = 0;

    /// <summary>When set (e.g. Canvas / Pieces / Export), sidebar links preserve this pattern in query strings.</summary>
    public int? CurrentPatternId { get; set; }
}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         