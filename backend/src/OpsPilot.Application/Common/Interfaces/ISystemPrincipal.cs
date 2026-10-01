namespace OpsPilot.Application.Common.Interfaces;

/// <summary>
/// The identity background jobs act as when they call the AI service: a dedicated, non-interactive account with a
/// short-lived token, so the agents read the ERP under ordinary role checks rather than a bypass.
/// </summary>
public interface ISystemPrincipal
{
    Task<SystemIdentity> GetAsync(CancellationToken cancellationToken = default);
}

public record SystemIdentity(Guid UserId, string Email, string Name, IReadOnlyList<string> Roles, string AccessToken);

/// <summary>Resolves who should hear about something, by role.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<Guid>> ActiveUserIdsInRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default);
}
