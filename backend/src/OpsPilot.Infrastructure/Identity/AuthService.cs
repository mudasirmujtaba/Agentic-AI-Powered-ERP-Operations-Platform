using Microsoft.AspNetCore.Identity;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Auth.Dtos;
using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Identity;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return new AuthResult(false, null, "Invalid email or password.");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return new AuthResult(false, null, "Invalid email or password.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtTokenService.GenerateAccessToken(user, roles);

        var currentUser = new CurrentUserDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());
        var response = new LoginResponse(token.AccessToken, token.ExpiresAtUtc, currentUser);

        return new AuthResult(true, response, null);
    }
}
