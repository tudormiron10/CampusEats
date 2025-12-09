using CampusEats.Api.Features.Payments.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Payments;

public class InitiateCheckoutValidator : AbstractValidator<InitiateCheckoutRequest>
{
    public InitiateCheckoutValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items list is required.")
            .NotEmpty().WithMessage("Cart cannot be empty.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId)
                .NotEmpty().WithMessage("Menu item ID is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be at least 1.");
        });
    }
}

