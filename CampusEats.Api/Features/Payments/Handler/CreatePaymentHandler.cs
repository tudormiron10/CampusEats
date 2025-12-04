using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Payments;
using MediatR;
using CampusEats.Api.Features.Payments.Response;
using CampusEats.Api.Infrastructure.Extensions;
using Stripe;

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

            var order = await _context.Orders.FindAsync(request.OrderId);
            if (order == null)
            {
                return ApiErrors.OrderNotFound();
            }

            if (order.Status != OrderStatus.Pending)
            {
                return ApiErrors.InvalidOperation("This order is not pending and cannot be paid.");
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