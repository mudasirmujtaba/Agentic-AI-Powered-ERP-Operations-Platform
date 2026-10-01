using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Domain.Ai;
using OpsPilot.Domain.Audit;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiAction> AiActions => Set<AiAction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
