namespace InventoryControl.ViewModels;

public class LogViewerViewModel
{
    public string? SelectedDate { get; set; }
    public string? LevelFilter { get; set; }
    public string? SearchTerm { get; set; }
    public List<LogLine> Lines { get; set; } = new();
    public List<string> AvailableDates { get; set; } = new();
    public bool LogDirExists { get; set; }
    public int TotalMatchedLines { get; set; }
    public bool IsTruncated { get; set; }
}

public class LogLine
{
    public string Raw { get; set; } = "";

    /// <summary>INF, WRN, ERR, DBG, FTL, or empty for continuation lines.</summary>
    public string Level { get; set; } = "INF";
    public bool IsContinuation { get; set; }
}
