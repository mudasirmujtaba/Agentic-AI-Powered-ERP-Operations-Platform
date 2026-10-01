using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<Invoice> Invoices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
