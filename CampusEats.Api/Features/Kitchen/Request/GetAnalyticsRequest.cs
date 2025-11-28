using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request;

public record GetAnalyticsRequest(
    DateTime StartDate,
    DateTime EndDate,
    string GroupBy // "hour" | "day" | "month"
) : IRequest<IResult>;