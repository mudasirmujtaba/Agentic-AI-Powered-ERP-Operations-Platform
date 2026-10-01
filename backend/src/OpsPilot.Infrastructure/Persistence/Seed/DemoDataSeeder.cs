using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpsPilot.Domain.Catalog;
using OpsPilot.Domain.Customers;
using OpsPilot.Domain.Identity;
using OpsPilot.Domain.Inventory;
using OpsPilot.Domain.Suppliers;

namespace OpsPilot.Infrastructure.Persistence.Seed;

/// <summary>Development-only sample data so screens and, later, the AI agents have realistic records to work with.</summary>
public class DemoDataSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<DemoDataSeeder> logger)
{
    private static readonly (string Email, string FirstName, string LastName, string Role)[] DemoUsers =
    [
        ("manager@opspilot.local", "Morgan", "Reyes", Roles.Manager),
        ("sales@opspilot.local", "Sam", "Patel", Roles.SalesUser),
        ("inventory@opspilot.local", "Ivy", "Chen", Roles.InventoryManager),
        ("procurement@opspilot.local", "Paulo", "Silva", Roles.ProcurementUser),
        ("finance@opspilot.local", "Fatima", "Khan", Roles.FinanceUser),
    ];

    public async Task SeedAsync()
    {
        await SeedDemoUsersAsync();

        if (await db.Categories.AnyAsync())
        {
            logger.LogInformation("Demo master data already present, skipping.");
        }
        else
        {
            await SeedMasterDataAsync();
        }

        if (await db.InventoryItems.AnyAsync())
        {
            logger.LogInformation("Demo operational data already present, skipping.");
            return;
        }

        var (orders, purchaseOrders, invoices) = await new DemoOperationsSeeder(db).SeedAsync();
        logger.LogInformation(
            "Seeded demo operations: {Orders} sales orders, {PurchaseOrders} purchase orders, {Invoices} invoices.",
            orders, purchaseOrders, invoices);
    }

