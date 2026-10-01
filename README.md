# OpsPilot

An enterprise ERP platform with an agentic AI Copilot. It's built with **Angular 21**, **ASP.NET Core 10**,
**SQL Server**, and a **Python / LangGraph** agent service using Groq-hosted models.

OpsPilot runs the operational core of a distribution business: master data, inventory, sales order fulfilment,
purchasing with approvals, invoicing and payments, plus a KPI dashboard. On top of that sits a Copilot that
answers questions from live ERP data, investigates late orders, assesses stock-out risk, cites company policy,
and drafts purchase orders that a person must approve before anything is created.

> The AI is an intelligent layer over the ERP, not a replacement for it. The ERP keeps responsibility for data
> integrity, business rules, transactions and authorization, and humans approve consequential actions.

## Features

| Area | What you can do |
|---|---|
| **Dashboard** | Revenue (30 days, year to date), open and late orders, products to reorder, receivables and overdue invoices, pending purchase approvals, a revenue-by-month chart, top customers. Each KPI links to the filtered list behind it. |
| **Master data** | Customers (with addresses, credit limits, payment terms), suppliers (lead times), products and categories (price, cost, reorder point, safety stock), warehouses. |
| **Inventory** | Stock per product per warehouse (on hand, reserved, available), adjustments, transfers, a movement ledger, and low-stock status. |
| **Sales orders** | Draft → Confirmed → Processing → Shipped → Delivered (or Cancelled). Confirming checks the customer's status and **credit limit** and **reserves stock**. Shipping deducts it; cancelling releases it. |
| **Purchase orders** | Draft → Pending approval → Approved → Ordered → Partially received → Completed. Orders over **$10,000 need a manager's approval**. Receiving goods posts stock. |
| **Invoicing** | Invoice a shipped order, issue it (the due date comes from payment terms), and record full or partial payments. Overdue status is computed. |
| **Service tickets** | Customer tickets with priority, category, assignee, an optional order and product, a comment thread with internal notes, and Open → In progress → Waiting on customer → Resolved → Closed (with reopen). Resolving requires a resolution. **AI summaries** of a ticket's history, and **AI recurring-problem analysis** across recent tickets. |
| **AI Copilot** | Chat over your ERP data, with data tables, cited policy answers, and a step-by-step trace of how each answer was produced. |
| **Approval center** | Every AI-proposed operation in one place: approve, reject, or modify quantities before approving. |
| **Automation** | Scheduled jobs (Hangfire): a **nightly inventory risk scan** at 02:00 that runs the Inventory agent as a restricted automation account and stores an AI-written briefing; **payment reminders** at 7, 14 and 30 days overdue; and **credit holds** for invoices over 60 days overdue, released once the account is current (manual holds are never touched). Admins and managers can see job history and run jobs on demand. |
| **Notifications** | An in-app bell with role-targeted alerts from the jobs (stock risk to inventory, reminders and holds to finance and sales), linking to the relevant page. |
| **Audit log** | Every consequential action (confirmations, shipments, approvals, payments, stock adjustments, AI proposals and decisions), with who did it and whether AI was involved. |

### The Copilot's agents

| Ask… | Agent | How it works |
|---|---|---|
| "Which customers spent the most in the last 90 days?" | **ERP Query** | The model writes one T-SQL `SELECT`. The ERP validates it and runs it read-only, and the model summarises the rows it got back. |
| "Why is SO-10044 delayed?" | **Order Investigation** | Gathers the order, its lines, stock by warehouse, open purchase orders and the customer account, derives likely causes, and then explains them. |
| "Are we going to run out of X200?" | **Inventory Intelligence** | Combines 90-day sales velocity, available stock, incoming purchase orders and supplier lead times into days of cover and projected stock. |
| "Prepare purchase orders for anything at risk." | **Procurement** | Turns the risk analysis into one draft purchase order per supplier, then **pauses for human approval**. |
| "Who can approve purchases over $10,000?" | **Policy (RAG)** | Retrieves sections of the company policy documents and answers only from them, citing each source. |

### How the AI is kept safe

- **Least privilege.** The agents call the ERP with the signed-in user's own token, so a user who can't see
  invoices in OpsPilot can't see them through the Copilot either.
- **Read-only SQL, validated twice.** Generated SQL is parsed with Microsoft's T-SQL grammar. It must be a single
  `SELECT` over curated `ai.*` reporting views the user's roles allow. It then runs under a separate login that has
  `SELECT` on that schema and nothing else, with a 5-second timeout and a 200-row cap.
- **Human approval.** A proposal stops the LangGraph run (`interrupt`, checkpointed to SQLite). Nothing changes
  until someone with purchasing rights approves in OpsPilot.
- **Business rules always apply.** Approved proposals are executed by the same purchasing service the UI uses.
  The resulting drafts still follow the $10,000 approval threshold. The graph then resumes and reports what
  actually happened.
