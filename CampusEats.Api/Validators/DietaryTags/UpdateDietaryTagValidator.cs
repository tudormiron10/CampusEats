using CampusEats.Api.Features.DietaryTags.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.DietaryTags;

public class UpdateDietaryTagValidator : AbstractValidator<UpdateDietaryTagRequest>
{
    public UpdateDietaryTagValidator()
    {
        RuleFor(x => x.DietaryTagId)
            .NotEmpty().WithMessage("Dietary tag ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Dietary tag name is required.")
            .MaximumLength(100).WithMessage("Dietary tag name must not exceed 100 characters.")
            .MinimumLength(2).WithMessage("Dietary tag name must be at least 2 characters.");
    }
}

