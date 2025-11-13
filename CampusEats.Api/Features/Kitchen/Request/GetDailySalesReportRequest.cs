using MediatR;

namespace CampusEats.Api.Features.Kitchen.Request
{
    public record GetDailySalesReportRequest(DateOnly Date) : IRequest<IResult>;
}