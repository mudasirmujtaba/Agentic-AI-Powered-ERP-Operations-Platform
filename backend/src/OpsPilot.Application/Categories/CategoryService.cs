using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Catalog;

namespace OpsPilot.Application.Categories;

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public class CategoryService(IApplicationDbContext db, IValidator<SaveCategoryRequest> validator) : ICategoryService
{
    private static readonly Dictionary<string, Expression<Func<Category, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = c => c.Name,
            ["productCount"] = c => c.Products.Count,
        };

    public async Task<PagedResult<CategoryDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var categories = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(c => c.Name.Contains(search));
        }

        return await categories
            .ApplySort(query, SortMap, "name")
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Products.Count))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, excludeId: null, cancellationToken);

        var category = new Category { Name = name, Description = request.Description?.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Description, 0);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        var name = request.Name.Trim();
        await EnsureNameIsUniqueAsync(name, excludeId: id, cancellationToken);

        category.Name = name;
        category.Description = request.Description?.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        if (await db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken))
        {
            throw new ConflictException($"Category '{category.Name}' still has products and cannot be deleted.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != excludeId, cancellationToken))
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }
    }
}
