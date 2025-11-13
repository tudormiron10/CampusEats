namespace CampusEats.Api.Features.Kitchen.Response
{
    public record GetDailySalesReportResponse(Guid MenuItemId, string Name, int Quantity, decimal Revenue);
}