using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Kitchen.Response;
using CampusEats.Api.Infrastructure.Persistence.Entities;

using OrderEntity = CampusEats.Api.Infrastructure.Persistence.Entities.Order;

namespace CampusEats.Api.Features.Kitchen.Handler;

public class GetAnalyticsHandler : IRequestHandler<GetAnalyticsRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetAnalyticsHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetAnalyticsRequest request, CancellationToken cancellationToken)
    {
        var startDate = request.StartDate.Kind == DateTimeKind.Utc
            ? request.StartDate
            : DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc);
        var endDate = request.EndDate.Kind == DateTimeKind.Utc
            ? request.EndDate
            : DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc);

        // Get all orders in the period
        var ordersInPeriod = await _context.Orders
            .Where(o => o.OrderDate >= startDate && o.OrderDate < endDate)
            .Include(o => o.Items)
                .ThenInclude(oi => oi.MenuItem)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Calculate previous period for comparison
        var periodLength = endDate - startDate;
        var prevStartDate = startDate - periodLength;
        var prevEndDate = startDate;

        var prevOrders = await _context.Orders
            .Where(o => o.OrderDate >= prevStartDate && o.OrderDate < prevEndDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Time series data
        var timeSeries = BuildTimeSeries(ordersInPeriod, startDate, endDate, request.GroupBy);

        // Summary stats
        var summary = BuildSummary(ordersInPeriod, prevOrders);

        // Performance metrics
        var performance = BuildPerformanceMetrics(ordersInPeriod);

        // Item insights
        var itemInsights = BuildItemInsights(ordersInPeriod);

        // Revenue insights
        var revenueInsights = BuildRevenueInsights(ordersInPeriod);

        // Customer insights - need to check order history before the period
        var customerInsights = await BuildCustomerInsightsAsync(ordersInPeriod, startDate, cancellationToken);

        var response = new AnalyticsResponse(
            timeSeries,
            summary,
            performance,
            itemInsights,
            revenueInsights,
            customerInsights
        );

        return Results.Ok(response);
    }

    private static List<TimeSeriesDataPoint> BuildTimeSeries(List<OrderEntity> orders, DateTime start, DateTime end, string groupBy)
    {
        var result = new List<TimeSeriesDataPoint>();

        switch (groupBy.ToLower())
        {
            case "hour":
                // Generate all hours in the range
                for (var hour = start; hour < end; hour = hour.AddHours(1))
                {
                    var hourEnd = hour.AddHours(1);
                    var hourOrders = orders.Where(o => o.OrderDate >= hour && o.OrderDate < hourEnd).ToList();
                    result.Add(new TimeSeriesDataPoint(
                        hour,
                        hourOrders.Count,
                        hourOrders.Sum(o => o.TotalAmount)
                    ));
                }
                break;

            case "day":
                for (var day = start.Date; day < end.Date; day = day.AddDays(1))
                {
                    var dayEnd = day.AddDays(1);
                    var dayOrders = orders.Where(o => o.OrderDate >= day && o.OrderDate < dayEnd).ToList();
                    result.Add(new TimeSeriesDataPoint(
                        day,
                        dayOrders.Count,
                        dayOrders.Sum(o => o.TotalAmount)
                    ));
                }
                break;

            case "month":
                for (var month = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc); month < end; month = month.AddMonths(1))
                {
                    var monthEnd = month.AddMonths(1);
                    var monthOrders = orders.Where(o => o.OrderDate >= month && o.OrderDate < monthEnd).ToList();
                    result.Add(new TimeSeriesDataPoint(
                        month,
                        monthOrders.Count,
                        monthOrders.Sum(o => o.TotalAmount)
                    ));
                }
                break;
        }

        return result;
    }

    private static AnalyticsSummary BuildSummary(List<OrderEntity> orders, List<OrderEntity> prevOrders)
    {
        var totalOrders = orders.Count;
        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        var totalItemsSold = orders.SelectMany(o => o.Items).Sum(i => i.Quantity);

        // Calculate % change vs previous period
        decimal? changeVsPrevious = null;
        if (prevOrders.Count > 0)
        {
            var prevTotal = prevOrders.Count;
            changeVsPrevious = ((decimal)(totalOrders - prevTotal) / prevTotal) * 100;
        }

        return new AnalyticsSummary(
            totalOrders,
            totalRevenue,
            avgOrderValue,
            totalItemsSold,
            changeVsPrevious
        );
    }

    private static PerformanceMetrics BuildPerformanceMetrics(List<OrderEntity> orders)
    {
        // Peak hour
        PeakHour? peakHour = null;
        if (orders.Count > 0)
        {
            var hourGroups = orders
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .First();

            if (hourGroups != null)
            {
                peakHour = new PeakHour(hourGroups.Hour, hourGroups.Count);
            }
        }

        // Best day of week
        BestDay? bestDay = null;
        if (orders.Count > 0)
        {
            var dayGroups = orders
                .GroupBy(o => o.OrderDate.DayOfWeek)
                .Select(g => new { Day = g.Key, AvgOrders = (decimal)g.Count() })
                .OrderByDescending(g => g.AvgOrders)
                .First();

            if (dayGroups != null)
            {
                bestDay = new BestDay(dayGroups.Day.ToString(), dayGroups.AvgOrders);
            }
        }

        // Completion rate
        var completedOrders = orders.Count(o => o.Status == OrderStatus.Completed);
        var cancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled);
        var totalFinished = completedOrders + cancelledOrders;
        var completionRate = totalFinished > 0 ? (decimal)completedOrders / totalFinished * 100 : 100m;

        // Avg items per order
        var totalItems = orders.SelectMany(o => o.Items).Sum(i => i.Quantity);
        var avgItemsPerOrder = orders.Count > 0 ? (decimal)totalItems / orders.Count : 0;

        return new PerformanceMetrics(
            peakHour,
            bestDay,
            completionRate,
            completedOrders,
            cancelledOrders,
            avgItemsPerOrder
        );
    }

    private static ItemInsights BuildItemInsights(List<OrderEntity> orders)
    {
        var allItems = orders
            .SelectMany(o => o.Items.Select(i => new
            {
                i.MenuItemId,
                Name = i.MenuItem?.Name ?? "Unknown",
                i.Quantity,
                OrderHour = o.OrderDate.Hour
            }))
            .ToList();

        // Group by menu item
        var itemGroups = allItems
            .GroupBy(i => new { i.MenuItemId, i.Name })
            .Select(g => new
            {
                g.Key.MenuItemId,
                g.Key.Name,
                TotalQuantity = g.Sum(x => x.Quantity),
                MorningQty = g.Where(x => x.OrderHour >= 6 && x.OrderHour < 12).Sum(x => x.Quantity),
                EveningQty = g.Where(x => x.OrderHour >= 17 && x.OrderHour < 22).Sum(x => x.Quantity)
            })
            .ToList();

        ItemInsight? mostSold = null;
        ItemInsight? leastSold = null;
        ItemInsight? morningBest = null;
        ItemInsight? eveningBest = null;

        if (itemGroups.Count > 0)
        {
            var most = itemGroups.OrderByDescending(g => g.TotalQuantity).First();
            mostSold = new ItemInsight(most.MenuItemId, most.Name, most.TotalQuantity);

            var least = itemGroups.OrderBy(g => g.TotalQuantity).First();
            leastSold = new ItemInsight(least.MenuItemId, least.Name, least.TotalQuantity);

            var morningItems = itemGroups.Where(g => g.MorningQty > 0).ToList();
            if (morningItems.Count > 0)
            {
                var mBest = morningItems.OrderByDescending(g => g.MorningQty).First();
                morningBest = new ItemInsight(mBest.MenuItemId, mBest.Name, mBest.MorningQty);
            }

            var eveningItems = itemGroups.Where(g => g.EveningQty > 0).ToList();
            if (eveningItems.Count > 0)
            {
                var eBest = eveningItems.OrderByDescending(g => g.EveningQty).First();
                eveningBest = new ItemInsight(eBest.MenuItemId, eBest.Name, eBest.EveningQty);
            }
        }

        return new ItemInsights(mostSold, leastSold, morningBest, eveningBest);
    }

    private static RevenueInsights BuildRevenueInsights(List<OrderEntity> orders)
    {
        // Revenue by hour (24 hours)
        var revenueByHour = Enumerable.Range(0, 24)
            .Select(h =>
            {
                var hourOrders = orders.Where(o => o.OrderDate.Hour == h).ToList();
                return new TimeSeriesDataPoint(
                    DateTime.Today.AddHours(h),
                    hourOrders.Count,
                    hourOrders.Sum(o => o.TotalAmount)
                );
            })
            .ToList();

        // Top 5 items by revenue
        var topItems = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.MenuItemId, Name = i.MenuItem?.Name ?? "Unknown" })
            .Select(g => new TopRevenueItem(
                g.Key.MenuItemId,
                g.Key.Name,
                g.Sum(i => i.Quantity * i.UnitPrice),
                g.Sum(i => i.Quantity)
            ))
            .OrderByDescending(i => i.Revenue)
            .Take(5)
            .ToList();

        // Category breakdown
        var categoryGroups = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.MenuItem?.Category ?? "Other")
            .Select(g => new
            {
                CategoryName = g.Key,
                Revenue = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .ToList();

        var totalRevenue = categoryGroups.Sum(c => c.Revenue);
        var categoryBreakdown = categoryGroups
            .Select(c => new CategoryRevenue(
                Guid.Empty, // We don't have category IDs in MenuItem, just names
                c.CategoryName,
                c.Revenue,
                totalRevenue > 0 ? (c.Revenue / totalRevenue) * 100 : 0
            ))
            .OrderByDescending(c => c.Revenue)
            .ToList();

        return new RevenueInsights(revenueByHour, topItems, categoryBreakdown);
    }

    private async Task<CustomerInsights> BuildCustomerInsightsAsync(
        List<OrderEntity> ordersInPeriod,
        DateTime periodStart,
        CancellationToken cancellationToken)
    {
        var uniqueCustomerIds = ordersInPeriod.Select(o => o.UserId).Distinct().ToList();
        var uniqueCustomers = uniqueCustomerIds.Count;

        var ordersPerCustomer = uniqueCustomers > 0
            ? (decimal)ordersInPeriod.Count / uniqueCustomers
            : 0;

        // Determine new vs returning customers
        // A customer is "new" if they had no orders before this period
        var customersWithPriorOrders = await _context.Orders
            .Where(o => o.OrderDate < periodStart && uniqueCustomerIds.Contains(o.UserId))
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var returningCustomers = customersWithPriorOrders.Count;
        var newCustomers = uniqueCustomers - returningCustomers;
        var newCustomerPercentage = uniqueCustomers > 0
            ? (decimal)newCustomers / uniqueCustomers * 100
            : 0;

        return new CustomerInsights(
            uniqueCustomers,
            ordersPerCustomer,
            newCustomers,
            returningCustomers,
            newCustomerPercentage
        );
    }
}