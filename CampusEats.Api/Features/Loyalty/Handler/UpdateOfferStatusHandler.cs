﻿using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class UpdateOfferStatusHandler : IRequestHandler<UpdateOfferStatusRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public UpdateOfferStatusHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(UpdateOfferStatusRequest request, CancellationToken cancellationToken)
    {
        if (!request.HttpContext.IsStaff())
            return Results.Forbid();

        var offer = await _context.Offers
            .Include(o => o.Items)
                .ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(o => o.OfferId == request.OfferId, cancellationToken);

        if (offer == null)
            return ApiErrors.NotFound("Offer");

        offer.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        var response = new OfferResponse(
            offer.OfferId,
            offer.Title,
            offer.Description,
            offer.ImageUrl,
            offer.PointCost,
            offer.MinimumTier,
            offer.Items.Select(i => new OfferItemResponse(
                i.MenuItemId,
                i.MenuItem != null ? i.MenuItem.Name : "Deleted Item",
                i.Quantity
            )).ToList(),
            offer.IsActive
        );

        return Results.Ok(response);
    }
}
