# C# / .NET Sample — Inventory Replenishment System

> **🌐 Language:** [English](README.md) | [繁體中文](README.zh-Hant.md)

> **IMPORTANT: Portfolio demo built by Datide.**
> The business scenario, company names, and data shown in this app are
> fictional — no real client code or confidential information is included.

A full-stack .NET 10 inventory replenishment system with role-based login,
SKU management, purchase-order workflow (draft → submit → manager approval)
with duplicate protection, over/under-order warnings and a stock-limit
approval guard, **trilingual UI (English / 繁體中文 / 简体中文)**, plus Excel
export. All monetary values are displayed in **USD**.

## What it demonstrates (why it sells)

This is not a "look at my code" sample — **it is a runnable system a client can
click through**:

| Business problem | How the system solves it |
|---|---|
| "We keep running out of stock" | Dashboard runs a replenishment check → suggests which SKUs to reorder and how many |
| "People create duplicate POs" | A SKU **cannot** have two open POs (draft or pending) at once — edit the existing one instead |
| "Staff ordered the wrong quantity" | Staff can review and edit a PO **before** submitting it for approval (draft workflow) |
| "Nobody notices over-ordering" | Quantity above the system's required amount is highlighted **yellow** in both the staff list and the manager's approval screen |
| "Manager approved a PO that blew past the stock limit" | Approval is **blocked** when current stock + in-flight quantity would exceed the SKU's MaxStock — the Approve button is disabled and the server refuses the request |
| "We don't know who ordered what" | Every PO records who created, submitted and approved/rejected it (audit trail) |
| "Orders need a manager's sign-off" | Staff create POs → **manager-only** approval page → approve/reject with reason |
| "Two people ordering at once = chaos" | GUID order numbers (duplicates impossible) + one open PO per SKU |
| "Our team reads Chinese, our clients read English" | The whole UI switches between **English, 繁體中文 and 简体中文** via a navbar language picker (cookie-persisted) |
| "We want to review data in Excel" | One-click export of all tables to .xlsx |

## Scope note — what this demo deliberately does NOT include

**Inbound receiving (入貨) and outbound dispatch (出貨) are out of scope for this
sample.** They belong to separate processes:

- **Inbound receiving** is part of the *acceptance / goods-receipt* process
  (verifying what was ordered against what actually arrived) — a distinct
  workflow from stock-level decision making.
- **Outbound dispatch** is part of the *packing / fulfilment* process
  (picking, packing, shipping customer orders) — also distinct.

This demo focuses on the **stock-management decision loop**: knowing current
levels, deciding what to reorder, and controlling the purchase-order workflow
up to approval. Because inbound and outbound are not included, `StockSnapshot`
levels are treated as given inputs (seeded data) rather than being mutated by
receiving or dispatch events. A receiving module would be the natural next
extension if you wanted to close the loop end-to-end.

## Run it

```bash
dotnet run --project src\Datide.Replenishment.Web
# open http://localhost:5000  (launchSettings may set 5165)
```

Zero setup — SQLite database is created and seeded automatically on first run.

**Demo accounts:**

| Role | Username | Password |
|---|---|---|
| Manager (can approve) | `manager` | `Manager@123` |
| Staff (creates POs) | `staff` | `Staff@123` |

**Try this flow:** log in as `staff` → open the language picker and switch to
**繁體中文** → create a PO for SKU-1001 → it opens as a **draft** on the edit
page → change the quantity to `250` (above the system-required amount — the
field turns **yellow**) → submit for approval → try creating another PO for the
same SKU (blocked) → log out → log in as `manager` → Approvals: you will see
the yellow over-order warning **and** a red **"Exceeds stock limit"** block
(current 8 + in-flight 250 = 258 > limit 200) with the Approve button disabled →
switch language back to **English** to see every label translate instantly.

## Solution structure

