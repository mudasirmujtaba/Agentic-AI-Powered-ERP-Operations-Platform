using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Dashboard;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardSummaryDto> Get(CancellationToken cancellationToken) => dashboard.GetSummaryAsync(cancellationToken);
}
