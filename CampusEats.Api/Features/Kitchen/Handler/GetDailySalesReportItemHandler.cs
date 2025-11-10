// Features/Kitchen/GetDailySalesReportItemHandler.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace CampusEats.Api.Features.Kitchen;

// Acesta este obiectul de răspuns pe care îl vom returna
public record DailySalesReportItem(Guid MenuItemId, string Name, int QuantitySold);

public class GetDailySalesReportItemHandler
{
    private readonly CampusEatsDbContext _context;

    public GetDailySalesReportItemHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle()
    {
        // Use a DateTime range to avoid EF translation issues with DateOnly
        var utcNow = DateTime.UtcNow;
        var start = utcNow.Date;
        var end = start.AddDays(1);

        // Query order items for completed orders in the date range, include MenuItem for name
        var items = await _context.OrderItems
            .Include(oi => oi.MenuItem)
            .Include(oi => oi.Order)
            .Where(oi => oi.Order != null
                         && oi.Order.Status == OrderStatus.Completed
                         && oi.Order.OrderDate >= start
                         && oi.Order.OrderDate < end)
            .ToListAsync();

        var report = items
            .GroupBy(oi => oi.MenuItemId)
            .Select(g => new DailySalesReportItem(
                g.Key,
                g.First().MenuItem != null ? g.First().MenuItem!.Name : string.Empty,
                g.Sum(oi => oi.Quantity)
            ))
            .ToList();

        return Results.Ok(report);
    }
}