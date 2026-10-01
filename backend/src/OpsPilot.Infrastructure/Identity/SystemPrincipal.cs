using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.Persistence;

namespace OpsPilot.Infrastructure.Identity;

/// <summary>
/// The automation account background jobs use. It has no password (so it can't sign in) and only the
/// InventoryManager role, so its token reads the ERP like any inventory manager and can't touch finance data.
/// </summary>
public class SystemPrincipal(UserManager<ApplicationUser> userManager, IJwtTokenService tokens) : ISystemPrincipal
{
    private static readonly string[] AccountRoles = [Roles.InventoryManager];

    public async Task<SystemIdentity> GetAsync(CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(SystemAccounts.AutomationEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = SystemAccounts.AutomationEmail,
                Email = SystemAccounts.AutomationEmail,
                FirstName = "OpsPilot",
                LastName = "Automation",
                EmailConfirmed = true,
            };
            EnsureSucceeded(await userManager.CreateAsync(user));
        }

        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in AccountRoles.Except(roles))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        }
        roles = await userManager.GetRolesAsync(user);

        var token = tokens.GenerateAccessToken(user, roles);
        return new SystemIdentity(user.Id, user.Email!, $"{user.FirstName} {user.LastName}", roles.ToList(), token.AccessToken);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not prepare the automation account: " +
                                                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}

public class UserDirectory(ApplicationDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyList<Guid>> ActiveUserIdsInRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var names = roles.ToList();
        return await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                join user in db.Users on userRole.UserId equals user.Id
                where names.Contains(role.Name!) && user.IsActive && user.Email != SystemAccounts.AutomationEmail
                select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
