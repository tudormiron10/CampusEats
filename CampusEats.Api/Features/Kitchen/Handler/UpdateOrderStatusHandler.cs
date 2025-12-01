﻿using MediatR;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Kitchen.Handler;

public class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public UpdateOrderStatusHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

        if (order == null)
            return ApiErrors.OrderNotFound();

        var current = order.Status;
        var next = request.NewStatus;

        bool isValid = current switch
        {
            OrderStatus.Pending => next is OrderStatus.InPreparation or OrderStatus.Cancelled,
            OrderStatus.InPreparation => next is OrderStatus.Ready,
            OrderStatus.Ready => next is OrderStatus.Completed,
            _ => false
        };

        if (!isValid)
            return Results.BadRequest("Invalid status transition.");

        order.Status = next;
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

