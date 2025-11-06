using CampusEats.Api.Features.Menu; // We will need the Request from here
using FluentValidation;

namespace CampusEats.Api.Validators.Menu;

// This is the validator that we will call manually
public class CreateMenuItemValidator : AbstractValidator<CreateMenuItemRequest>
{
    public CreateMenuItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(100).WithMessage("Product name cannot exceed 100 characters.");

        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(x => x.Category).NotEmpty().WithMessage("Category is required.");
    }
}