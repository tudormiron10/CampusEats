// Features/Kitchen/GetDailySalesReportHandler.cs
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Kitchen;

// Acesta este obiectul de răspuns pe care îl vom returna
public record DailySalesReportItem(Guid MenuItemId, string Name, int QuantitySold);

public class GetDailySalesReportHandler
{
    private readonly CampusEatsDbContext _context;

    public GetDailySalesReportHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Luăm toate articolele din comenzile finalizate astăzi
        var itemsSoldToday = await _context.Orders
            .Where(o => o.Status == OrderStatus.Completed && 
                        DateOnly.FromDateTime(o.OrderDate) == today)
            .SelectMany(o => o.Items) // Extragem toate listele de articole
            .ToListAsync();
            
        // 2. Grupăm și numărăm articolele
        var report = itemsSoldToday
            .GroupBy(item => item.MenuItemId)
            .Select(group => new DailySalesReportItem(
                group.Key,
                group.First().Name,
                group.Count()
            ))
            .ToList();

        return Results.Ok(report);
    }
}