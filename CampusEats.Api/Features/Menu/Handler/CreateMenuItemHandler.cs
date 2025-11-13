using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Validators.Menu;
using MediatR;

namespace CampusEats.Api.Features.Menu.Handler
{
    public class CreateMenuItemHandler : IRequestHandler<CreateMenuItemRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;

        public CreateMenuItemHandler(CampusEatsDbContext context)
        {
            _context = context;
        }

        public async Task<IResult> Handle(CreateMenuItemRequest request, CancellationToken cancellationToken)
        {
            var validator = new CreateMenuItemValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.BadRequest(validationResult.Errors);
            }

            var menuItem = new MenuItem
            {
                MenuItemId = Guid.NewGuid(),
                Name = request.Name,
                Price = request.Price,
                Category = request.Category,
                Description = request.Description,
                ImageUrl = request.ImageUrl
            };

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            var response = new MenuItemResponse(
                menuItem.MenuItemId,
                menuItem.Name,
                menuItem.Price,
                menuItem.Category,
                menuItem.ImageUrl,
                menuItem.Description
            );

            return Results.Created($"/menu/{menuItem.MenuItemId}", response);
        }
    }
}