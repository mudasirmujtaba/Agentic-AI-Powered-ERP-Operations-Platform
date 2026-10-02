using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Reports;

namespace OpsPilot.Api.Controllers;

/// <summary>Business reports (design doc §48). Access is per report: each function sees its own domain.</summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(IReportService reports, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The reports the caller may open, for the navigation.</summary>
    [HttpGet]
    public IReadOnlyList<string> Available() => ReportKeys.All.Where(k => ReportKeys.CanRead(k, currentUser.Roles)).ToList();

    [HttpGet("{key}")]
    public async Task<ActionResult<ReportDto>> Get(string key, [FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        if (!ReportKeys.All.Contains(key)) return NotFound();
        if (!ReportKeys.CanRead(key, currentUser.Roles)) return Forbid();
        return await reports.GetAsync(key, query, cancellationToken);
    }
}
