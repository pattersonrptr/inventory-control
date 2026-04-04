namespace ControleEstoque.ViewModels;

public class MonthlyReportViewModel
{
    public int Month { get; set; }
    public int Year { get; set; }

    public string MonthName =>
        new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public IEnumerable<MonthlyReportItem> Items { get; set; } = Enumerable.Empty<MonthlyReportItem>();

    public int TotalEntries => Items.Sum(i => i.TotalEntries);
    public int TotalExits => Items.Sum(i => i.TotalExits);
}

public class MonthlyReportItem
{
    public string ProductName { get; set; } = string.Empty;
    public int TotalEntries { get; set; }
    public int TotalExits { get; set; }
    public int Balance => TotalEntries - TotalExits;
}
