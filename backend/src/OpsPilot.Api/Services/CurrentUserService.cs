using System.Security.Claims;
using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => User?.FindFirstValue(ClaimTypes.Email);

    public string? Name
    {
        get
        {
            var name = $"{User?.FindFirstValue(ClaimTypes.GivenName)} {User?.FindFirstValue(ClaimTypes.Surname)}".Trim();
            return name.Length > 0 ? name : Email;
        }
    }

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}
