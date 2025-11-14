using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Features.Categories.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using MediatR;

namespace CampusEats.Api.Features.Categories.Handler;

public class CreateCategoryHandler : IRequestHandler<CreateCategoryRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public CreateCategoryHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            CategoryId = Guid.NewGuid(),
            Name = request.Name,
            Icon = request.Icon,
            SortOrder = request.SortOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new CategoryResponse(category.CategoryId, category.Name, category.Icon, category.SortOrder);
        return Results.Created($"/categories/{category.CategoryId}", response);
    }
}
