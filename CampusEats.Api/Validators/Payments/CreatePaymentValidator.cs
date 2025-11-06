using CampusEats.Api.Features.Payments;
using FluentValidation;

namespace CampusEats.Api.Validators.Payments;

public class CreatePaymentValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("Order ID is required to initiate a payment.");
    }
}