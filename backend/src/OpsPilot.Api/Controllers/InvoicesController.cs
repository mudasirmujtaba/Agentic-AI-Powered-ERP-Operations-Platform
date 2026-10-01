using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Finance;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceListItemDto>> List([FromQuery] InvoiceQuery query, CancellationToken cancellationToken) =>
        invoices.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<InvoiceDto> Get(Guid id, CancellationToken cancellationToken) => invoices.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageFinance)]
    public async Task<ActionResult<InvoiceDto>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var invoice = await invoices.CreateFromOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = invoice.Id }, invoice);
    }

    [HttpPost("{id:guid}/issue")]
    [Authorize(Policy = Policies.ManageFinance)]
    public Task<InvoiceDto> Issue(Guid id, CancellationToken cancellationToken) => invoices.IssueAsync(id, cancellationToken);

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = Policies.ManageFinance)]
    public Task<InvoiceDto> RecordPayment(Guid id, RecordPaymentRequest request, CancellationToken cancellationToken) =>
        invoices.RecordPaymentAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.ManageFinance)]
    public Task<InvoiceDto> Cancel(Guid id, CancellationToken cancellationToken) => invoices.CancelAsync(id, cancellationToken);
}
