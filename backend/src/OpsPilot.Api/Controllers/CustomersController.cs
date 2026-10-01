using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsPilot.Application.Common.Models;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Customers;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<CustomerListItemDto>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken) =>
        customers.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CustomerDto> Get(Guid id, CancellationToken cancellationToken) =>
        customers.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageCustomers)]
    public async Task<ActionResult<CustomerDto>> Create(SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await customers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageCustomers)]
    public Task<CustomerDto> Update(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken) =>
        customers.UpdateAsync(id, request, cancellationToken);
}
