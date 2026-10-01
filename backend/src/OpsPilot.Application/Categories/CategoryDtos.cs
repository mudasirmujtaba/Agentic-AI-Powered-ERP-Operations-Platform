namespace OpsPilot.Application.Categories;

public record CategoryDto(Guid Id, string Name, string? Description, int ProductCount);

public record SaveCategoryRequest(string Name, string? Description);
