using CampusEats.Api.Features.DietaryTags.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.DietaryTags;

public class CreateDietaryTagValidator : AbstractValidator<CreateDietaryTagRequest>
{
    public CreateDietaryTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Dietary tag name is required.")
            .MaximumLength(100).WithMessage("Dietary tag name must not exceed 100 characters.")
            .MinimumLength(2).WithMessage("Dietary tag name must be at least 2 characters.");
    }
}

