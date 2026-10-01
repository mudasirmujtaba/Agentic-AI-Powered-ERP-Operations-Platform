using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Warehouses;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/warehouses")]
public class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<WarehouseDto>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        warehouses.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<WarehouseDto> Get(Guid id, CancellationToken cancellationToken) =>
        warehouses.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageWarehouses)]
    public async Task<ActionResult<WarehouseDto>> Create(SaveWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await warehouses.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = warehouse.Id }, warehouse);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageWarehouses)]
    public Task<WarehouseDto> Update(Guid id, SaveWarehouseRequest request, CancellationToken cancellationToken) =>
        warehouses.UpdateAsync(id, request, cancellationToken);
}
