using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.ViewAuditLog)]
[Route("api/audit-logs")]
public class AuditLogsController(IAuditLogService auditLogs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> List([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
        auditLogs.ListAsync(query, cancellationToken);
}
