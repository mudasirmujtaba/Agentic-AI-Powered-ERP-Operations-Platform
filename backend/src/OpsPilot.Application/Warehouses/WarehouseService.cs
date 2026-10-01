using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Inventory;

namespace OpsPilot.Application.Warehouses;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<WarehouseDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<WarehouseDto> UpdateAsync(Guid id, SaveWarehouseRequest request, CancellationToken cancellationToken = default);
}

public class WarehouseService(IApplicationDbContext db, IValidator<SaveWarehouseRequest> validator) : IWarehouseService
{
    private static readonly Dictionary<string, Expression<Func<Warehouse, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = w => w.Code,
            ["name"] = w => w.Name,
            ["isActive"] = w => w.IsActive,
        };

    public async Task<PagedResult<WarehouseDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var warehouses = db.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            warehouses = warehouses.Where(w => w.Code.Contains(search) || w.Name.Contains(search));
        }

        return await warehouses
            .ApplySort(query, SortMap, "code")
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Location, w.IsActive, w.CreatedAtUtc, w.UpdatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<WarehouseDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        return ToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: null, cancellationToken);

        var warehouse = new Warehouse { Code = code };
        Apply(warehouse, request);

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    public async Task<WarehouseDto> UpdateAsync(Guid id, SaveWarehouseRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), id);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: id, cancellationToken);

        warehouse.Code = code;
        Apply(warehouse, request);

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(warehouse);
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await db.Warehouses.AnyAsync(w => w.Code == code && w.Id != excludeId, cancellationToken))
        {
            throw new ConflictException($"A warehouse with code '{code}' already exists.");
        }
    }

    private static void Apply(Warehouse warehouse, SaveWarehouseRequest request)
    {
        warehouse.Name = request.Name.Trim();
        warehouse.Location = request.Location?.Trim();
        warehouse.IsActive = request.IsActive;
    }

    private static WarehouseDto ToDto(Warehouse w) =>
        new(w.Id, w.Code, w.Name, w.Location, w.IsActive, w.CreatedAtUtc, w.UpdatedAtUtc);
}
