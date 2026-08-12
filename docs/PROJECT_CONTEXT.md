# Project Context For Future Development

This file is the first-stop project memory for future agent work. Before scanning the full repository, read this file and then inspect only the directly relevant files. After every development task, update this file when behavior, schema, routes, workflows, project structure, or conventions change.

## Product Purpose

KitRental is a .NET 10 kit rental management system for robotics education kits. It tracks catalog models, serial-numbered physical kits, customers, rental orders, assignments, shipments, public QR flows, faults, returns, stock, warehouse components, BOMs, audits, and dashboards.

Main user surfaces:

- MVC admin/operations portal in `KitRental.Web/src/KitRental.Web.Mvc`.
- Public QR pages under `/ariza/{qrCode}` for fault, delivery, and return workflows.
- Customer portal under `CustomerPortal`.
- Core API behind Gateway at `/core/*`.
- Identity API behind Gateway at `/identity/*`.

## Solution Map

- `KitRental.BuildingBlocks`: shared kernel, security, observability, contracts.
- `KitRental.Identity`: user accounts, roles, PBKDF2 passwords, JWT-like signed bearer token, MongoDB persistence.
- `KitRental.Core.Domain`: domain entities, value objects, enums, status machines, invariant checks.
- `KitRental.Core.Application`: use-case services, commands/responses, authorization/customer-scope checks, repository ports.
- `KitRental.Core.Infrastructure`: EF Core SQL Server persistence, migrations, in-memory test repository.
- `KitRental.Core.Api`: Minimal API endpoints, DI, auth policies, problem details.
- `KitRental.Gateway`: lightweight HttpClient reverse proxy for Identity and Core.
- `KitRental.Web`: server-rendered ASP.NET Core MVC UI and API client.

## Code Standards

- Target framework is `net10.0`.
- Nullable reference types and implicit usings are enabled.
- Warnings are treated as errors via `Directory.Build.props`.
- Prefer existing patterns over new abstractions.
- Keep domain state changes inside domain methods; services orchestrate use cases.
- Application services should not know HTTP.
- Controllers/API endpoints should stay thin and delegate to application services.
- EF Core schema changes require a migration before finishing the task.
- Update this file after each development task when project behavior, schema, routes, or conventions change.
- Avoid unrelated refactors and do not revert unrelated working-tree changes.
- Ask for user approval before running build/compilation checks such as `dotnet build`.

## Persistence Rules

Core data uses EF Core 10 + SQL Server through `KitRentalDbContext`.

Identity data uses MongoDB through `IUserRepository`.

Core application code accesses persistence only through `ICoreRepository`.

Production/development repository:

- `KitRental.Core.Infrastructure/Persistence/EfCoreRepository.cs`

Test repository:

- `KitRental.Core.Infrastructure/Persistence/InMemoryCoreRepository.cs`

Migration command pattern:

```powershell
dotnet ef migrations add MigrationName --project "KitRental.Core\src\KitRental.Core.Infrastructure\KitRental.Core.Infrastructure.csproj" --startup-project "KitRental.Core\src\KitRental.Core.Api\KitRental.Core.Api.csproj" --context KitRentalDbContext
```

Build verification:

```powershell
dotnet build KitRental.slnx
```

## Core Business Areas

Inventory and catalog:

- Main domain: `ProductModel`, `ProductUnit`, `InventoryEvent`.
- Main services: `InventoryService`, `PhysicalKitService`.
- Physical kit status flow includes `Available`, `Reserved`, `Preparing`, `OutboundInTransit`, `WithCustomer`, `ReturnInTransit`, `UnderInspection`, `Available/InMaintenance/Quarantined/Retired`.

Customers and orders:

- Main domain: `Customer`, `Address`, `AddressSnapshot`, `RentalOrder`.
- Order delivery address is a snapshot. Later customer address edits do not rewrite old orders.
- Main service: `OperationsService`.

Rentals:

- Main domain: `RentalAssignment`, `RentalPeriod`.
- Reservation overlap is handled atomically in repository methods such as `TryCreateReservationAsync` and `TryCreateReservationsAsync`.