```
Datide.Replenishment.slnx
├── src/
│   ├── Datide.Replenishment.Domain/         # Pure business logic, zero dependencies
│   │   ├── Entities/        StockSnapshot, Sku, User, PurchaseOrder, ReplenishmentSuggestion
│   │   ├── Interfaces/      IReplenishmentRule
│   │   └── Rules/           ConsecutiveDaysReplenishmentRule
│   ├── Datide.Replenishment.Application/    # Use cases + contracts
│   │   ├── Abstractions/    IStockSnapshotRepository, IPurchaseOrderRepository
│   │   ├── Contracts/       EvaluateSkuRequest, EvaluationResult
│   │   └── Services/        ReplenishmentService
│   ├── Datide.Replenishment.Infrastructure/ # EF Core + SQLite + seeding
│   │   └── Persistence/     DbContext, Repositories, DatabaseSeeder
│   ├── Datide.Replenishment.Web/            # Razor Pages + cookie auth + i18n
│   │   ├── Pages/
│   │   │   ├── Account/     Login, Logout, AccessDenied
│   │   │   ├── Skus/        List, Create, Edit
│   │   │   └── PurchaseOrders/ List, Create, Edit (draft), Approvals (manager-only), Export
│   │   ├── Resources/       SharedResource.resx (+ zh-Hant / zh-Hans satellites)
│   │   └── App_Data/        datide.db (auto-created)
└── tests/
    └── Datide.Replenishment.UnitTests/      # 15 tests — rule engine + service + in-flight logic
```

## Design decisions worth mentioning in an interview

| Decision | Why |
|---|---|
| **SQLite + repository abstraction** | Zero-setup demo; swap the provider/connection string to move to SQL Server — the Application layer never touches EF Core |
| **Draft → Submit → Approval lifecycle** | Staff can review and fix a PO before it reaches the manager; once submitted it is locked |
| **Duplicate-PO guard** | A SKU can only have one open PO (draft or pending) — edit it instead of creating duplicates |
| **In-flight stock awareness** | Replenishment suggestions subtract quantities already on order (draft/pending/approved) so the system never recommends double-ordering |
| **Over/under-order warnings** | Each PO snapshots the system-required quantity at creation; quantity above it is flagged yellow (staff list *and* manager screen); quantity below it asks staff to confirm |
| **Stock-limit approval guard** | Before approving, the server checks `current stock + in-flight > SKU.MaxStock`; if exceeded the Approve button is disabled and the POST is refused — a final safety net that no over-limit order can slip through |
| **Language-agnostic domain** | The rule returns *structured facts* (stock levels, gross need, in-flight), not an English sentence — the UI composes the reason via the localizer, so every language gets a correct sentence without parsing text |
| **Trilingual UI (i18n)** | `IStringLocalizer<SharedResource>` + neutral/zh-Hant/zh-Hans resx, cookie-persisted via `/set-language`; no browser-detection surprises |
| **GUID order numbers** | Concurrent users can never receive the same number (unlike an auto-increment counter) |
| **Audit columns on PurchaseOrder** | RequestedBy/At, SubmittedAt, DecidedBy/At/Reason — you can always answer "who did what" |
| **Soft deactivate for SKUs** | Never hard-delete; history stays consistent |
| **Password hashing** | Seeded via ASP.NET Core `PasswordHasher` — never plain text |
| **Excel export via ClosedXML** | Stakeholders who don't touch the app can still review the data |
| **USD forced via invariant culture** | Currency displays as USD regardless of the host machine's locale |

## Build & test

```bash
dotnet build Datide.Replenishment.slnx
dotnet test  Datide.Replenishment.slnx
```

**Verified end-to-end:** 0 build warnings/errors · 15 unit tests pass · login,
role separation (staff blocked from Approvals), draft → edit → submit →
approval flow, duplicate-PO blocking, in-flight replenishment math, yellow
over-order warnings carried through to the manager screen, **stock-limit
approval blocking** (UI disabled + server refusal), **language switching across
EN / 繁體中文 / 简体中文** (including replenishment reason sentences), and Excel
export — all exercised against the running app.
