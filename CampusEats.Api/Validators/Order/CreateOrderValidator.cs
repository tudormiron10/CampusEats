using CampusEats.Api.Features.Order.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Orders;

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
        RuleFor(x => x.MenuItemIds)
            .NotEmpty()
            .WithMessage("The order must contain at least one product.");
    }
}