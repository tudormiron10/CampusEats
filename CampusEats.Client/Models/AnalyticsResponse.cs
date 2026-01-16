namespace CampusEats.Client.Models;

public record AnalyticsResponse(
    List<TimeSeriesDataPoint> TimeSeries,
    AnalyticsSummary Summary,
    PerformanceMetrics Performance,
    ItemInsights Items,
    RevenueInsights Revenue,
    CustomerInsights Customers
);

public record TimeSeriesDataPoint(
    DateTime Period,
    int OrderCount,
    decimal Revenue
);

public record AnalyticsSummary(
    int TotalOrders,
    decimal TotalRevenue,
    decimal AverageOrderValue,
    int TotalItemsSold,
    decimal? ChangeVsPrevious
);

public record PerformanceMetrics(
    PeakHour? PeakHour,
    BestDay? BestDayOfWeek,
    decimal CompletionRate,
    int CompletedOrders,
    int CancelledOrders,
    decimal AvgItemsPerOrder
);

public record PeakHour(int Hour, int OrderCount);
public record BestDay(string DayName, decimal AvgOrders);

public record ItemInsights(
    ItemInsight? MostSold,
    ItemInsight? LeastSold,
    ItemInsight? MorningBestseller,
    ItemInsight? EveningBestseller
);

public record ItemInsight(Guid? MenuItemId, string Name, int Quantity);

public record RevenueInsights(
    List<TimeSeriesDataPoint> RevenueByHour,
    List<TopRevenueItem> TopItemsByRevenue,
    List<CategoryRevenue> CategoryBreakdown
);

public record TopRevenueItem(Guid? MenuItemId, string Name, decimal Revenue, int Quantity);
public record CategoryRevenue(Guid CategoryId, string CategoryName, decimal Revenue, decimal Percentage);

public record CustomerInsights(
    int UniqueCustomers,
    decimal OrdersPerCustomer,
    int NewCustomers,
    int ReturningCustomers,
    decimal NewCustomerPercentage
);