using CampusEats.Api.Features.Loyalty.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Loyalty;

public class CreateOfferValidator : AbstractValidator<CreateOfferRequest>
{
    public CreateOfferValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(100).WithMessage("Title must be 100 characters or less");

        RuleFor(x => x.PointCost)
            .GreaterThan(0).WithMessage("Point cost must be greater than 0");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one menu item is required");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.MenuItemId)
                .NotEmpty().WithMessage("Menu item ID is required");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

