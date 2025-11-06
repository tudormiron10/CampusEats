// Validators/Menu/UpdateMenuItemValidator.cs
using CampusEats.Api.Features.Menu;
using FluentValidation;

namespace CampusEats.Api.Validators.Menu;

public class UpdateMenuItemValidator : AbstractValidator<UpdateMenuItemRequest>
{
    public UpdateMenuItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(100).WithMessage("Product name cannot exceed 100 characters.");

        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(x => x.Category).NotEmpty().WithMessage("Category is required.");
    }
}