    private async Task SeedMasterDataAsync()
    {
        var suppliers = BuildSuppliers();
        var categories = BuildCategories();
        var products = BuildProducts(categories, suppliers);

        db.Suppliers.AddRange(suppliers.Values);
        db.Categories.AddRange(categories.Values);
        db.Products.AddRange(products);
        db.Customers.AddRange(BuildCustomers());
        db.Warehouses.AddRange(BuildWarehouses());

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Seeded demo master data: {Categories} categories, {Products} products, {Suppliers} suppliers.",
            categories.Count, products.Count, suppliers.Count);
    }

    private async Task SeedDemoUsersAsync()
    {
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:AdminPassword is not configured, skipping demo users.");
            return;
        }

        foreach (var (email, firstName, lastName, role) in DemoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to seed demo user {Email}: {Errors}",
                    email, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Created demo user {Email} ({Role})", email, role);
        }
    }

    private static Dictionary<string, Supplier> BuildSuppliers()
    {
        Supplier S(string code, string name, string contact, string email, string phone, int leadTime, int terms = 30) =>
            new()
            {
                Code = code, Name = name, ContactName = contact, Email = email, Phone = phone,
                AverageLeadTimeDays = leadTime, PaymentTermsDays = terms,
            };

        return new[]
        {
            S("SUP-ABC", "ABC Industrial Supplies", "Dana Whitfield", "orders@abc-industrial.example", "+1 312 555 0141", 7),
            S("SUP-NOR", "Northwind Components", "Lars Eriksen", "sales@northwind-components.example", "+1 206 555 0199", 10),
            S("SUP-GLB", "Global Safety Gear", "Renee Okafor", "support@globalsafety.example", "+1 404 555 0117", 5, 45),
            S("SUP-FST", "FastenRight Hardware", "Tom Brennan", "orders@fastenright.example", "+1 614 555 0102", 4),
            S("SUP-PRC", "Precision Tools Co.", "Akira Mori", "b2b@precisiontools.example", "+1 408 555 0165", 14, 60),
            S("SUP-VOL", "Volta Electric Supply", "Grace Nwosu", "orders@voltaelectric.example", "+1 713 555 0128", 9),
            S("SUP-HYD", "HydroFlow Systems", "Marco Bianchi", "sales@hydroflow.example", "+1 503 555 0133", 12),
            S("SUP-MRD", "Meridian Industrial", "Helen Park", "procurement@meridian.example", "+1 617 555 0150", 8),
        }.ToDictionary(s => s.Code);
    }

    private static Dictionary<string, Category> BuildCategories() =>
        new[]
        {
            new Category { Name = "Pumps & Valves", Description = "Industrial pumps, valves and flow control" },
            new Category { Name = "Electrical Components", Description = "Motors, breakers, contactors and controllers" },
            new Category { Name = "Safety Equipment", Description = "PPE and site safety supplies" },
            new Category { Name = "Fasteners & Hardware", Description = "Bolts, nuts, washers and fixings" },
            new Category { Name = "Tools & Machinery", Description = "Power tools and precision hand tools" },
        }.ToDictionary(c => c.Name);

    private static List<Product> BuildProducts(Dictionary<string, Category> categories, Dictionary<string, Supplier> suppliers)
    {
        Product P(string code, string name, string category, string supplier, decimal cost, decimal price, int reorderPoint, int safetyStock) =>
            new()
            {
                Code = code, Name = name, Category = categories[category], PrimarySupplier = suppliers[supplier],
                Cost = cost, UnitPrice = price, ReorderPoint = reorderPoint, SafetyStock = safetyStock,
            };

        return
        [
            P("X200", "Industrial Pump X200", "Pumps & Valves", "SUP-ABC", 125.00m, 189.00m, 100, 35),
            P("X100", "Industrial Pump X100", "Pumps & Valves", "SUP-ABC", 95.00m, 149.00m, 60, 20),
            P("X300", "Industrial Pump X300 Heavy Duty", "Pumps & Valves", "SUP-HYD", 210.00m, 319.00m, 40, 15),
            P("VB-050", "Ball Valve 2\"", "Pumps & Valves", "SUP-HYD", 18.00m, 32.00m, 200, 80),
            P("VG-100", "Gate Valve 4\"", "Pumps & Valves", "SUP-HYD", 42.00m, 76.00m, 120, 40),
            P("PS-220", "Pressure Sensor PS-220", "Electrical Components", "SUP-NOR", 34.00m, 59.00m, 150, 50),
            P("EM-750", "Electric Motor 7.5kW", "Electrical Components", "SUP-VOL", 480.00m, 699.00m, 25, 10),
            P("CB-16A", "Circuit Breaker 16A", "Electrical Components", "SUP-VOL", 9.50m, 17.90m, 300, 100),
            P("CT-40A", "Contactor 40A", "Electrical Components", "SUP-VOL", 22.00m, 39.00m, 180, 60),
            P("PLC-S7", "PLC Controller Module", "Electrical Components", "SUP-NOR", 310.00m, 459.00m, 30, 10),
            P("HH-01", "Safety Helmet", "Safety Equipment", "SUP-GLB", 6.50m, 14.99m, 400, 150),
            P("GL-NT", "Nitrile Work Gloves (box of 100)", "Safety Equipment", "SUP-GLB", 8.00m, 15.50m, 500, 200),
            P("SG-02", "Safety Goggles", "Safety Equipment", "SUP-GLB", 4.20m, 9.90m, 300, 100),
            P("HV-XL", "Hi-Vis Vest", "Safety Equipment", "SUP-GLB", 3.80m, 8.50m, 250, 80),
            P("BT-M10", "Hex Bolt M10 (pack of 50)", "Fasteners & Hardware", "SUP-FST", 5.50m, 11.00m, 600, 200),
            P("NT-M10", "Hex Nut M10 (pack of 100)", "Fasteners & Hardware", "SUP-FST", 3.20m, 7.00m, 600, 200),
            P("WS-M10", "Flat Washer M10 (pack of 100)", "Fasteners & Hardware", "SUP-FST", 1.90m, 4.50m, 500, 150),
            P("DR-18V", "Cordless Drill 18V", "Tools & Machinery", "SUP-PRC", 72.00m, 129.00m, 40, 15),
            P("AG-115", "Angle Grinder 115mm", "Tools & Machinery", "SUP-PRC", 48.00m, 89.00m, 35, 12),
            P("TW-100", "Torque Wrench 10-100Nm", "Tools & Machinery", "SUP-MRD", 55.00m, 98.00m, 30, 10),
        ];
    }

    private static List<Customer> BuildCustomers()
    {
        Customer C(int number, string name, string contact, string city, string state, decimal creditLimit,
            CustomerStatus status = CustomerStatus.Active, int terms = 30)
        {
            var slug = new string(name.ToLowerInvariant().Where(char.IsLetter).Take(12).ToArray());
            return new Customer
            {
                Code = $"CUST-{number}",
                Name = name,
                ContactName = contact,
                Email = $"purchasing@{slug}.example",
                Phone = $"+1 555 01{number % 100:00}",
                CreditLimit = creditLimit,
                PaymentTermsDays = terms,
                Status = status,
                Addresses =
                [
                    new CustomerAddress { Type = AddressType.Billing, Line1 = $"{100 + number} Commerce Way", City = city, State = state, PostalCode = $"{10000 + number * 7}", Country = "United States", IsDefault = true },
                    new CustomerAddress { Type = AddressType.Shipping, Line1 = $"{number} Industrial Park Rd", City = city, State = state, PostalCode = $"{10000 + number * 7}", Country = "United States", IsDefault = true },
                ],
            };
        }

        return
        [
            C(1001, "Apex Manufacturing", "Rachel Moore", "Detroit", "MI", 150_000m),
            C(1002, "BlueRiver Utilities", "Kevin Ortiz", "Portland", "OR", 250_000m, terms: 45),
            C(1003, "Cascade Construction", "Emily Zhao", "Seattle", "WA", 120_000m),
            C(1004, "Delta Fabrication", "Omar Haddad", "Houston", "TX", 90_000m),
            C(1005, "Evergreen Facilities", "Laura Svensson", "Denver", "CO", 60_000m),
            C(1006, "Frontier Mining Co.", "Jack Dawson", "Phoenix", "AZ", 300_000m, terms: 60),
            C(1007, "Granite Industrial Services", "Priya Nair", "Pittsburgh", "PA", 80_000m),
            C(1008, "Harbor Logistics", "Chris Adams", "Baltimore", "MD", 70_000m),
            C(1009, "Ironclad Engineering", "Nina Petrova", "Cleveland", "OH", 110_000m),
            C(1010, "Juniper Water Authority", "Daniel Kim", "Sacramento", "CA", 200_000m, terms: 45),
            C(1011, "Keystone Plant Services", "Sofia Rossi", "Harrisburg", "PA", 50_000m),
            C(1012, "Lakeside Refinery", "Ahmed Saleh", "Chicago", "IL", 180_000m, CustomerStatus.OnHold),
            C(1013, "Metro Transit Maintenance", "Beth Collins", "Atlanta", "GA", 40_000m, CustomerStatus.Inactive),
            C(1014, "Northstar Energy", "Victor Lindqvist", "Minneapolis", "MN", 220_000m),
            C(1015, "Orion Food Processing", "Mei Tanaka", "Omaha", "NE", 65_000m),
        ];
    }

    private static List<Warehouse> BuildWarehouses() =>
    [
        new Warehouse { Code = "WH-MAIN", Name = "Main Distribution Center", Location = "Chicago, IL" },
        new Warehouse { Code = "WH-EAST", Name = "East Coast Depot", Location = "Newark, NJ" },
        new Warehouse { Code = "WH-WEST", Name = "West Coast Depot", Location = "Reno, NV" },
    ];
}
