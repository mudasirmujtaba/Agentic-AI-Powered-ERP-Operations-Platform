using OpsPilot.Application.Customers;
using OpsPilot.Domain.Customers;

namespace OpsPilot.UnitTests.Customers;

public class SaveCustomerRequestValidatorTests
{
    private readonly SaveCustomerRequestValidator _validator = new();

    private static CustomerAddressDto Address(AddressType type, bool isDefault) =>
        new(null, type, "1 Main St", null, "Detroit", null, null, "United States", isDefault);

    private static SaveCustomerRequest Request(params CustomerAddressDto[] addresses) =>
        new("CUST-1", "Apex", null, null, null, 0m, 30, CustomerStatus.Active, addresses);

    [Fact]
    public void AllowsOneDefaultAddressPerType()
    {
        var result = _validator.Validate(Request(Address(AddressType.Billing, true), Address(AddressType.Shipping, true)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void RejectsTwoDefaultAddressesOfTheSameType()
    {
        var result = _validator.Validate(Request(Address(AddressType.Billing, true), Address(AddressType.Billing, true)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void RejectsNegativeCreditLimitAndMissingName()
    {
        var result = _validator.Validate(Request() with { Name = "", CreditLimit = -1m });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveCustomerRequest.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveCustomerRequest.CreditLimit));
    }
}
