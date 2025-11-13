// Validators/Users/CreateUserValidator.cs

using CampusEats.Api.Features.User.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Users;

public class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Email address is not valid.");

        RuleFor(x => x.Role).IsInEnum().WithMessage("The specified role is not valid.");
        
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}