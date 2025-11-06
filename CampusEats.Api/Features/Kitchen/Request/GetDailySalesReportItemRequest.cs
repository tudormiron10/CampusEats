// Features/Kitchen/DailySalesReportDto.cs
namespace CampusEats.Api.Features.Kitchen;

// Acesta este obiectul pe care îl vom returna: 
// un articol de meniu și de câte ori a fost vândut
public record GetDailySalesReportItemRequest(Guid MenuItemId, string Name, int QuantitySold);