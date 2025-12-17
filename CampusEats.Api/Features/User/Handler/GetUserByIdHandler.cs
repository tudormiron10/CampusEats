﻿using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.User.Response;
using MediatR;

namespace CampusEats.Api.Features.Users
{
    public class GetUserByIdHandler : IRequestHandler<GetUserByIdRequest, IResult>
    {
        private readonly CampusEatsDbContext _context;
        public GetUserByIdHandler(CampusEatsDbContext context) { _context = context; }

        public async Task<IResult> Handle(GetUserByIdRequest request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.Loyalty)
                .Include(u => u.Orders)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user == null)
                return ApiErrors.UserNotFound();

            var response = new UserResponse(
                user.UserId,
                user.Name,
                user.Email,
                user.Role.ToString(),
                user.Loyalty?.CurrentPoints,
                user.CreatedAt,
                user.Orders.Count
            );

            return Results.Ok(response);
        }
    }
}