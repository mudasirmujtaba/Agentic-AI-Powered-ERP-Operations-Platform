using OpsPilot.Domain.Identity;

namespace OpsPilot.UnitTests.Identity;

public class RolesTests
{
    [Fact]
    public void All_ContainsSixUniqueRoleNames()
    {
        Assert.Equal(6, Roles.All.Count);
        Assert.Equal(Roles.All.Count, Roles.All.Distinct().Count());
    }

    [Fact]
    public void All_ContainsAdministratorRole()
    {
        Assert.Contains(Roles.Administrator, Roles.All);
    }
}
