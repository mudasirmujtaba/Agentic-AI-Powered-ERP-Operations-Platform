using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Categories;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CategoryDto>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        categories.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CategoryDto> Get(Guid id, CancellationToken cancellationToken) =>
        categories.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageCatalog)]
    public async Task<ActionResult<CategoryDto>> Create(SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categories.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageCatalog)]
    public Task<CategoryDto> Update(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken) =>
        categories.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.ManageCatalog)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await categories.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
