# OpsPilot

An enterprise ERP platform built with **Angular 21**, **ASP.NET Core 10** and **SQL Server**, structured to take an
agentic AI layer next (see [Roadmap](#roadmap)).

It covers the operational core of a distribution business: master data, inventory, sales order fulfilment,
purchasing with approvals, invoicing and payments, and a KPI dashboard. Every role sees and can do only what
its job allows.

## Features

| Area | What you can do |
|---|---|
| **Dashboard** | Revenue (30 days, year to date), open and late orders, products to reorder, receivables and overdue invoices, pending purchase approvals, a revenue-by-month chart, top customers. Each KPI links to the filtered list behind it. |
| **Master data** | Customers (with billing/shipping addresses, credit limits, payment terms), suppliers (lead times), products and categories (price, cost, reorder point, safety stock), warehouses. Server-side search, sort and paging everywhere. |
| **Inventory** | Stock per product per warehouse: on hand, reserved for open orders, and available. Adjustments (count, damage, returns), transfers between warehouses, a full movement ledger, and low-stock status. |
| **Sales orders** | Draft → Confirmed → Processing → Shipped → Delivered (or Cancelled). Confirming checks the customer's status and **credit limit** and **reserves stock**. Shipping deducts it; cancelling releases it. Late orders are flagged. |
| **Purchase orders** | Draft → Pending approval → Approved → Ordered → Partially received → Completed. Orders over **$10,000 require a manager's approval**; smaller ones are approved on submit. Receiving goods posts stock into the receiving warehouse. |
| **Invoicing** | Invoice a shipped order, issue it (the due date comes from the customer's payment terms), and record full or partial payments. Overdue status is computed from the due date. |

Business rules live in the domain model and are enforced on the server. A violation returns
HTTP 422 with a readable reason, for example
*"Confirming this order would put Keystone Plant Services over their credit limit: existing exposure 0.00 + order 349,500.00 > limit 50,000.00."*

## Architecture

```
frontend/   Angular 21 (standalone components, signals, zoneless), Angular Material + Tailwind
backend/
  src/OpsPilot.Domain          Entities, state machines and invariants (no framework dependencies beyond Identity types)
  src/OpsPilot.Application     Use-case services, DTOs, FluentValidation validators, authorization policies
  src/OpsPilot.Infrastructure  EF Core (SQL Server), Identity, JWT, migrations, audit interceptor, demo seeding
  src/OpsPilot.Api             Controllers, ProblemDetails error mapping, composition root
  tests/OpsPilot.UnitTests     xUnit tests against a real relational database (SQLite in-memory)
```

- **Clean Architecture.** Dependencies point inward. Controllers are thin, services orchestrate, and the domain decides.
- **Stock integrity.** All quantity changes go through one `StockLedger`, which writes a ledger entry for every
  movement. A database check constraint guarantees `0 ≤ reserved ≤ on hand`.
- **Security.** ASP.NET Core Identity with JWT bearer tokens. Role-based policies mirror the design document's
  permission matrix, and the UI hides actions the user can't perform (the API enforces them regardless). Secrets
  live in .NET user secrets, never in the repository.
- **Auditability.** Every record carries created/updated timestamps and user ids, stamped automatically by an
  EF Core interceptor.
- **Errors.** One global handler maps validation errors (400, with per-field messages that the forms show inline),
  not found (404), conflicts such as duplicate codes (409) and business-rule violations (422).

## Running locally

**Prerequisites:** .NET SDK 10, Node 22+, SQL Server (any edition) with SQL authentication enabled, and the EF Core
CLI (`dotnet tool install --global dotnet-ef`).

**1. Configure secrets** (from `backend/`; replace the placeholders):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OpsPilotDb;User Id=<sql-user>;Password=<sql-password>;TrustServerCertificate=True;" --project src/OpsPilot.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project src/OpsPilot.Api
dotnet user-secrets set "Seed:AdminPassword" "<a strong password>" --project src/OpsPilot.Api
```

**2. Create the database and start the API:**

```powershell
dotnet ef database update --project src/OpsPilot.Infrastructure --startup-project src/OpsPilot.Api
dotnet dev-certs https --trust
dotnet run --project src/OpsPilot.Api --launch-profile https     # https://localhost:7170
```

In Development the API seeds realistic demo data on first start: 20 products, 15 customers, 8 suppliers,
3 warehouses, and six months of orders, purchase orders, invoices and payments.

**3. Start the frontend** (from `frontend/`):

```powershell
npm install
npm start                                                         # http://localhost:4200
```

**Tests:** `dotnet test` in `backend/` (27 tests) and `npm test -- --watch=false` in `frontend/`.

## Demo accounts

All accounts use the password you set in `Seed:AdminPassword`.

| Email | Role | Can |
|---|---|---|
| `admin@opspilot.local` | Administrator | Everything |
| `manager@opspilot.local` | Manager | Everything operational, including approving large purchase orders |
| `sales@opspilot.local` | Sales | Customers and sales orders |
| `inventory@opspilot.local` | Inventory manager | Catalog, stock adjustments and transfers, shipping orders, receiving goods |
| `procurement@opspilot.local` | Procurement | Suppliers and purchase orders (cannot approve them) |
| `finance@opspilot.local` | Finance | Invoices and payments; read-only elsewhere |

## Five-minute demo

1. **Dashboard** (as admin). Point out the late orders, the products to reorder, and the overdue receivables.
   Click *Products to reorder*: **X200 Industrial Pump** has 43 on hand against a reorder point of 100.
2. **Inventory → X200.** Stock by warehouse, the reservation held by open orders, and the movement ledger.
   Under *Purchase orders* there's a 100-unit order from ABC Industrial Supplies, due in 12 days.
3. **Order to cash.** In *Sales*, create a new order for Apex Manufacturing, then *Confirm* it (stock is reserved),
   *Start processing*, *Ship* (stock is deducted), *Create invoice*, *Issue*, and *Record payment*. The invoice
   moves to Paid.
4. **Rules.** Create an order for 500 × EM-750 for Keystone Plant Services and confirm it. It's rejected with
   the credit-limit explanation.
5. **Approvals.** Sign in as `procurement@`, create a purchase order for 30 × EM-750 ($14,400) and *Submit*. It
   waits for approval, and procurement has no Approve button. Sign in as `manager@`, approve it, mark it as sent,
   and receive the goods. The stock appears in inventory.
6. **Roles.** Sign in as `finance@`. Customers are read-only, and invoices are fully actionable.

## Roadmap

The design document's next phases build on this foundation:

- **AI Copilot.** A Python/LangGraph service: natural-language ERP questions over read-only SQL views,
  order-delay investigation, and inventory-risk purchase recommendations that go through a human approval step
  before becoming PO drafts. Planned LLM provider: Groq.
- **RAG** over company policies (for example, the purchasing approval policy) with cited answers.
- **Audit log** of consequential actions, built on the existing created/updated-by stamps.
- **Production engineering.** Docker Compose, CI/CD, scheduled jobs (Hangfire) for overdue notices and
  stock-risk scans, refresh tokens, optimistic concurrency on stock rows, OpenTelemetry.

## Troubleshooting

- **`An Application Control policy has blocked this file`** when running `dotnet ef`, `dotnet run` or `dotnet test`:
  Windows Smart App Control is blocking the freshly built, unsigned assemblies. Turn it off under
  *Windows Security → App & browser control → Smart App Control*.
- **SQL error 26 (`Error Locating Server/Instance`)**: point the connection string at the instance that is
  actually running. For a default instance that's `Server=localhost`, not `localhost\SQLEXPRESS`.
- **Login works in curl but not the browser**: the API's CORS policy allows `http://localhost:4200` only.
