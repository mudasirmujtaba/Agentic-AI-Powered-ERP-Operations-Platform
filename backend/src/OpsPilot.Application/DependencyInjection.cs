using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Ai;
using OpsPilot.Application.Audit;
using OpsPilot.Application.Categories;
using OpsPilot.Application.Customers;
using OpsPilot.Application.Dashboard;
using OpsPilot.Application.Finance;
using OpsPilot.Application.Inventory;
using OpsPilot.Application.Jobs;
using OpsPilot.Application.Notifications;
using OpsPilot.Application.Purchasing;
using OpsPilot.Application.Sales;
using OpsPilot.Application.Service;
using OpsPilot.Application.Products;
using OpsPilot.Application.Reports;
using OpsPilot.Application.Suppliers;
using OpsPilot.Application.Warehouses;

namespace OpsPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<StockLedger>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<AuditLogWriter>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAiCopilotService, AiCopilotService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<IInsightService, InsightService>();
        services.AddScoped<InventoryRiskScanJob>();
        services.AddScoped<OverdueInvoiceReminderJob>();
        services.AddScoped<CreditHoldReviewJob>();

        return services;
    }
}
