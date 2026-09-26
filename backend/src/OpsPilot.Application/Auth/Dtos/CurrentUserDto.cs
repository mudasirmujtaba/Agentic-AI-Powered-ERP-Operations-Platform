namespace OpsPilot.Application.Auth.Dtos;

public record CurrentUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);
