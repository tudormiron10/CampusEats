namespace CampusEats.Client.Models;

public record DietaryTagResponse(
    Guid DietaryTagId,
    string Name
);

public record CreateDietaryTagRequest(
    string Name
);

public record UpdateDietaryTagRequest(
    string Name
);