using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Sales;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales-orders")]
public class SalesOrdersController(ISalesOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SalesOrderListItemDto>> List([FromQuery] SalesOrderQuery query, CancellationToken cancellationToken) =>
        orders.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<SalesOrderDto> Get(Guid id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageSalesOrders)]
    public async Task<ActionResult<SalesOrderDto>> Create(SaveSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageSalesOrders)]
    public Task<SalesOrderDto> Update(Guid id, SaveSalesOrderRequest request, CancellationToken cancellationToken) =>
        orders.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Policies.ManageSalesOrders)]
    public Task<SalesOrderDto> Confirm(Guid id, CancellationToken cancellationToken) => orders.ConfirmAsync(id, cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.ManageSalesOrders)]
    public Task<SalesOrderDto> Cancel(Guid id, CancellationToken cancellationToken) => orders.CancelAsync(id, cancellationToken);

    [HttpPost("{id:guid}/start-processing")]
    [Authorize(Policy = Policies.FulfilSalesOrders)]
    public Task<SalesOrderDto> StartProcessing(Guid id, CancellationToken cancellationToken) =>
        orders.StartProcessingAsync(id, cancellationToken);

    [HttpPost("{id:guid}/ship")]
    [Authorize(Policy = Policies.FulfilSalesOrders)]
    public Task<SalesOrderDto> Ship(Guid id, ShipSalesOrderRequest request, CancellationToken cancellationToken) =>
        orders.ShipAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Policy = Policies.FulfilSalesOrders)]
    public Task<SalesOrderDto> Deliver(Guid id, CancellationToken cancellationToken) => orders.DeliverAsync(id, cancellationToken);
}
