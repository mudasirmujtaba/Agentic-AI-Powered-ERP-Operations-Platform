namespace OpsPilot.Application.Auth.Dtos;

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserDto User);
