using CampusEats.Api.Features.Order.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Orders;

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
        
        // Order must have at least one item (paid OR redeemed)
        RuleFor(x => x)
            .Must(x => (x.MenuItemIds != null && x.MenuItemIds.Any()) || 
                       (x.RedeemedMenuItemIds != null && x.RedeemedMenuItemIds.Any()))
            .WithMessage("The order must contain at least one product.");
    }
}