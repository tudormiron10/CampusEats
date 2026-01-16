﻿using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class GetAllOffersHandler : IRequestHandler<GetAllOffersRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetAllOffersHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetAllOffersRequest request, CancellationToken cancellationToken)
    {
        if (!request.HttpContext.IsStaff())
            return Results.Forbid();

        var offers = await _context.Offers
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OfferResponse(
                o.OfferId,
                o.Title,
                o.Description,
                o.ImageUrl,
                o.PointCost,
                o.MinimumTier,
                o.Items.Select(i => new OfferItemResponse(
                    i.MenuItemId,
                    i.MenuItem != null ? i.MenuItem.Name : "Deleted Item",
                    i.Quantity
                )).ToList(),
                o.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(offers);
    }
}
