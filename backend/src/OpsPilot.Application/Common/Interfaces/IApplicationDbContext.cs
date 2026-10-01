using Microsoft.EntityFrameworkCore;
using OpsPilot.Domain.Ai;
using OpsPilot.Domain.Audit;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Finance;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Insights;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Notifications;
using OpsPilot.Domain.Purchasing;
using OpsPilot.Domain.Sales;
using OpsPilot.Domain.Service;
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
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiAction> AiActions { get; }
    DbSet<ServiceTicket> ServiceTickets { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<InsightReport> InsightReports { get; }

    /// <summary>Identity users, read-only use: assignee lookups. Account changes go through Identity's UserManager.</summary>
    DbSet<ApplicationUser> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
