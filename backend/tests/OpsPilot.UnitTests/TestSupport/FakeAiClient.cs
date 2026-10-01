using OpsPilot.Application.Ai;
using OpsPilot.Application.Common.Interfaces;

namespace OpsPilot.UnitTests.TestSupport;

/// <summary>Stands in for the Python AI service; records what it was sent.</summary>
public sealed class FakeAiClient : IAiAgentClient
{
    public TicketSummaryInput? LastSummaryInput { get; private set; }
    public AgentJobRequest? LastJobRequest { get; private set; }
    public InventoryScanReply ScanReply { get; set; } = new("No risks.", true, 0, [], null);

    public Task<AgentReply> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AgentReply> ResumeAsync(AgentResumeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AiTextReply> SummarizeTicketAsync(TicketSummaryInput input, CancellationToken cancellationToken = default)
    {
        LastSummaryInput = input;
        return Task.FromResult(new AiTextReply($"summary of {input.TicketNumber}", null));
    }

    public Task<AiTextReply> FindRecurringProblemsAsync(TicketInsightsInput input, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AiTextReply($"{input.Tickets.Count} tickets", null));

    public Task<InventoryScanReply> ScanInventoryAsync(AgentJobRequest request, CancellationToken cancellationToken = default)
    {
        LastJobRequest = request;
        return Task.FromResult(ScanReply);
    }
}

public sealed class FakeSystemPrincipal : ISystemPrincipal
{
    public Task<SystemIdentity> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SystemIdentity(Guid.NewGuid(), "automation@opspilot.local", "OpsPilot Automation", ["InventoryManager"], "system-token"));
}

/// <summary>Returns a fixed set of recipients for any roles asked about.</summary>
public sealed class FakeUserDirectory(params Guid[] users) : IUserDirectory
{
    public List<string> AskedRoles { get; } = [];

    public Task<IReadOnlyList<Guid>> ActiveUserIdsInRolesAsync(IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        AskedRoles.AddRange(roles);
        return Task.FromResult<IReadOnlyList<Guid>>(users);
    }
}
