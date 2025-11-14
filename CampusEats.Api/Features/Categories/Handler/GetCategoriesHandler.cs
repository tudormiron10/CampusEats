using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Features.Categories.Response;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Categories.Handler;

public class GetCategoriesHandler : IRequestHandler<GetCategoriesRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetCategoriesHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetCategoriesRequest request, CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryResponse(c.CategoryId, c.Name, c.Icon, c.SortOrder))
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }
}
