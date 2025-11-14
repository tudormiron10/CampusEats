namespace CampusEats.Client.Models;

public record CategoryResponse(
    Guid CategoryId,
    string Name,
    string Icon,
    int SortOrder
);

public record CreateCategoryRequest(
    string Name,
    string Icon,
    int SortOrder
);

public record UpdateCategoryRequest(
    string Name,
    string Icon,
    int SortOrder
);
