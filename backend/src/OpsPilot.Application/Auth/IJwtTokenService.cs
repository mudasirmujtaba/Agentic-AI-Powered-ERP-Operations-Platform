using OpsPilot.Domain.Identity;

namespace OpsPilot.Application.Auth;

public record AccessTokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(ApplicationUser user, IList<string> roles);
}
