namespace CampusEats.Api.Features.Categories.Response;

public record CategoryResponse(
    Guid CategoryId,
    string Name,
    string Icon,
    int SortOrder
);
