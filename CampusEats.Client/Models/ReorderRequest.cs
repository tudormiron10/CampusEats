namespace CampusEats.Client.Models;

public record ReorderCategoriesRequest(
    List<Guid> OrderedCategoryIds
);

public record ReorderMenuItemsRequest(
    List<Guid> OrderedMenuItemIds
);