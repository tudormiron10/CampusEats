using CampusEats.Api.Features.User.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.User;

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        
        RuleFor(x => x.Password).NotEmpty();
    }
}