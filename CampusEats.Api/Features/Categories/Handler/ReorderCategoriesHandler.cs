using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Categories.Handler;

public class ReorderCategoriesHandler : IRequestHandler<ReorderCategoriesRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public ReorderCategoriesHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(ReorderCategoriesRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderedCategoryIds == null || request.OrderedCategoryIds.Count == 0)
            return ApiErrors.ValidationFailed("Category IDs are required.");

        var categories = await _context.Categories
            .Where(c => request.OrderedCategoryIds.Contains(c.CategoryId))
            .ToListAsync(cancellationToken);

        if (categories.Count != request.OrderedCategoryIds.Count)
            return ApiErrors.ValidationFailed("Some category IDs were not found.");

        // Update sort order based on position in the list
        for (int i = 0; i < request.OrderedCategoryIds.Count; i++)
        {
            var category = categories.First(c => c.CategoryId == request.OrderedCategoryIds[i]);
            category.SortOrder = i;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}