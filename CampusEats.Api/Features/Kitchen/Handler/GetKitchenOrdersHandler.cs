using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;

namespace CampusEats.Api.Features.Kitchen
{
    public class GetKitchenOrdersHandler : IRequestHandler<GetKitchenOrdersRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        public GetKitchenOrdersHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(GetKitchenOrdersRequest request, CancellationToken cancellationToken)
        {
            var activeOrders = await _context.Orders
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.InPreparation || o.Status == OrderStatus.Ready)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return Results.Ok(activeOrders);
        }
    }
}