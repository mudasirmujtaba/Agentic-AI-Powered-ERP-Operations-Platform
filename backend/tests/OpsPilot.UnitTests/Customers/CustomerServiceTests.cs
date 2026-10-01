using FluentValidation;
using OpsPilot.Application.Common.Exceptions;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Customers;
using OpsPilot.Domain.Customers;
using OpsPilot.UnitTests.TestSupport;

namespace OpsPilot.UnitTests.Customers;

public class CustomerServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    private CustomerService NewService() => new(_database.NewContext(), new SaveCustomerRequestValidator());

    private static SaveCustomerRequest Request(string code = "cust-100", string name = "Apex Manufacturing", string? email = null) =>
        new(code, name, "Rachel Moore", email, null, 10_000m, 30, CustomerStatus.Active,
        [
            new CustomerAddressDto(null, AddressType.Billing, "1 Main St", null, "Detroit", "MI", "48201", "United States", true),
        ]);

    [Fact]
    public async Task Create_ThenGet_RoundTripsWithAddressesAndNormalizedCode()
    {
        var created = await NewService().CreateAsync(Request());

        var fetched = await NewService().GetAsync(created.Id);

        Assert.Equal("CUST-100", fetched.Code);
        Assert.Equal("Apex Manufacturing", fetched.Name);
        var address = Assert.Single(fetched.Addresses);
        Assert.Equal("Detroit", address.City);
        Assert.Equal(_database.CurrentUser.UserId, (await Load(created.Id)).CreatedBy);
    }

    [Fact]
    public async Task Create_WithDuplicateCodeIgnoringCase_ThrowsConflict()
    {
        await NewService().CreateAsync(Request(code: "CUST-100"));

        await Assert.ThrowsAsync<ConflictException>(() => NewService().CreateAsync(Request(code: " cust-100 ")));
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ThrowsValidationException()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => NewService().CreateAsync(Request(email: "not-an-email")));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SaveCustomerRequest.Email));
    }

    [Fact]
    public async Task Update_MissingCustomer_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => NewService().UpdateAsync(Guid.NewGuid(), Request()));
    }

    [Fact]
    public async Task Update_ReplacesAddressesAndStampsUpdatedAt()
    {
        var created = await NewService().CreateAsync(Request());
        var updatedRequest = Request() with
        {
            Addresses =
            [
                new CustomerAddressDto(null, AddressType.Shipping, "9 Dock Rd", null, "Toledo", "OH", null, "United States", true),
                new CustomerAddressDto(null, AddressType.Billing, "1 Main St", null, "Detroit", "MI", null, "United States", true),
            ],
        };

        var updated = await NewService().UpdateAsync(created.Id, updatedRequest);

        Assert.Equal(2, updated.Addresses.Count);
        Assert.Contains(updated.Addresses, a => a.City == "Toledo");
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_OnlyRemovingAddresses_StillStampsUpdatedAt()
    {
        var created = await NewService().CreateAsync(Request());

        var updated = await NewService().UpdateAsync(created.Id, Request() with { Addresses = [] });

        Assert.Empty(updated.Addresses);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task List_FiltersBySearchAndPages()
    {
        var service = NewService();
        for (var i = 1; i <= 25; i++)
        {
            await service.CreateAsync(Request(code: $"ACME-{i:00}", name: $"Acme Branch {i:00}"));
        }
        await service.CreateAsync(Request(code: "OTHER-1", name: "Unrelated Co"));

        var page = await NewService().ListAsync(new PagedQuery { Search = "Acme", Page = 2, PageSize = 10, SortBy = "code" });

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal("ACME-11", page.Items[0].Code);
    }

    private async Task<Customer> Load(Guid id)
    {
        await using var context = _database.NewContext();
        return await context.Customers.FindAsync(id) ?? throw new InvalidOperationException();
    }

    public void Dispose() => _database.Dispose();
}
