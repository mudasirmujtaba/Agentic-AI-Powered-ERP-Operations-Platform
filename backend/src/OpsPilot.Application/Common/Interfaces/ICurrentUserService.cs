namespace OpsPilot.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    IReadOnlyList<string> Roles { get; }
}
