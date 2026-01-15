using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Payments;
using MediatR;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Infrastructure.Extensions;
using Stripe;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Payments
{
    public class CreatePaymentHandler : IRequestHandler<CreatePaymentRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CreatePaymentHandler> _logger;

        public CreatePaymentHandler(
            CampusEatsDbContext context,
            IConfiguration configuration,
            ILogger<CreatePaymentHandler> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IResult> Handle(CreatePaymentRequest request, CancellationToken cancellationToken)
        {
            var validator = new CreatePaymentValidator();
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var order = await _context.Orders.FindAsync(new object[] { request.OrderId }, cancellationToken);
            if (order == null)
            {
                return ApiErrors.OrderNotFound();
            }

            if (order.Status != OrderStatus.Pending)
            {
                return ApiErrors.InvalidOperation("This order is not pending and cannot be paid.");
            }

            // Check if there's already an active payment for this order
            var existingPayment = await _context.Payments
                .Where(p => p.OrderId == request.OrderId 
                            && p.Status != PaymentStatus.Failed 
                            && p.Status != PaymentStatus.Cancelled)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingPayment != null)
            {
                _logger.LogInformation(
                    "Returning existing payment {PaymentId} for order {OrderId} to prevent duplicate PaymentIntent creation",
                    existingPayment.PaymentId,
                    request.OrderId);

                // Return the existing payment instead of creating a new one
                var existingResponse = new PaymentResponse(
                    existingPayment.PaymentId,
                    existingPayment.OrderId,
                    existingPayment.Amount,
                    existingPayment.Status.ToString(),
                    existingPayment.ClientSecret
                );
                
                return Results.Ok(existingResponse);
            }

            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

            // Create Stripe PaymentIntent
            var paymentIntentService = new PaymentIntentService();
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(order.TotalAmount * 100), // Stripe uses smallest currency unit (bani for RON)
                Currency = "ron", // Romanian Lei
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never" // Disable redirect-based payment methods for easier testing
                },
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", order.OrderId.ToString() }
                }
            };

            PaymentIntent paymentIntent;
            try
            {
                paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe error while creating PaymentIntent for order {OrderId}", order.OrderId);
                return Results.Problem(
                    detail: "Failed to initiate payment with payment provider. Please try again.",
                    statusCode: 500
                );
            }

            var payment = new Infrastructure.Persistence.Entities.Payment
            {
                PaymentId = Guid.NewGuid(),
                OrderId = request.OrderId,
                Amount = order.TotalAmount,
                Status = PaymentStatus.Processing,
                StripePaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var response = new PaymentResponse(
                payment.PaymentId,
                payment.OrderId,
                payment.Amount,
                payment.Status.ToString(),
                payment.ClientSecret
            );

            return Results.Created($"/payments/{payment.PaymentId}", response);
        }
    }
}