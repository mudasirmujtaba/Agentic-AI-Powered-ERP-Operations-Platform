using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Jobs;
using OpsPilot.Application.Notifications;
using OpsPilot.Domain.Insights;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<NotificationFeedDto> Feed([FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        notifications.GetFeedAsync(take, cancellationToken);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}

/// <summary>Scheduled jobs (design doc §40): status for managers, and "run now".</summary>
[ApiController]
[Authorize(Policy = Policies.ManageJobs)]
[Route("api/jobs")]
public class JobsController(IJobScheduler scheduler) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<JobStatusDto>> List(CancellationToken cancellationToken) => scheduler.ListAsync(cancellationToken);

    [HttpPost("{id}/run")]
    public IActionResult Run(string id) => Accepted(new { runId = scheduler.Trigger(id) });
}

[ApiController]
[Authorize]
[Route("api/insights")]
public class InsightsController(IInsightService insights) : ControllerBase
{
    /// <summary>The latest nightly inventory risk report, or 204 when no scan has run yet.</summary>
    [HttpGet("inventory-risk/latest")]
    public async Task<ActionResult<InsightReportDto>> LatestInventoryRisk(CancellationToken cancellationToken) =>
        await insights.GetLatestAsync(InsightKinds.InventoryRisk, cancellationToken) is { } report ? report : NoContent();
}
