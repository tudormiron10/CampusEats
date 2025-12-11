using CampusEats.Api.Features.DietaryTags.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.DietaryTags;

public class DeleteDietaryTagValidator : AbstractValidator<DeleteDietaryTagRequest>
{
    public DeleteDietaryTagValidator()
    {
        RuleFor(x => x.DietaryTagId)
            .NotEmpty().WithMessage("Dietary tag ID is required.");
    }
}

