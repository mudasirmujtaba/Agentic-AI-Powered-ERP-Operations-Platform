using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Application.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierListItemDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default);
}

public class SupplierService(IApplicationDbContext db, IValidator<SaveSupplierRequest> validator) : ISupplierService
{
    private static readonly Dictionary<string, Expression<Func<Supplier, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = s => s.Code,
            ["name"] = s => s.Name,
            ["averageLeadTimeDays"] = s => s.AverageLeadTimeDays,
            ["isActive"] = s => s.IsActive,
        };

    public async Task<PagedResult<SupplierListItemDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var suppliers = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            suppliers = suppliers.Where(s => s.Code.Contains(search) || s.Name.Contains(search));
        }

        return await suppliers
            .ApplySort(query, SortMap, "name")
            .Select(s => new SupplierListItemDto(s.Id, s.Code, s.Name, s.ContactName, s.Email, s.AverageLeadTimeDays, s.IsActive))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), id);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: null, cancellationToken);

        var supplier = new Supplier { Code = code };
        Apply(supplier, request);

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), id);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: id, cancellationToken);

        supplier.Code = code;
        Apply(supplier, request);

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await db.Suppliers.AnyAsync(s => s.Code == code && s.Id != excludeId, cancellationToken))
        {
            throw new ConflictException($"A supplier with code '{code}' already exists.");
        }
    }

    private static void Apply(Supplier supplier, SaveSupplierRequest request)
    {
        supplier.Name = request.Name.Trim();
        supplier.ContactName = request.ContactName?.Trim();
        supplier.Email = request.Email?.Trim();
        supplier.Phone = request.Phone?.Trim();
        supplier.PaymentTermsDays = request.PaymentTermsDays;
        supplier.AverageLeadTimeDays = request.AverageLeadTimeDays;
        supplier.IsActive = request.IsActive;
    }

    private static SupplierDto ToDto(Supplier s) => new(
        s.Id, s.Code, s.Name, s.ContactName, s.Email, s.Phone,
        s.PaymentTermsDays, s.AverageLeadTimeDays, s.IsActive, s.CreatedAtUtc, s.UpdatedAtUtc);
}
