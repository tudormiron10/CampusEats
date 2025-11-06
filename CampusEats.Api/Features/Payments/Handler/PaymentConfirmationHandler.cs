using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Payments
{
    public class PaymentConfirmationHandler
    {
        private readonly CampusEatsDbContext _context;

        public PaymentConfirmationHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(PaymentConfirmationRequest request)
        {
            var payment = await _context.Payments.FindAsync(request.PaymentId);
            if (payment == null)
            {
                return Results.NotFound("Payment not found.");
            }

            if (payment.Status != PaymentStatus.Initiated)
            {
                return Results.BadRequest("This payment has already been processed.");
            }

            if (request.NewStatus == PaymentStatus.Initiated)
            {
                return Results.BadRequest("Invalid status provided by webhook.");
            }

            payment.Status = request.NewStatus;

            await _context.SaveChangesAsync();

            var response = new PaymentResponse(
                payment.PaymentId,
                payment.OrderId,
                payment.Amount,
                payment.Status.ToString()
            );

            return Results.Ok(response);
        }
    }
}