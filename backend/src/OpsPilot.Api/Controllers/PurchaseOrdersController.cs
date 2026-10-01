using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Purchasing;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/purchase-orders")]
public class PurchaseOrdersController(IPurchaseOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PurchaseOrderListItemDto>> List([FromQuery] PurchaseOrderQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<PurchaseOrderDto> Get(Guid id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(SavePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var po = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = po.Id }, po);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<PurchaseOrderDto> Update(Guid id, SavePurchaseOrderRequest request, CancellationToken cancellationToken) =>
        orders.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<PurchaseOrderDto> Submit(Guid id, CancellationToken cancellationToken) => orders.SubmitAsync(id, cancellationToken);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.ApprovePurchaseOrders)]
    public Task<PurchaseOrderDto> Approve(Guid id, CancellationToken cancellationToken) => orders.ApproveAsync(id, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.ApprovePurchaseOrders)]
    public Task<PurchaseOrderDto> Reject(Guid id, RejectPurchaseOrderRequest request, CancellationToken cancellationToken) =>
        orders.RejectAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/mark-ordered")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<PurchaseOrderDto> MarkOrdered(Guid id, CancellationToken cancellationToken) => orders.MarkOrderedAsync(id, cancellationToken);

    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = Policies.ReceiveGoods)]
    public Task<PurchaseOrderDto> Receive(Guid id, ReceiveGoodsRequest request, CancellationToken cancellationToken) =>
        orders.ReceiveAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.ManagePurchaseOrders)]
    public Task<PurchaseOrderDto> Cancel(Guid id, CancellationToken cancellationToken) => orders.CancelAsync(id, cancellationToken);
}
