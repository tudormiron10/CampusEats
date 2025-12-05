using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using FluentValidation;

namespace CampusEats.Api.Validators.Payments;

public class PaymentConfirmationValidator : AbstractValidator<PaymentConfirmationRequest>
{
    public PaymentConfirmationValidator()
    {
        RuleFor(x => x.PaymentId)
            .NotEmpty().WithMessage("Payment ID is required.");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("Invalid payment status.")
            .NotEqual(PaymentStatus.Processing).WithMessage("Webhook cannot set status back to Processing.");
    }
}