using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Features.Categories.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Categories.Handler;

public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public UpdateCategoryHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        if (category == null)
            return ApiErrors.CategoryNotFound();

        category.Name = request.Name;
        category.Icon = request.Icon;
        category.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync(cancellationToken);

        var response = new CategoryResponse(category.CategoryId, category.Name, category.Icon, category.SortOrder);
        return Results.Ok(response);
    }
}
