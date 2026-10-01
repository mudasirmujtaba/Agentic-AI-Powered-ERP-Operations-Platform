using FluentValidation;
using OpsPilot.Application.Categories;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Products;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Catalog;

public class CatalogServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private CategoryService Categories() => new(_database.NewContext(), new SaveCategoryRequestValidator());
    private ProductService Products() => new(_database.NewContext(), new SaveProductRequestValidator());

    private static SaveProductRequest Product(Guid categoryId, string code = "X200", int reorderPoint = 100, int safetyStock = 35) =>
        new(code, "Industrial Pump X200", null, categoryId, 189m, 125m, reorderPoint, safetyStock, null, true);

    [Fact]
    public async Task CreateProduct_ReturnsCategoryName()
    {
        var category = await Categories().CreateAsync(new SaveCategoryRequest("Pumps & Valves", null));

        var product = await Products().CreateAsync(Product(category.Id));

        Assert.Equal("Pumps & Valves", product.CategoryName);
        Assert.Null(product.PrimarySupplierName);
    }

    [Fact]
    public async Task CreateProduct_WithUnknownCategory_ThrowsValidationForCategoryId()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Products().CreateAsync(Product(Guid.NewGuid())));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SaveProductRequest.CategoryId));
    }

    [Fact]
    public async Task CreateProduct_WithSafetyStockAboveReorderPoint_ThrowsValidation()
    {
        var category = await Categories().CreateAsync(new SaveCategoryRequest("Pumps & Valves", null));

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => Products().CreateAsync(Product(category.Id, reorderPoint: 10, safetyStock: 20)));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SaveProductRequest.SafetyStock));
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateCode_ThrowsConflict()
    {
        var category = await Categories().CreateAsync(new SaveCategoryRequest("Pumps & Valves", null));
        await Products().CreateAsync(Product(category.Id));

        await Assert.ThrowsAsync<ConflictException>(() => Products().CreateAsync(Product(category.Id, code: "x200")));
    }

    [Fact]
    public async Task DeleteCategory_WithProducts_ThrowsConflict()
    {
        var category = await Categories().CreateAsync(new SaveCategoryRequest("Pumps & Valves", null));
        await Products().CreateAsync(Product(category.Id));

        await Assert.ThrowsAsync<ConflictException>(() => Categories().DeleteAsync(category.Id));
    }

    [Fact]
    public async Task DeleteCategory_WithoutProducts_RemovesIt()
    {
        var category = await Categories().CreateAsync(new SaveCategoryRequest("Obsolete", null));

        await Categories().DeleteAsync(category.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => Categories().GetAsync(category.Id));
    }

    public void Dispose() => _database.Dispose();
}