Faults:

- Main domain: `FaultTicket`, `FaultStatusEvent`.
- Public QR fault flow can create a new fault or update an existing open fault.
- Fault updates preserve history and now also insert a new kit location event.

Physical kit detail history:

- `PhysicalKitService.GetDetailAsync` now exposes separate histories for delivery/receipt events, fault records, and return-request starts.
- `KitRental.Web.Mvc/Views/PhysicalKits/Details.cshtml` renders those histories as separate list-card sections.
- `KitRental.Web.Mvc/Views/PhysicalKits/Lookup.cshtml` mirrors the same separated history groups for quick lookup.

Returns:

- Main domain: `KitReturnRequest`, `ReturnInspection`.
- Public QR return request inserts a kit location event.
- Return receipt/inspection changes kit and assignment status.

Shipments:

- Main domain: `Shipment`, `ShipmentEvent`.
- Shipment delivered events can advance order and kit statuses.

Workshop and manufacturing:

- Components, storage locations, stock movements, component stock, BOMs, and buildable kit calculations live mostly in `WorkshopService`.

Audit:

- Mutating use cases generally add `AuditEntry` with actor/time/action.

## Current Kit Location Model

Kit current address is sourced only from `KitLocationEvents`.

Important files:

- `KitRental.Core/src/KitRental.Core.Domain/Logistics/KitLocationEvent.cs`
- `KitRental.Core/src/KitRental.Core.Application/Operations/OperationsService.cs`
- `KitRental.Core/src/KitRental.Core.Application/CustomerPortal/CustomerPortalService.cs`
- `KitRental.Core/src/KitRental.Core.Application/PhysicalKits/PhysicalKitService.cs`
- `KitRental.Core/src/KitRental.Core.Infrastructure/Persistence/KitRentalDbContext.cs`

Rules:

- Do not add current-location fields back onto `ProductUnit`.
- Do not reintroduce `KitDeliveryReceipts` as a live domain/repository table.
- The latest `KitLocationEvents` row for a `ProductUnitId`, ordered by `OccurredAt` then `Id`, is the current kit address.
- Delivery form inserts a `KitLocationEvent` with source `DeliveryReceipt`.
- Public fault creation inserts source `FaultReport`.
- Public fault update inserts source `FaultUpdate`.
- Existing open public fault edits also insert source `FaultUpdate`.
- Public return request inserts source `ReturnRequest`.
- Public QR forms treat latitude/longitude as optional and untrusted. Invalid or missing coordinates must not block saving; backend geocoding tries to resolve coordinates from the open address and stores null coordinates if geocoding fails.
- Dashboard and portal maps read latest location events, with order delivery address as fallback for old kits with no event.
- Physical kit detail uses assignment-specific latest location for rental history, and product-unit latest location for current location.

Migration status:

- `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents` creates `KitLocationEvents`.
- The migration copies existing `KitDeliveryReceipts` rows into `KitLocationEvents` with source `DeliveryReceipt`, then drops `KitDeliveryReceipts`.

## Public QR Flows

MVC controller:

- `KitRental.Web/src/KitRental.Web.Mvc/Controllers/PublicFaultController.cs`

Core API routes:

- `GET /api/public/faults/kit/{qrCode}`
- `GET /api/public/deliveries/context/{qrCode}`
- `GET /api/public/faults/context/{qrCode}`
- `POST /api/public/faults`
- `POST /api/public/returns`
- `POST /api/public/deliveries`

Web API client:

- `KitRental.Web/src/KitRental.Web.Mvc/Services/KitRentalApiClient.cs`

The delivery context endpoint now reads the latest kit location event, not delivery receipts.

## UI And Map Notes

Map UI is rendered in MVC views and powered by:

- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/turkey-kit-map.js`
- `KitRental.Web/src/KitRental.Web.Mvc/Views/Operations/Dashboard.cshtml`
- `KitRental.Web/src/KitRental.Web.Mvc/Views/CustomerPortal/Index.cshtml`

Map markers depend on latitude/longitude where present. Address text still appears in marker details.

Map location response rows include `ProductModelId`, `KitSku`, `Status`, and `LocationCategory`.
`LocationCategory` is produced by backend services:

- `faulty`: open fault exists, or the unit is in maintenance/quarantine.
- `returning`: unit status is `ReturnInTransit`.
- `active`: all other active rental assignment map rows.

Operations dashboard and customer portal map filters are powered by `turkey-kit-map.js`.
Both screens expose status checkboxes for faulty, return-process, and active kits; a serial-number search; and product-model checkboxes that are selected by default.
Product-model filter labels show the education set/product model name (`KitName`), not the stock code/SKU.
Filter counts are calculated from all active rental map rows, not just rows with coordinates.
Rows without latitude/longitude are not rendered as markers and are shown as a small "missing location" count below the filters.

There are existing web UI changes in the working tree unrelated to the kit-location backend work; do not revert them unless explicitly requested.

## Recent Development Log

2026-08-13:

- Added `AGENTS.md` with the rule that EF schema changes require migrations.
- Replaced the initial `ProductUnit.LastKnown...` approach with a dedicated `KitLocationEvents` table.
- Removed live use of `KitDeliveryReceipts`.
- Added `KitLocationEvent` and `KitLocationEventSource`.
- Added repository methods for adding/listing kit location events.
- Updated EF and in-memory repositories.
- Updated public delivery, public fault create/update, and public return request flows to insert location events.
- Updated operation dashboard, customer portal map, and physical kit details to read current location from latest location event.
- Added migration `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents`.
- Migration preserves old delivery receipt data by copying it into `KitLocationEvents` before dropping `KitDeliveryReceipts`.
- Added backend address geocoding through `IAddressGeocoder` and `NominatimAddressGeocoder`.
- Removed MVC range validation from public QR form latitude/longitude fields so invalid hidden coordinates do not block form submission.
- Added map filters for faulty kits, return-process kits, active kits, serial number, and product model.
- Added a missing-location label under the map filters; map counts now include coordinate-less active rental rows while markers still require coordinates.
- Split physical kit detail and lookup history into separate card groups for deliveries, faults, and return requests.
- Added customer portal summary cards for expired rental kits, kits with started return flow, and returned kits.
- Customer portal return counts now come from `KitReturnRequest` states and rental expiry counts from active assignments whose `EndDate` is before today.
- Added a dedicated customer portal `Returns` page with filters for pending, in-progress, and returned states, plus expired kits that have not started a return yet.
- Added customer portal navigation entry for `İadeler`.
- Customer portal expired-rental checks now use the app server local date (`DateTime.Today`) instead of UTC so locally expired kits appear immediately after midnight.
- Customer portal returns filter now matches on a dedicated state key (`pending`/`processing`/`returned`) while the table keeps separate Turkish status labels for display.
- Customer portal return semantics are assignment-based: on Wednesday, August 12, 2026, `pending` means an active rental ended before today and still has no return form, `processing` means a return request exists regardless of due date, and `returned` means warehouse/admin accepted the return back into available stock.
- Admin dashboard now exposes `Iadeyi kabul et` for active return requests, and receiving a return is allowed from both `Requested` and `InTransit`.
- Verified with `dotnet build KitRental.slnx`.

## Development Checklist

Before changing code:

- Read this file first.
- Search only the relevant service/domain/repository/UI files from this map.
- Check `git status --short` and avoid unrelated changes.

When changing schema:

- Update domain/entity and `KitRentalDbContext`.
- Update `ICoreRepository`, `EfCoreRepository`, and `InMemoryCoreRepository` if persistence access changes.
- Add or update EF migration.
- Inspect migration for accidental data loss. If dropping/replacing a table, migrate important existing data first.

Before finishing:

- Run `dotnet build KitRental.slnx`.
- Update this file if the task changed behavior, routes, schema, workflow, or conventions.
- Mention if database update was not run.
