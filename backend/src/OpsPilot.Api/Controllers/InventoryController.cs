using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Inventory;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("stock")]
    public Task<PagedResult<StockLevelDto>> Stock([FromQuery] StockQuery query, CancellationToken cancellationToken) =>
        inventory.ListStockAsync(query, cancellationToken);

    [HttpGet("stock/{productId:guid}")]
    public Task<ProductStockDto> ProductStock(Guid productId, CancellationToken cancellationToken) =>
        inventory.GetProductStockAsync(productId, cancellationToken);

    [HttpGet("transactions")]
    public Task<PagedResult<InventoryTransactionDto>> Transactions([FromQuery] InventoryTransactionQuery query, CancellationToken cancellationToken) =>
        inventory.ListTransactionsAsync(query, cancellationToken);

    [HttpPost("adjustments")]
    [Authorize(Policy = Policies.ManageInventory)]
    public Task<ProductStockDto> Adjust(AdjustStockRequest request, CancellationToken cancellationToken) =>
        inventory.AdjustAsync(request, cancellationToken);

    [HttpPost("transfers")]
    [Authorize(Policy = Policies.ManageInventory)]
    public Task<ProductStockDto> Transfer(TransferStockRequest request, CancellationToken cancellationToken) =>
        inventory.TransferAsync(request, cancellationToken);
}