- **Grounded answers.** Investigation, inventory and procurement facts are computed deterministically from ERP
  data, and the model only explains them. If a tool fails, the Copilot says so rather than guessing.
- **Auditability and observability.** Proposals, approvals and executions are audit-logged as AI-assisted. Every
  answer carries a per-node and per-tool timing trace, token usage and the model used.

## Architecture

```
Angular (ERP UI, dashboard, Copilot, approval center)
   │  HTTPS + JWT
ASP.NET Core API ── Identity/JWT, role policies, business rules, audit log, AI gateway
   │        │
   │        └── HTTP + internal key ──▶ Python AI service (FastAPI + LangGraph, Groq)
   │                                      │  calls back with the user's JWT
   │◀─────────────────────────────────────┘  (ERP APIs, validated read-only SQL)
SQL Server ── dbo.* transactional tables, ai.* read-only reporting views
```

```
frontend/      Angular 21: standalone components, signals, zoneless; Angular Material + Tailwind
backend/
  src/OpsPilot.Domain          Entities, state machines, invariants
  src/OpsPilot.Application     Use-case services, DTOs, FluentValidation, policies, AI gateway
  src/OpsPilot.Infrastructure  EF Core, Identity/JWT, migrations, SQL validator, agent client, seeding
  src/OpsPilot.Api             Controllers, ProblemDetails errors, composition root
  tests/OpsPilot.UnitTests     xUnit against SQLite in-memory, plus SQL validator tests
ai-service/
  app/agents/    Router, ERP Query, Order Investigation, Inventory, Procurement, Policy, General
  app/tickets.py Ticket summaries and recurring-problem analysis
  evals/         Evaluation dataset and runner
  app/graph.py   LangGraph: intent → workflow → (approval interrupt → outcome)
  knowledge/     Policy documents indexed for RAG (local fastembed embeddings)
  tests/         pytest: risk maths, proposals, findings, routing, retrieval, interrupt/resume
```

Other engineering notes:
- **Stock ledger.** All stock goes through a single `StockLedger`; a check constraint keeps `0 ≤ reserved ≤ on hand`.
- **Error mapping.** Errors map to 400 (validation), 404, 409 (duplicates), 422 (business rules) and 503 (AI service down); client-aborted requests are not logged as server errors.
- **Dates.** All timestamps are stored and returned as UTC (`...Z`); the UI shows them in the viewer's time zone.
- **Resilience.** EF Core retries transient SQL failures. Scheduled jobs are idempotent: reminders track the stage sent, and holds record whether policy placed them.
- **Job security.** Jobs that call the AI service use a short-lived token for `automation@opspilot.local`, which has no password and only the InventoryManager role, so agents read the ERP under normal role checks. Hangfire's own dashboard (`/hangfire`) is local-only in Development.
- **Secrets.** Secrets never live in the repository: they're in .NET user secrets locally and in a gitignored `.env` for the AI service and Docker.

## Running locally

