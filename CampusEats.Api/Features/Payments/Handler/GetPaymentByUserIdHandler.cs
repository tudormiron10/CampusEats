using CampusEats.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Payments
{
    public class GetPaymentByUserIdHandler
    {
        private readonly CampusEatsDbContext _context;

        public GetPaymentByUserIdHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(Guid userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.UserId == userId);
            if (!userExists)
            {
                return Results.NotFound("User not found.");
            }

            var payments = await _context.Payments
                .AsNoTracking()
                .Include(p => p.Order)
                .Where(p => p.Order.UserId == userId)
                .Select(p => new PaymentResponse(p.PaymentId, p.OrderId, p.Amount, p.Status.ToString()))
                .ToListAsync();

            return Results.Ok(payments);
        }
    }
}