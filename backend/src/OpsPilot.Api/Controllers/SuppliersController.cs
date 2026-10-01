using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Suppliers;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers")]
public class SuppliersController(ISupplierService suppliers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<SupplierListItemDto>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        suppliers.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<SupplierDto> Get(Guid id, CancellationToken cancellationToken) =>
        suppliers.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageSuppliers)]
    public async Task<ActionResult<SupplierDto>> Create(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = supplier.Id }, supplier);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageSuppliers)]
    public Task<SupplierDto> Update(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken) =>
        suppliers.UpdateAsync(id, request, cancellationToken);
}
