using CampusEats.Api.Features.Categories.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Category;

public class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Category icon is required")
            .MaximumLength(10).WithMessage("Category icon must not exceed 10 characters");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be 0 or greater");
    }
}
