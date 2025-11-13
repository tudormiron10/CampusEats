using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Payments;
using MediatR;
using CampusEats.Api.Features.Payments.Response;

namespace CampusEats.Api.Features.Payments
{
    public class CreatePaymentHandler : IRequestHandler<CreatePaymentRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public CreatePaymentHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(CreatePaymentRequest request, CancellationToken cancellationToken)
        {
            var validator = new CreatePaymentValidator();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var order = await _context.Orders.FindAsync(request.OrderId);
            if (order == null)
            {
                return Results.NotFound("Order not found.");
            }

            if (order.Status != OrderStatus.Pending)
            {
                return Results.BadRequest("This order is not pending and cannot be paid.");
            }

            var payment = new Infrastructure.Persistence.Entities.Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = request.OrderId,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Initiated
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var response = new PaymentResponse(
                payment.PaymentId,
                payment.OrderId,
                payment.Amount,
                payment.Status.ToString()
            );

            return Results.Created($"/payments/{payment.PaymentId}", response);
        }
    }
}