**Prerequisites:**
- .NET SDK 10
- Node 22+
- Python 3.10+ with [uv](https://docs.astral.sh/uv/)
- SQL Server with SQL authentication enabled
- the EF Core CLI (`dotnet tool install --global dotnet-ef`)

**1. API secrets** (from `backend/`):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=OpsPilotDb;User Id=<sql-user>;Password=<sql-password>;TrustServerCertificate=True;" --project src/OpsPilot.Api
dotnet user-secrets set "ConnectionStrings:AiReadOnly" "Server=localhost;Database=OpsPilotDb;User Id=opspilot_ai_reader;Password=<reader-password>;TrustServerCertificate=True;ApplicationIntent=ReadOnly;" --project src/OpsPilot.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random characters>" --project src/OpsPilot.Api
dotnet user-secrets set "Seed:AdminPassword" "<a strong password>" --project src/OpsPilot.Api
dotnet user-secrets set "AiService:InternalKey" "<random shared secret>" --project src/OpsPilot.Api
```

**2. Database and API:**

```powershell
dotnet ef database update --project src/OpsPilot.Infrastructure --startup-project src/OpsPilot.Api
dotnet dev-certs https --trust
# First run only: also create the read-only AI login (the connection string's user must be able to create logins).
dotnet run --project src/OpsPilot.Api --launch-profile https -- --Database:EnsureAiReader=true    # https://localhost:7170
```

In Development the API seeds demo data on first start: 20 products, 15 customers, 8 suppliers, 3 warehouses,
six months of orders, purchase orders, invoices and payments, and a dozen service tickets with deliberate
recurring themes (X200 seal leaks, late deliveries, duplicate invoices) for the AI analysis to find.

**3. AI service** (from `ai-service/`):

```powershell
Copy-Item .env.example .env      # set GROQ_API_KEY and INTERNAL_KEY (same value as AiService:InternalKey)
uv sync
uv run uvicorn app.main:app --port 8001
```

**4. Frontend** (from `frontend/`): `npm install`, then `npm start`, and open http://localhost:4200.

### With Docker

```powershell
Copy-Item .env.example .env      # fill in the passwords and keys
docker compose up --build        # http://localhost:8080
```

Compose runs four containers:
- **SQL Server**
- **API:** applies migrations, creates the read-only AI login, and seeds demo data on start.
- **AI service:** keeps paused approval runs and the embedding model on volumes.
- **nginx:** serves the app and proxies `/api`.

### Tests and CI

| Suite | Command | Count |
|---|---|---|
| Backend | `dotnet test` in `backend/` | 56 |
| AI service | `uv run pytest` in `ai-service/` | 25 |
| Frontend | `npx ng test --watch=false` in `frontend/` | 15 |
| AI evaluation | `uv run python -m evals.run` in `ai-service/` (needs the API, AI service and `OPSPILOT_PASSWORD`) | 12 cases |

The AI evaluation (`ai-service/evals/`) runs real questions as different users against the live stack. It checks
the routed intent, the tools called, the generated SQL, citations, answer content and role security, and fails
below a 90% pass rate. It calls the LLM, so it is run on demand rather than in CI.

`.github/workflows/ci.yml` runs the three test suites on every push and pull request. It also runs:
- warnings-as-errors builds
- dependency vulnerability audits (`dotnet list package --vulnerable`, `npm audit`, `pip-audit`)
- a build of all Docker images

## Demo accounts

All accounts use the password you set in `Seed:AdminPassword`.

| Email | Role | Can |
|---|---|---|
| `admin@opspilot.local` | Administrator | Everything, including the audit log |
| `manager@opspilot.local` | Manager | Everything operational, including approving large purchase orders and AI proposals |
| `sales@opspilot.local` | Sales | Customers and sales orders; the Copilot can't show them invoices |
| `inventory@opspilot.local` | Inventory manager | Catalog, stock adjustments and transfers, shipping, receiving |
| `procurement@opspilot.local` | Procurement | Suppliers and purchase orders; can approve AI proposals but not large POs |
| `finance@opspilot.local` | Finance | Invoices and payments; read-only elsewhere |

## Demo walkthrough

1. **Dashboard** (as admin). Show the late orders, products to reorder, and overdue receivables.
2. **Copilot: questions.** Ask *"Which sales orders are late, and by how many days?"* and open
   *How I got this* to show the validated SQL and node timings. Then ask *"Why is SO-10044 delayed?"* and
   *"Are we going to run out of X200?"*.
3. **Copilot: policy.** Ask *"Who can approve purchases over $10,000?"*. The answer cites the procurement policy.
4. **Copilot: action with approval.**
   1. Sign in as `procurement@` and ask *"Prepare purchase orders for anything we need to reorder."*
   2. A proposal card appears, waiting for approval.
   3. Sign in as `manager@`, open the **Approval center**, change a quantity, and approve.
   4. Draft POs are created through the normal purchasing rules, and the original conversation shows the agent's follow-up.
5. **Audit log** (as admin). Filter by *AI-assisted* to see proposal → approval → execution.
6. **Automation** (as manager). Open *Automation*, click *Run now* on the inventory risk scan, and see the AI
   briefing, the bell notification, and the reminders and credit holds the other jobs produce.
7. **Service tickets.** Open *Service Tickets*, click *Find recurring problems* (the AI groups the X200 seal
   leaks, late deliveries and duplicate invoices), then open TCK-50003 and click *Summarise*.
8. **Order to cash.** Create, confirm, ship and invoice a sales order, then record a payment.
9. **Roles.** As `sales@`, ask the Copilot about invoices; the request is refused. As `finance@`, customers are read-only.

## Roadmap

- **Email delivery** for payment reminders (they are recorded and notified in-app today).
- **AI evaluation in CI:** run the evaluation set on a schedule against a staging stack.
- **Hardening:** refresh tokens, optimistic concurrency on stock rows, OpenTelemetry tracing across API and agents,
  and a deployment stage in CI.

## Troubleshooting

- **`An Application Control policy has blocked this file`** when you run `dotnet ef`, `run` or `test`: Windows
  Smart App Control is blocking the freshly built, unsigned assemblies. Turn it off under *Windows Security →
  App & browser control*.
- **SQL error 26:** use the instance that's actually running. A default instance is `Server=localhost`.
- **Copilot says the model isn't configured:** set `GROQ_API_KEY` in `ai-service/.env` and restart the AI
  service. Models are configurable with `GROQ_MODEL` and `GROQ_FAST_MODEL`.
- **Copilot says the AI service isn't available:** start it on port 8001, and check that `INTERNAL_KEY` matches
  `AiService:InternalKey`.
