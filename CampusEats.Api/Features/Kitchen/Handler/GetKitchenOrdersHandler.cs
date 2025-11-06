// Features/Kitchen/GetKitchenOrdersHandler.cs
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Kitchen;

public class GetKitchenOrdersHandler
{
    private readonly CampusEatsDbContext _context;
    public GetKitchenOrdersHandler(CampusEatsDbContext context) { _context = context; }

    public async Task<IResult> Handle()
    {
        var activeOrders = await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Ready)
            .AsNoTracking()
            .ToListAsync();
            
        return Results.Ok(activeOrders);
    }
}