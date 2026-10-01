using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Models;
using OpsPilot.Domain.Customers;

namespace OpsPilot.Application.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<CustomerDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default);
}

public class CustomerService(IApplicationDbContext db, IValidator<SaveCustomerRequest> validator) : ICustomerService
{
    private static readonly Dictionary<string, Expression<Func<Customer, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = c => c.Code,
            ["name"] = c => c.Name,
            ["creditLimit"] = c => c.CreditLimit,
            ["status"] = c => c.Status,
        };

    public async Task<PagedResult<CustomerListItemDto>> ListAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        var customers = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            customers = customers.Where(c =>
                c.Code.Contains(search) ||
                c.Name.Contains(search) ||
                (c.Email != null && c.Email.Contains(search)));
        }

        return await customers
            .ApplySort(query, SortMap, "name")
            .Select(c => new CustomerListItemDto(c.Id, c.Code, c.Name, c.ContactName, c.Email, c.Phone, c.CreditLimit, c.Status))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

        return ToDto(customer);
    }

    public async Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: null, cancellationToken);

        var customer = new Customer { Code = code };
        Apply(customer, request);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

        var code = Codes.Normalize(request.Code);
        await EnsureCodeIsUniqueAsync(code, excludeId: id, cancellationToken);

        customer.Code = code;
        Apply(customer, request);

        await db.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var taken = await db.Customers.AnyAsync(c => c.Code == code && c.Id != excludeId, cancellationToken);
        if (taken)
        {
            throw new ConflictException($"A customer with code '{code}' already exists.");
        }
    }

    private static void Apply(Customer customer, SaveCustomerRequest request)
    {
        customer.Name = request.Name.Trim();
        customer.ContactName = request.ContactName?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Phone = request.Phone?.Trim();
        customer.CreditLimit = request.CreditLimit;
        customer.PaymentTermsDays = request.PaymentTermsDays;
        customer.Status = request.Status;
        // A manual status change takes ownership of the hold away from the credit policy.
        if (request.Status != CustomerStatus.OnHold) customer.OnPolicyCreditHold = false;

        // Addresses are value-like children, so the submitted set replaces the stored one;
        // EF deletes the removed rows as orphans of the required relationship.
        customer.Addresses.Clear();
        foreach (var address in request.Addresses)
        {
            customer.Addresses.Add(new CustomerAddress
            {
                Type = address.Type,
                Line1 = address.Line1.Trim(),
                Line2 = address.Line2?.Trim(),
                City = address.City.Trim(),
                State = address.State?.Trim(),
                PostalCode = address.PostalCode?.Trim(),
                Country = address.Country.Trim(),
                IsDefault = address.IsDefault,
            });
        }
    }

    private static CustomerDto ToDto(Customer c) => new(
        c.Id,
        c.Code,
        c.Name,
        c.ContactName,
        c.Email,
        c.Phone,
        c.CreditLimit,
        c.PaymentTermsDays,
        c.Status,
        c.Addresses
            .OrderBy(a => a.Type).ThenByDescending(a => a.IsDefault)
            .Select(a => new CustomerAddressDto(a.Id, a.Type, a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country, a.IsDefault))
            .ToList(),
        c.CreatedAtUtc,
        c.UpdatedAtUtc);
}
