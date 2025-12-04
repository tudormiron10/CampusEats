using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Features.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Payments
{
    public class PaymentConfirmationHandler : IRequestHandler<PaymentConfirmationRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public PaymentConfirmationHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(PaymentConfirmationRequest request, CancellationToken cancellationToken)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.PaymentId == request.PaymentId, cancellationToken);
                
            if (payment == null)
            {
                return Results.NotFound("Payment not found.");
            }

            if (payment.Status != PaymentStatus.Processing)
            {
                return Results.BadRequest("This payment has already been processed.");
            }

            if (request.NewStatus == PaymentStatus.Processing)
            {
                return Results.BadRequest("Invalid status provided by webhook.");
            }

            payment.Status = request.NewStatus;
            payment.UpdatedAt = DateTime.UtcNow;

            // Award loyalty points if payment is successful
            if (request.NewStatus == PaymentStatus.Succeeded && payment.Order != null)
            {
                var user = await _context.Users
                    .Include(u => u.Loyalty)
                    .FirstOrDefaultAsync(u => u.UserId == payment.Order.UserId, cancellationToken);

                if (user?.Loyalty != null)
                {
                    // Calculate points based on current tier
                    var tier = LoyaltyHelpers.CalculateTier(user.Loyalty.LifetimePoints);
                    var earnRate = LoyaltyHelpers.GetEarnRate(tier);
                    var pointsEarned = (int)(payment.Order.TotalAmount * earnRate);

                    // Add points
                    user.Loyalty.CurrentPoints += pointsEarned;
                    user.Loyalty.LifetimePoints += pointsEarned;

                    // Create transaction record
                    var transaction = new LoyaltyTransaction
                    {
                        TransactionId = Guid.NewGuid(),
                        Date = DateTime.UtcNow,
                        Description = $"Order #{payment.Order.OrderId.ToString()[..8]}",
                        Type = "Earned",
                        Points = pointsEarned,
                        OrderId = payment.Order.OrderId,
                        LoyaltyId = user.Loyalty.LoyaltyId
                    };

                    _context.LoyaltyTransactions.Add(transaction);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            var response = new PaymentResponse(
                payment.PaymentId,
                payment.OrderId,
                payment.Amount,
                payment.Status.ToString(),
                payment.ClientSecret
            );

            return Results.Ok(response);
        }
    }
}