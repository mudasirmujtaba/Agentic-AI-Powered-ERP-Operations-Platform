using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Warehouse> Warehouses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
