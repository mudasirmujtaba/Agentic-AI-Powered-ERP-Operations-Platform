using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Products;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController(IProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductListItemDto>> List([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        products.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken cancellationToken) =>
        products.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageCatalog)]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageCatalog)]
    public Task<ProductDto> Update(Guid id, SaveProductRequest request, CancellationToken cancellationToken) =>
        products.UpdateAsync(id, request, cancellationToken);
}
