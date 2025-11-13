using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Kitchen.Response; 


namespace CampusEats.Api.Features.Kitchen.Handler;

public class GetDailySalesReportHandler : IRequestHandler<GetDailySalesReportRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetDailySalesReportHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetDailySalesReportRequest request, CancellationToken cancellationToken)
    {
        var startDateTime = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDateTime = request.Date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await _context.OrderItems
            .AsNoTracking()
            .Include(oi => oi.MenuItem)
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.OrderDate >= startDateTime && oi.Order.OrderDate < endDateTime)
            .GroupBy(oi => new { oi.MenuItemId, Name = oi.MenuItem != null ? oi.MenuItem.Name : string.Empty })
            .Select(g => new GetDailySalesReportResponse( 
                g.Key.MenuItemId,
                g.Key.Name,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.Quantity * x.UnitPrice)
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }
}