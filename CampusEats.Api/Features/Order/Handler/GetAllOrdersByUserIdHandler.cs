using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Features.Orders.Requests;
using CampusEats.Api.Features.Orders.Response;

namespace CampusEats.Api.Features.Order.Handler
{
    public class GetAllOrdersByUserIdHandler : IRequestHandler<GetAllOrdersByUserIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _db;
        private readonly ILogger<GetAllOrdersByUserIdHandler> _logger;

        public GetAllOrdersByUserIdHandler(CampusEatsDbContext db, ILogger<GetAllOrdersByUserIdHandler> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IResult> Handle(GetAllOrdersByUserIdRequest request, CancellationToken cancellationToken)
        {
            // Query orders for the user and project to the flat SimpleOrderResponse DTO
            var orders = await _db.Orders
                .AsNoTracking()
                .Where(o => o.UserId == request.UserId)
                .Select(o => new SimpleOrderResponse(
                    o.OrderId,
                    o.UserId,
                    o.Status.ToString(),
                    o.TotalAmount,
                    o.OrderDate
                ))
                .ToListAsync(cancellationToken);

            return Results.Ok(orders);
        }
    }
}
