using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Catalog;

namespace OpsPilot.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default);
}

public class ProductService(IApplicationDbContext db, IValidator<SaveProductRequest> validator) : IProductService
{
    private static readonly Dictionary<string, Expression<Func<Product, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = p => p.Code,
            ["name"] = p => p.Name,
            ["categoryName"] = p => p.Category.Name,
            ["unitPrice"] = p => p.UnitPrice,
            ["reorderPoint"] = p => p.ReorderPoint,
            ["isActive"] = p => p.IsActive,
        };

    public async Task<PagedResult<ProductListItemDto>> ListAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        var products = db.Products.AsNoTracking();

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(p => p.Code.Contains(search) || p.Name.Contains(search));
        }

        return await products
            .ApplySort(query, SortMap, "code")
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.Category.Name,
                p.UnitPrice,
                p.ReorderPoint,
                p.SafetyStock,
                p.PrimarySupplier != null ? p.PrimarySupplier.Name : null,
                p.IsActive))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.CategoryId,
                p.Category.Name,
                p.UnitPrice,
                p.Cost,
                p.ReorderPoint,
                p.SafetyStock,
                p.PrimarySupplierId,
                p.PrimarySupplier != null ? p.PrimarySupplier.Name : null,
                p.IsActive,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);
    }

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureReferencesExistAsync(request, cancellationToken);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: null, cancellationToken);

        var product = new Product { Code = code };
        Apply(product, request);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), id);

        await EnsureReferencesExistAsync(request, cancellationToken);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: id, cancellationToken);

        product.Code = code;
        Apply(product, request);

        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task EnsureReferencesExistAsync(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            failures.Add(new ValidationFailure(nameof(request.CategoryId), "The selected category does not exist."));
        }

        if (request.PrimarySupplierId is { } supplierId &&
            !await db.Suppliers.AnyAsync(s => s.Id == supplierId, cancellationToken))
        {
            failures.Add(new ValidationFailure(nameof(request.PrimarySupplierId), "The selected supplier does not exist."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(p => p.Code == code && p.Id != excludeId, cancellationToken))
        {
            throw new ConflictException($"A product with code '{code}' already exists.");
        }
    }

    private static void Apply(Product product, SaveProductRequest request)
    {
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.CategoryId = request.CategoryId;
        product.UnitPrice = request.UnitPrice;
        product.Cost = request.Cost;
        product.ReorderPoint = request.ReorderPoint;
        product.SafetyStock = request.SafetyStock;
        product.PrimarySupplierId = request.PrimarySupplierId;
        product.IsActive = request.IsActive;
    }
}
