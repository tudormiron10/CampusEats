// Validators/Users/UpdateUserValidator.cs
using CampusEats.Api.Features.Users;
using FluentValidation;

namespace CampusEats.Api.Validators.Users;

public class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Email address is not valid.");

        RuleFor(x => x.Role).IsInEnum().WithMessage("The specified role is not valid.");
    }
}