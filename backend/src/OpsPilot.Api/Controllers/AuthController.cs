using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Auth;
using OpsPilot.Application.Auth.Dtos;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        if (!result.Succeeded || result.Response is null)
        {
            return Unauthorized(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserDto> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var firstName = User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
        var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (id is null || email is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserDto(Guid.Parse(id), email, firstName, lastName, roles));
    }
}
