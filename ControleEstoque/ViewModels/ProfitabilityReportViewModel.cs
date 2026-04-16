namespace ControleEstoque.ViewModels;

public class ProfitabilityReportViewModel
{
    public int Month { get; set; }
    public int Year { get; set; }

    public string MonthName =>
        new DateTime(Year, Month, 1).ToString("MMMM yyyy");

    public IReadOnlyList<ProfitabilityItem> Items { get; set; } = [];

    public decimal TotalRevenue => Items.Sum(i => i.Revenue);
    public decimal TotalCost => Items.Sum(i => i.Cost);
    public decimal TotalProfit => Items.Sum(i => i.Profit);
}

public class ProfitabilityItem
{
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal Revenue => QuantitySold * SellingPrice;
    public decimal Cost => QuantitySold * CostPrice;
    public decimal Profit => Revenue - Cost;
    public decimal MarginPercent => Revenue > 0 ? (Profit / Revenue) * 100 : 0;
}
