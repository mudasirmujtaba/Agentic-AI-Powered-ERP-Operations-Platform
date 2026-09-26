using OpsPilot.Application.Auth.Dtos;

namespace OpsPilot.Application.Auth;

public record AuthResult(bool Succeeded, LoginResponse? Response, string? Error);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password);
}
