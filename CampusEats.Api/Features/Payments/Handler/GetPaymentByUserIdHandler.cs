using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.Payments.Request;
using MediatR;
using CampusEats.Api.Features.Payments.Response;

namespace CampusEats.Api.Features.Payments
{
    public class GetPaymentByUserIdHandler : IRequestHandler<GetPaymentByUserIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public GetPaymentByUserIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(GetPaymentByUserIdRequest request, CancellationToken cancellationToken)
        {
            var userExists = await _context.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken);
            if (!userExists)
            {
                return Results.NotFound("User not found.");
            }

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Order)
                .Where(p => p.Order.UserId == request.UserId)
                .Select(p => new PaymentResponse(p.PaymentId, p.OrderId, p.Amount, p.Status.ToString(), p.ClientSecret))
                .ToListAsync(cancellationToken);

            return Results.Ok(payments);
        }
    }
}