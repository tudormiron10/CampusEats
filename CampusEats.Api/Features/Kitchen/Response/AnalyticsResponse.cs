namespace CampusEats.Api.Features.Kitchen.Response;

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

// Row 1: Summary Stats
public record AnalyticsSummary(
    int TotalOrders,
    decimal TotalRevenue,
    decimal AverageOrderValue,
    int TotalItemsSold,
    decimal? ChangeVsPrevious // % change vs previous period
);

// Row 3: Performance Metrics
public record PerformanceMetrics(
    PeakHour? PeakHour,
    BestDay? BestDayOfWeek,
    decimal CompletionRate, // % completed vs total
    int CompletedOrders,
    int CancelledOrders,
    decimal AvgItemsPerOrder
);

public record PeakHour(int Hour, int OrderCount);
public record BestDay(string DayName, decimal AvgOrders);

// Row 4: Item Insights
public record ItemInsights(
    ItemInsight? MostSold,
    ItemInsight? LeastSold,
    ItemInsight? MorningBestseller, // 6:00-12:00
    ItemInsight? EveningBestseller  // 17:00-22:00
);

public record ItemInsight(Guid? MenuItemId, string Name, int Quantity);

// Row 5: Revenue Insights
public record RevenueInsights(
    List<TimeSeriesDataPoint> RevenueByHour,
    List<TopRevenueItem> TopItemsByRevenue,
    List<CategoryRevenue> CategoryBreakdown
);

public record TopRevenueItem(Guid? MenuItemId, string Name, decimal Revenue, int Quantity);
public record CategoryRevenue(Guid CategoryId, string CategoryName, decimal Revenue, decimal Percentage);

// Row 6: Customer Insights
public record CustomerInsights(
    int UniqueCustomers,
    decimal OrdersPerCustomer,
    int NewCustomers,
    int ReturningCustomers,
    decimal NewCustomerPercentage
);