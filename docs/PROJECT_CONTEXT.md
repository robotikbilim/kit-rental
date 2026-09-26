# Project Context For Future Development

This file is the first-stop project memory for future agent work. Before scanning the full repository, read this file and then inspect only the directly relevant files. After every development task, update this file when behavior, schema, routes, workflows, project structure, or conventions change.

## Product Purpose

KitRental is a .NET 10 kit rental management system for robotics education kits. It tracks catalog models, serial-numbered physical kits, customers, rental orders, assignments, Kargonomi student shipments, public QR flows, faults, returns, stock, warehouse components, BOMs, audits, and dashboards.

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
- `KitRental.Core.Api`: MVC API controllers grouped by public, customer portal, returns, inventory, physical kits, workshop, supply, manufacturing, operations, support, and reporting domains; also owns DI, auth policies, problem details, and health checks.
- `KitRental.Identity.Api`: MVC API controllers for authentication, users, and internal notification-recipient access.
- `KitRental.Gateway`: lightweight HttpClient reverse proxy for Identity and Core.
- `KitRental.Web`: server-rendered ASP.NET Core MVC UI and API client. The former standalone `KitRental.Web.React` project was removed; MVC is the only application UI.

## Code Standards

- Target framework is `net10.0`.
- Nullable reference types and implicit usings are enabled.
- Warnings are treated as errors via `Directory.Build.props`.
- Prefer existing patterns over new abstractions.
- Keep domain state changes inside domain methods; services orchestrate use cases.
- Application services should not know HTTP.
- Controllers/API endpoints should stay thin and delegate to application services.
- Application and infrastructure service implementations are kept under project-level `Services` folders (feature subfolders are allowed); interfaces that were previously colocated with implementations are kept under `Interfaces` folders.
- API routes are exposed from controller classes under each API project's `Controllers` folder. `Program.cs` is limited to host configuration, DI, middleware, health checks, and `MapControllers()`.
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

- Local build troubleshooting: MSB3027/MSB3021 "file is locked by .NET Host" errors occur when the Core API, Identity API, Gateway or MVC hosts are still running from `bin/Debug/net10.0`. Verify each host's loaded DLL path belongs to this checkout before stopping it, then rerun the approved normal solution build. Do not stop unrelated .NET/Codex processes. Missing `obj/.../ref` metadata files can be downstream errors from these failed copies. On 2026-09-26, stopping the four confirmed KitRental hosts resolved the normal build with 0 warnings/errors; those hosts were left stopped for the next development launch.

## Admin Operation Center (2026-09-25)

- Primary admin navigation now exposes `Operasyon Merkezi`, `Siparişler`, `Arıza Takibi`, and `İadeler` directly. Utility submenus retain click-only behavior; the customer portal navigation is unchanged.
- `OperationsOverviewService` owns read-only dashboard and order projections. `OperationsReadModels.cs` holds API read contracts; `OperationsWorkload` centralizes return-state, open-fault/stage, and order-focus classifications. Existing commands remain in `OperationsService` and the domain. MVC only binds filters, calls the API, and renders typed screen models; customer portal order-summary contracts remain compatible.
- `GET /api/dashboard?customerId=` returns customer-scoped lifecycle counts, customer choices, a generated-at timestamp, and up to eight priority orders. Priority ordering is overdue rentals, failed shipments, approval requests, then rental end date. Cards open matching filtered orders/faults/returns rather than unrelated unfiltered lists.
- `GET /api/operations/orders` is restricted to admin/operations/warehouse/service/auditor roles and accepts `customerId`, `query` (order number, customer, period name or kit description), `type`, `status`, `focus`, `endsFrom`, `endsTo`, `sort`, `page`, and `pageSize`. Focus keys are `approval`, `address`, `preparation`, `shipment`, `ready`, `in-transit`, `delivered`, `shipment-failed`, `active`, `faults`, `returns`, `overdue`, and `ending-soon`. Sort is newest by default, `oldest`, or `end-date`; page size is clamped to 10–100. Filtering and stable sorting happen before paging. The MVC orders screen uses this endpoint rather than downloading 5,000 summaries and issuing per-order assignment queries. Shared client DataTables enhancement is explicitly disabled on this already-paged table.
- Operational address/shipping backlogs exclude draft, cancelled, and rejected orders. Student rows exclude deleted students. Failed shipment attempts are included in both the failure count and the retryable shipping backlog; draft/ready shipments are separate from in-transit shipments. Delivery is based on Kargonomi records, not the administrative order-completion flag.
- Active kit counts represent active rental assignments without a received return. An administratively completed order can still have active rentals. Overdue means its end date is before the injected TimeProvider's Turkey date and it still has active rentals; ending-soon includes today through seven days ahead. Latest return requests are selected per assignment by CreatedAt, then Id. Received returns take precedence over external shipment IDs, preventing completed returns from also counting as in transit. Return counts are per kit assignment; missing-form counts are a subset of pending returns.
- `GET /api/returns/table` now accepts `customerId`, `orderId`, and `state` (`pending`, `in-transit`, `completed`, `missing-form`). It batch-loads only assigned physical kits within the selected order/customer scope. MVC exposes state tabs, order links, and existing column filters. This table opts out of saved DataTables state so old browser filters cannot silently hide a dashboard drill-down result.
- `GET /api/faults` additionally accepts `customerId`, `orderId`, and `stage`; the authenticated customer's scope still overrides a supplied customer ID. Stages: `review` = Open/Investigating/WaitingForCustomer; `repair` = Accepted/InService/WorkshopReceived; `shipment` = AwaitingReturn/AwaitingWorkshopShipment/Repaired; `completed` = Resolved/Closed/RemoteResolved; `open` excludes those completed states and Rejected, and includes transport stages. Fault rows link back to their order. Pagination and mutations preserve the current scope/filters.
- Order details preserve the student/kit/shipment DataTable, selection-scoped bulk actions, exports, labels, period edits and existing lifecycle commands. Added summary tiles and links to the order's faults/returns connect the screens. Shared order-status labels live in MVC `OperationsDisplay`.
- New admin styling is isolated in `wwwroot/css/operations.css`, loaded only for Operations views. Dashboard metric markup is shared in `_OperationsMetric`; `_OperationsOrderRow` and `_OrderPeriodDialog` separate order-row rendering and editing from filter/pagination markup. Failed reads render a 503 retry screen instead of displaying fabricated zero metrics or an empty list.
- No EF entity/schema changes or migration are required. Repository reads are batched and customer-scoped, but operational projections still materialize the selected customer's data in the application before filtering/paging; database-side aggregate projections remain a performance follow-up for large installations.
- Regression coverage is authored in `OperationsOverviewTests` and `OperationsOverviewApiTests`: received-return deduplication, expired fulfilled orders, Turkey-date boundaries, scope/filter-before-pagination, shipment retry/ready semantics, fault-group parity, and admin endpoint authorization/query binding. Build, test execution, and browser rendering still require verification after the project's build approval gate.

## Responsive Operations Tables (2026-09-26)

- Admin fault records retain their original background and use a 3px separator after each record, scoped to `#operations-faults-table` in `operations.css`. The list is an overview with fault, courier and outbound statuses; the record number and `Detayı Aç` open the detail page in a new tab. QR printing and all status/shipment commands are available only on the detail page.
- Fault courier/outbound panels stretch to equal height when side by side. Before a provider shipment exists, the carrier field defaults to `HepsiJet` for collection and `Aras Kargo` for outbound, matching the existing service selection; created shipments retain their recorded carrier. The outbound panel no longer displays the explanatory warehouse-wait/serial/QR note.
- Admin `OrderDetails`, `Returns`, and `Faults` opt into `data-operations-table="true"`, with stable table IDs and styling scoped to `ops-data-region` / `ops-data-table` in `operations.css`. At widths up to 900px the same rows become labeled cards; full addresses/links wrap, selection stays on the original row, and row actions have 44px touch targets. The customer portal and inventory retain their existing table configuration.
- `wwwroot/js/operations-tables.js` loads before `site.js` for Operations views. It captures column identities before DataTables initialization and moves the existing student/return filter controls into a keyboard-accessible collapsible panel. Filters remain accessible when their columns are hidden. Client search and text filters debounce for 250ms; all filters apply in one draw, including reset. Shipment multi-select uses exact alternatives against status-only search values. Return address regex options use single backslashes in HTML.
- Student/return tables provide full-list search, column visibility, sortable desktop headings, a mobile sort selector, 10/25/50/100 row limits, a filter count, and reset. These screens do not restore stale browser filter/page state. Cross-page student selection still covers every matching row, independent of the visible page size. The selection summary reports selected rows outside the current filters and offers a separate clear-selection action. Export, label, create-kit, shipping and deletion eligibility remain unchanged.
- Shipment filter option labels remain clickable: a temporary `focusout` with no next focus target must not close the checkbox menu before the label activates its checkbox. Focus moving to an external control, an outside click, and Escape still close the menu.
- Faults retain existing server filtering and paging. The table opts out of DataTables (`data-datatable="false"`); mobile cell labels are rendered explicitly. The result heading explains whole-result filtering and newest-first ordering. On the detail page, `_FaultStatusEditor` presents only manual transitions allowed by the current `FaultTicket` state, with explanatory radio cards, a labeled optional customer-visible note, selection preview, cancel/reset and explicit save. No action is preselected; terminal or unsupported legacy stages do not expose an unusable form. Existing authorized mutations and local return URLs are preserved.
- Performance improvements here concern browser interaction/rendering. Student and return reads still load the full scoped list, and fault reads retain their current application-level paging; no database-query scalability claim is made. No schema or migration change is required.
- Validation: `git diff --check` passed. The approved solution build hit MSB3027/MSB3021 because the running MVC host locked its output DLL; `dotnet build KitRental.slnx --artifacts-path <temporary-directory>` then passed with 0 warnings/errors without stopping that host. Isolated Edge/Playwright fixtures using the real local CSS/JS passed 1,200-row search/paging, address regex, exact shipment multi-select, cross-page/hidden selection clearing, hidden-column filtering, mobile sorting, fault server-page controls/disclosures, and return-state filter/reset. Layouts were inspected at 390/768/900/1440px as applicable. These are UI fixture checks, not authenticated end-to-end mutation tests; the running application was not redeployed or restarted.

## Admin Fault Detail (2026-09-26)

- `/Operations/FaultDetails/{id}` loads `GET /api/faults/{ticketId}` through the Gateway. The read endpoint is restricted to admin, operations, warehouse, technician and auditor roles. Missing records return 404; MVC renders the operations unavailable screen for failed upstream requests.
- `OperationsService.GetFaultDetailAsync` reads the original fault/kit, status history and newest live assignment. Reported student/address and current student/address are separate. Current addresses are resolved only from that assignment's latest location event, then its student/order; received returns suppress current-assignment display. Customer-scoped cohort/return reads remain materialized; this is not a database scalability change.
- `FaultDetails.cshtml` shows the description/attachment, reporting contact, original order/student, kit identity/status and locally generated QR, current assignment, and newest-first status notes. Serial and `Kit Detayı ve Geçmişi` links open `PhysicalKits/Details` under its existing read permissions. QR printing uses the existing fault label endpoint.
- `_FaultLogistics.cshtml` owns the courier/outbound panels and includes `_FaultStatusEditor`. They preserve authorization, antiforgery, carrier defaults, tracking/label actions and equal desktop panel heights. Forms return to the same detail URL after success or failure. The list contains no command forms or label buttons.
- No database schema change or migration. Added detail-read regression coverage for original kit/history, assignment-scoped addresses, returned kits, historical fault/current-student separation, customer denial and missing records. Build/test execution requires approval; static diff checks do not establish runtime correctness.

## Customer Portal Operational Tracking (2026-09-26)

- Customer dashboard cards use the same `OperationsOverviewService.GetDashboardAsync(customerId)` read model as admin. The simplified layout contains only `Sipariş ve Kargo`, `Arıza Durumu`, and `İade Durumu` metric groups; `Genel Durum` is removed. `Sipariş ve Kargo` contains `Adres Bekleyen`, `Gönderim Bekleyen`, `Kargoda`, and `Teslim Edildi`; the ready/acceptance-waiting and failed-shipment cards are removed from this dashboard. `Arıza Durumu` no longer displays the `İnceleme Bekleyen` card. All remaining cards link to matching scoped lists and follow `OperationsWorkload`. Placeholder zero cards and the UI's form-submission-as-completed-return counter are removed. Existing API counts and top-level dashboard fields remain for other screens and compatibility; this UI uses the `Operations` property. The kit map remains available.
- The customer ID always comes from authenticated claims. The nested dashboard's admin customer-selector collection is explicitly cleared; no other customer names/options are returned. Dashboard data failures show a 503 retry view instead of zero counts.
- Rental-period contexts include `Progress` from customer-scoped admin order summaries, without per-order summary queries. The list and details distinguish order status from actual kargo delivery and show preparation, missing addresses, ready/in-transit/delivered/failed shipments, active kits, faults and returns. `/api/customer-portal/rental-periods/context?focus=` uses `OperationsWorkload.MatchesOrderFocus`, matching admin dashboard drill-downs. Orders without a cohort are returned as `StandaloneOrders` and shown with read-only progress so direct rentals/purchases are not omitted. Planned cohorts with no order retain existing management actions.
- Customer fault DTOs include `Stage`, `IsOpen` and two independent, read-only shipment summaries: collection to Robotik Bilim and replacement to the parent. The fault list shows the fault status and links to its detail; the detail screen shows each shipment's status, tracking, carrier, destination and update time in collapsed panels alongside the fault status/history. `OperationsDisplay.FaultStatus` is shared with admin, including status 16. Open faults exclude resolved/closed/remote-resolved/rejected; completed faults exclude rejected. Customer routes expose no shipment-creation/admin action or provider-error payload.
- Customer return responses include `OperationalReturns` generated by `OperationsService.GetReturnsTableAsync` with the authenticated customer scope. The UI uses these rows instead of deriving a competing lifecycle in MVC. Tabs use `pending`, `in-transit`, `completed`, `missing-form`; old `processing`/`returned` links map to the corresponding new states. Carrier status/tracking, shipment date and warehouse receipt date are separate. Provider delivery alone stays `Kargoda`; `Received` wins even with an external ID. Kit-detail return history uses the same normalized state.
- Portal styling lives in `wwwroot/css/portal-progress.css`, loaded only for CustomerPortal. Fault/return/order lists disable saved DataTables state so dashboard filters cannot be masked by stale browser filters. Status and shipment tracking remain read-only; student/order editing locks and public QR flows are preserved.
- No EF schema change or migration. Added regression cases in `OperationsOverviewTests` cover admin/customer counter parity, receipt precedence, tenant isolation, two independent fault shipments and order filters including orders without cohorts. `git diff --check` passed; build/tests and authenticated browser verification still await the project build-approval gate. Reads reuse existing batched projections but still materialize the customer's datasets; no database scalability claim is made.

## Core Business Areas

Inventory and catalog:

- Main domain: `ProductModel`, `ProductUnit`, `InventoryEvent`.
- Main services: `InventoryService`, `PhysicalKitService`.
- Physical kit status flow includes `Available`, `Reserved`, `Preparing`, `OutboundInTransit`, `WithCustomer`, `ReturnInTransit`, `UnderInspection`, `Available/InMaintenance/Quarantined/Retired`.

Customers and orders:

- Main domain: `Customer`, `Address`, `AddressSnapshot`, `RentalOrder`.
- Order delivery address is a snapshot. Later customer address edits do not rewrite old orders.
- Customer allowed product model rows use client-generated `CustomerAllowedProductModel.Id` values so replacing the allowed-kit list in the admin customer edit flow does not create EF owned-entity tracking key collisions.
- Main service: `OperationsService`.

Rentals:

- Main domain: `RentalAssignment`, `RentalPeriod`.
- Rental dates are owned by `RentalOrder.Period` (and the linked rental cohort where applicable); `RentalAssignment` no longer stores a student/kit-specific period. All kits in one rental order therefore share the order's start/end dates, and assignment overlap checks resolve dates through the related order line. Migration `20260918120000_RemoveRentalAssignmentPeriod` removes the legacy `RentalAssignments.Period` column.
- Reservation overlap is handled atomically in repository methods such as `TryCreateReservationAsync` and `TryCreateReservationsAsync`.
- Customer/TACEV rental planning uses `RentalCohort` with owned `RentalCohortStudent` rows for named date ranges and student kit choices.
- Customer/TACEV period names are persisted on `RentalCohort.Name`; the customer portal order-period form offers distinct previous period names as selectable suggestions while still allowing a new name to be typed.
- TACEV can create a rental order from a rental cohort in the customer portal. Active students are linked to the created order through `RentalCohortStudent.OrderId`.
- Admin rental order creation collects one education kit selection plus student full name/guardian phone rows, creates an order-linked `RentalCohort`, and computes the order quantity from the student count.
- Order-linked rental cohort students can start without an address. Public student address links under `/adres/{token}` collect the current free-text address and optional coordinates into the student row; admin order detail and customer portal order-period detail show student address status, addresses, and copyable links.
- Public student address collection treats coordinates as optional: an open address is sufficient, and missing/invalid map coordinates are ignored instead of blocking save. The MVC form also offers city/district dropdowns from `wwwroot/js/turkey-address-dropdowns.js`; selected city/district are folded into the saved free-text address rather than stored in separate schema columns.
- Admin order detail and customer portal order-period detail can export the order student/address-link list as Excel, including student full name, phone, kit, address status, address, and public link. Admin order detail also exposes a `Kargonomi` export beside the standard Excel export; it includes only students with a completed address, uses the fixed sender values from the supplied Kargonomi template, maps student name/address/phone and parses the stored `City / District - Address` prefix when present, repeats `admin@robotikbilim.com.tr` in receiver e-mail and `2` in the first package desi/weight for every row, and leaves neighborhood, order amount, later package desi/weight, and `Mail` cells blank while retaining all template headers.
- Admin `Operations/Orders` uses the paged operational read model documented below, with icon-only detail and period-edit actions. System administrators and operations managers can update a linked rental cohort period in a popup; the order and cohort dates are saved together and audited. Editing preserves the current list filters through a validated local return URL. Desktop uses a fixed-width table and mobile uses stacked records.
- Admin kit preparation for order-linked student cohorts can continue even when some student addresses are still missing.
- Admin order details use a shortened order flow: approve the incoming order, create/reserve kits for selected students from the combined DataTable, then complete the order directly. The previous admin "prepare for shipment" and "mark shipped" actions are no longer shown; completion requires every order student to have an address, writes each assigned student's address to `KitLocationEvents`, moves reserved rental kits to customer/rented state, and moves purchase kits to sold state without requiring shipment statuses.
- Admin kit preparation assigns physical kits to order-linked students but no longer writes student address location events at reservation time; student kit location is written when the order is completed.
- If a student address is edited from the public address form after the order is already completed, the kit's latest location history is updated from that new address.
- In the customer portal, rental cohorts are presented as `Siparişler`: the former customer `Orders` page redirects to `RentalPeriods`, and the list shows each cohort's linked order number plus approved/unapproved state. The detail action opens the cohort's student list.
- In the customer portal `Siparişler` list, order labels now use shared `OperationsDisplay.OrderStatus`; physical delivery progress is shown separately rather than reducing delivered/active/completed states to one `Tamamlandı` label.
- Rental cohort responses include `IsApproved`; once the linked order reaches `Approved` or any later non-cancelled/non-rejected status, the customer portal locks student add/update/import and order-period plan edits, while linked-kit fault reporting and return request flows remain available. Student deletion is governed separately: approval alone does not block deletion, but a student with a physical-kit assignment or Kargonomi shipment cannot be deleted.
- Admin approval and kit preparation for orders linked to TACEV rental cohorts do not geocode student addresses; the student free-text address is used as entered.
- Admin order kit preparation can select a customer's rental cohort. When selected, kit quantities are calculated from unassigned cohort students, and reserved/created kits are linked to the matching students.
- When admin kit preparation assigns a rental cohort student to a kit, the generated `KitLocationEvent` uses the student's free-text address and does not copy separate student regional fields or coordinates.
- If an admin opens kit preparation for an order created from a TACEV cohort, the cohort is inferred from student `OrderId` links and selected automatically.
- Preparing kits for a TACEV cohort assigns kits to students without requiring addresses. Completing the order creates `DeliveryReceipt` kit-location events from each student's name, guardian phone, and address, and completion is blocked until every order student has an address.
- Order-scoped QR label printing keeps the physical kit serial number and QR code unchanged, but includes the currently assigned cohort student's name and guardian phone when a kit is linked to a TACEV order; student address is intentionally not printed on the label. Printed labels use the ordering customer's name as the label heading and the "Arıza bildirimi veya iade için okutun" instruction; non-order label printing falls back to `Robotik Bilim`. Print CSS pins the A4 layout to fixed-width label cards instead of allowing mobile rules to collapse labels to a single column.
- TACEV rental period student rows include assigned kit serial/QR plus delivery-form summary fields when the kit has been delivered or auto-filled from the student list.
- TACEV rental period student rows show address status and address in separate columns; rows created for public address collection show that the address is still pending, and the single-line student filter bar includes address-status filtering.
- Admin and customer order-detail student tables keep address and public-link cells empty when there is no value. Both surfaces render the real address and public URL inline, truncate overflow to one line, and open the public URL in a new tab. Global MVC table/form styling loads at 80% zoom on desktop and 100% on narrow mobile screens, keeps table rows, filters, dropdowns, and action buttons compact, and preserves full-height/responsive desktop sidebar behavior when the sidebar is collapsed.
- TACEV rental period student create/edit forms and Excel import collect student full name and guardian phone without requiring address. Separate regional student fields are no longer stored.
- TACEV rental period student updates are handled from an in-page modal opened by compact icon-only row actions; delete, return-request, and fault actions also use compact color-coded Lucide icon buttons.
- Removing an already assigned student from the admin order removes the student from the cohort and its kit association from the order's combined student/kit view; the physical unit remains managed by inventory history.
- Customer-portal student kit returns open a prefilled return form instead of creating the request immediately; the form uses the delivery-form recipient/address when present, otherwise the student record, requires a return reason, and does not ask for map coordinates.
- When an admin accepts a kit return, the TACEV student row keeps its assigned kit serial/QR as historical context; completed-return rows disable customer fault and return-request actions.
- `ProductUnitActivity` stores chronological kit operation logs with action, description, timestamp, actor id, and actor display-name snapshot.

Faults:

- Main domain: `FaultTicket`, `FaultStatusEvent`.
- New fault records follow `Open` -> `Investigating` -> `Accepted` or `Rejected`; accepted records can be remotely resolved or enter logistics. The repair sequence remains available, but collection and replacement shipping can start independently from `Accepted`: replacement no longer waits for `WorkshopReceived`/`Repaired`. The original physical-kit identity, serial number, QR and rental assignment are retained. Kargonomi webhook updates preserve the other shipment leg; automatic closure waits for replacement delivery and old-kit receipt (a delivered collection shipment or recorded workshop receipt). The transition service records customer-visible notes in `FaultStatusEvents`; persisted enum values remain unchanged.
- Legacy fault status enum values 1-8 are retained for existing data; new workflow states use values 9-16 to avoid renumbering persisted records. The operations UI now presents action-oriented labels for the new lifecycle.
- Public QR fault flow can create a new fault or update an existing open fault.
- Fault updates preserve history and now also insert a new kit location event.
- `FaultTicket.Origin` distinguishes internal, public QR form, and customer-portal fault records. Customer-portal fault creation uses reporter name, phone, free-text address, and description fields, and operations fault lists show the source column.
- Customer-portal fault forms prefill reporter name, phone, and address from the selected rental assignment's chronologically latest physical-kit location event. Delivery receipt, fault report/update, and return request events share this ordering; the linked student and customer address are used only when the current assignment has no location history.
- Fault notification emails are queued in-process by Core API through `EmailNotificationQueue` / `EmailNotificationWorker`; public QR and customer-portal fault save flows enqueue the admin email and return without waiting for SMTP delivery.
- Public QR fault records optionally store one validated photo or short video attachment URL (`FaultTicket.AttachmentUrl`). MVC accepts JPG/PNG/WEBP or MP4/WEBM/MOV files up to 25 MB under `wwwroot/uploads/faults`; operations fault rows expose the attachment link. Migration `20260910100000_AddFaultAttachmentUrl` adds the nullable column.

Physical kit detail history:

- `PhysicalKitService.GetDetailAsync` now exposes separate histories for delivery/receipt events, fault records, and return-request starts.
- `PhysicalKitService.GetDetailAsync` also exposes `ActivityHistory` for the chronological kit operation log.
- `KitRental.Web.Mvc/Views/PhysicalKits/Details.cshtml` renders those histories as separate list-card sections.
- `KitRental.Web.Mvc/Views/PhysicalKits/Lookup.cshtml` mirrors the same separated history groups for quick lookup.

Returns:

- Main domain: `KitReturnRequest`, `ReturnInspection`.
- Public QR return request inserts a kit location event.
- Admin receipt/acceptance of a kit return inserts a latest `KitLocationEvent` for each returned kit with address and contact name `Robotik Bilim Atölye`; latitude and longitude are null.
- Return receipt/inspection changes kit and assignment status.

Shipments:

- The former generic manual `Shipment` / `ShipmentEvent` domain, repository methods, API routes, MVC screens, and database tables are removed.
- Main outbound shipping domain: `KargonomiShipment` with owned `KargonomiShipmentEvent` history, keyed uniquely by `OrderId + StudentId`.
- `KargonomiShippingService` creates one Kargonomi shipment per addressed student, resolves the stored `City / District - Address` text, selects and confirms the fixed Aras Kargo quote, and stores external IDs/status/errors per student.
- When an order shipment attempt fails before its first database save, the same tracked shipment record is marked failed instead of creating a duplicate `OrderId + StudentId` row. Provider status, tracking, description, and error text are bounded to their persistence limits, and the operations result banner includes the first provider error so retryable failures remain actionable.
- Kargonomi create-shipment requests normalize Turkish sender and recipient mobile numbers from `05xx`, `+905xx`, `00905xx`, or formatted variants to the provider-required 10-digit `5xxxxxxxxx` representation; invalid or non-mobile values return an actionable validation message before calling the provider.
- Kargonomi statuses are updated through `POST /api/kargonomi/webhooks/shipment-updated`. The webhook validates the raw request body against the configured secret using the `X-Webhook-Signature` HMAC-SHA256 header, parses the documented nested `shipment` payload, and updates matching order, fault, and return shipments; duplicate order/fault events remain ignored by their domain event history. Manual status-refresh endpoints and buttons are removed. The production callback URL is `https://atolye.et-edu.net/core/api/kargonomi/webhooks/shipment-updated`, registered in Kargonomi with event type `shipment.updated`.
- Delivery status is displayed and recorded but does not automatically change the order's existing delivery/return confirmation flow.
- Operations UI: order detail shows student and Kargonomi shipment information in one combined table. The admin navigation exposes a top-level `Kargonomi` menu with a `Gönderiler` submenu; `/Operations/KargonomiShipments` is independent of local orders and students, reads every page of the Kargonomi `GET /shipments` API, and lists all shipments in the connected Kargonomi account with recipient, address, package, carrier, tracking, status, and timestamp data.
- The Kargonomi shipments list remains read-only for status display; shipment status changes arrive through the signed webhook.
- Admin order details and `İadeler` no longer expose manual Kargonomi status-refresh actions; their displayed status/tracking values are updated by the signed webhook.
- Core API registers `KargonomiClient` as the typed implementation of `IKargonomiClient`; Kargonomi service resolution depends on this interface mapping for list, create, barcode, and webhook-related operations.
- Kargonomi GET requests honor the provider's `429 Too Many Requests` `Retry-After` response with bounded retries. This allows the all-shipments screen to read multi-page accounts without turning a temporary provider rate limit into an empty MVC list.
- Kargonomi credentials, webhook secret, warehouse, and sender configuration are read from the `Kargonomi` configuration section and must be supplied through environment variables, user secrets, or deployment secrets; tracked appsettings keeps secrets empty.
- Fault logistics use existing owned `FaultKargonomiShipment` records with independent `ToWorkshop` and `ToCustomer` directions. `POST /api/faults/{faultTicketId}/kargonomi-shipments` now accepts only `direction`; contact/address inputs are resolved on the server. `Kurye Gönder` uses the fault reporter as sender, the configured Robotik Bilim warehouse/sender address as destination, and the existing reverse-shipment HepsiJet flow. `Kargoya Gönder` uses the fault reporter as recipient and the Aras outbound flow. Both are available after acceptance without waiting for either leg. Invalid/remote-resolved/rejected/closed stages reject new shipments before calling the provider.
- Each admin fault detail page has two native, keyboard-accessible `details` panels titled `Kurye Gönder` and `Kargoya Gönder` (side by side on desktop, stacked on mobile). Both start closed on every render and open independently on user interaction. Their summaries retain purpose, status and tracking number; opening reveals route, address, carrier, update time, error/retry, start and PDF label actions. Expanding a panel never initiates a shipment. QR printing resolves the fault's original physical kit. List responses batch-load kit serials for the visible page and include shipment summaries without per-row API calls. Local return URLs and optional status notes remain supported.
- Fault output endpoints: `GET /api/faults/{faultTicketId}/kargonomi-shipments/{shipmentId}/barcode` validates shipment ownership; `GET /api/faults/{faultTicketId}/kit-label` returns existing kit identifiers. MVC `FaultKargonomiBarcode` reuses the shared PDF print handler; `FaultKitLabel` renders a 60×30 mm QR label. Read roles include operations, warehouse, technician and auditor; shipment commands retain admin/operations/technician authorization and MVC antiforgery.
- Provider IDs are saved before quote confirmation; failed confirmation retries reuse that ID. Successful repeated requests reuse the corresponding shipment. In-process striped locks serialize starts for the same fault; these do not provide cross-instance provider idempotency. The UI disables submitted buttons. Existing cancelled shipment history remains stored. No EF schema changes or migration are needed.
- Validation for the 2026-09-26 fault logistics change: regression tests authored in `FaultShipmentTests` cover either start/delivery order, independent tracking, original QR/serial, provider-ID reuse, stage guards and barcode ownership. Build/test execution awaits explicit approval; live provider shipping and authenticated browser verification have not been run.

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
- `KitLocationEvents` coordinates are stored when a delivery, fault, or return form supplies valid latitude/longitude values; missing coordinates remain unavailable until a later form submission provides them.
- Delivery form inserts a `KitLocationEvent` with source `DeliveryReceipt`.
- Public fault creation inserts source `FaultReport`.
- Public fault update inserts source `FaultUpdate`.
- Existing open public fault edits also insert source `FaultUpdate`.
- Public return request inserts source `ReturnRequest`.
- Public QR form access now uses 24-hour random access tokens. The printed QR still opens `/ariza/{qrCode}`, but MVC immediately requests `POST /api/public/form-access/{qrCode}` and redirects to `/ariza/form/{token}`. Public fault, return, delivery context, fault-guide, and submit endpoints resolve the token server-side before using the kit QR code. Tokens are stored only as SHA-256 hashes in `PublicFormAccessTokens` with `ExpiresAt`; successful submissions do not invalidate tokens before expiry.
- Public return requests store `DeliveryMethod` (`Adresimden Alınsın` or `Kendim Bırakacağım`). Drop-off returns do not show the fixed Aras Kargo return code until the form is saved; after save, the public success page shows a pop-up with code `1234567890`. Drop-off returns do not require pickup address/map fields and store the Aras drop-off instruction as the return address.
- Reopening the public QR return form before admin return receipt loads the active return request through `/api/public/returns/context/{token}` and allows updating the return reason, requester details, pickup/drop-off delivery method, and pickup location fields instead of creating a duplicate return.
- Public QR forms treat latitude/longitude as optional and untrusted. Invalid or missing coordinates must not block saving; backend stores null coordinates when no valid map selection is provided.
- Public QR fault, delivery, and return forms store free-text address plus optional latitude/longitude; fault and pickup-return forms additionally collect city/district selections in the address prefix. Reopening the forms with a valid token refills name, phone, address, and coordinates from the current rental assignment's latest delivery/fault/return location event. This assignment scope prevents a reused kit from exposing a previous customer's address. Legacy assignments without an event fall back to their student, order-delivery, or customer address.
- Public QR fault form now includes required city/district dropdowns, prefills the address from the kit's latest delivery/fault/return location event (falling back to the current assignment and then the open fault address), preserves the selected city/district in the stored address prefix, and fills both dropdowns when reverse geocoding completes after `Konumumu Bul` or map selection.
- Public QR return form uses the same city/district dropdown and address-prefix flow for pickup returns, restores the selections when an active return is reopened, and keeps city/district/address optional for drop-off returns.
- The public QR landing screen offers only fault reporting and kit return; the user-facing `Kit Teslim Al` option is hidden because admins mark customer delivery automatically. The public delivery endpoint and MVC action still exist for internal compatibility.
- Customer portal maps read latest location events, with order delivery address as fallback for old kits with no event.
- Physical kit detail uses assignment-specific latest location for rental history, and product-unit latest location for current location.
- Inventory supports a rental-expiry filter for active customer rentals and shows customer, order number, rental end date, and remaining/overdue days when rental information is present.

Migration status:

- `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents` creates `KitLocationEvents`.
- The migration copies existing `KitDeliveryReceipts` rows into `KitLocationEvents` with source `DeliveryReceipt`, then drops `KitDeliveryReceipts`.
- `20260818123000_AddRentalCohortStudentCoordinates` adds nullable latitude/longitude columns to `RentalCohortStudents`.
- `20260820162000_AddFaultTicketOrigin` adds `FaultTickets.Origin` with default `Internal`.
- `20260820222749_AddPublicFormAccessTokens` adds `PublicFormAccessTokens` for 24-hour hashed public form access tokens.
- `20260907120000_AddStudentAddressCollectionTokens` adds `RentalCohortStudents.PublicAddressToken` and `AddressSubmittedAt` for order-specific public student address collection.
- `20260917134029_ReplaceManualShipmentWithKargonomi` drops `Shipments` and `ShipmentEvents`, creates `KargonomiShipments` and `KargonomiShipmentEvents`, and adds the student/order and external shipment indexes.

## Public QR Flows

MVC controller:

- `KitRental.Web/src/KitRental.Web.Mvc/Controllers/PublicFaultController.cs`

Core API routes:

- `POST /api/public/form-access/{qrCode}` creates a 24-hour public access token from a scanned QR code.
- `GET /api/public/faults/kit/{token}`
- `GET /api/public/deliveries/context/{token}`
- `GET /api/public/faults/context/{token}`
- `POST /api/public/faults` requires `Token` in the request body.
- `POST /api/public/returns` requires `Token` in the request body.
- `GET /api/public/returns/context/{token}`
- `POST /api/public/deliveries` requires `Token` in the request body.
- `GET /api/public/fault-guides/{token}` returns active kit-specific guides through a valid token.
- `GET /api/public/student-addresses/{token}` returns the public student address form context.
- `POST /api/public/student-addresses/{token}` saves the public student address and optional coordinates.
- `GET/POST/PUT /api/customer-portal/rental-periods`
- `DELETE /api/customer-portal/rental-periods/{periodId}`
- `POST/PUT/DELETE /api/customer-portal/rental-periods/{periodId}/students`
- `POST /api/customer-portal/rental-periods/{periodId}/student-imports`
- `POST /api/customer-portal/rental-periods/{periodId}/students/{studentId}/return`
- `POST /api/customer-portal/rental-periods/{periodId}/order`
- `POST /api/customer-portal/returns`
- `POST /api/customer-portal/returns/{returnId}/ship`
- `GET /api/customers/{customerId}/rental-periods`
- `POST /api/orders/{orderId}/kargonomi/shipments`
- `POST /api/orders/{orderId}/kargonomi/shipments/{studentId}`
- `GET /api/orders/{orderId}/kargonomi/shipments`
- `GET /api/kargonomi/shipments`
- `GET /api/kargonomi/shipments/{id}/barcode`
- `POST /api/kargonomi/webhooks/shipment-updated` (secret header required)

Web API client:

- `KitRental.Web/src/KitRental.Web.Mvc/Services/KitRentalApiClient.cs`

The delivery context endpoint now reads the latest kit location event, not delivery receipts.

## UI And Map Notes

Map UI is rendered in MVC views and powered by:

- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/turkey-kit-map.js`
- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/public-location.js`
- `KitRental.Web/src/KitRental.Web.Mvc/Views/CustomerPortal/Index.cshtml`

Global MVC UI behavior:

- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/site.js` sets a page-level busy state for form submits, same-origin navigation links, and same-origin mutating `fetch` calls so backend-bound actions disable other buttons/links and show a small loader until the response navigates, completes, or the page is restored.
- Same-page, external, telephone/mail, dialog, and explicit download links are excluded from the navigation busy lock; same-page downloads recover through a fallback timeout if no navigation occurs.
- MVC confirmation prompts use SweetAlert2 through `data-confirm` on forms or submit buttons; avoid inline `onsubmit`/`onclick` browser `confirm(...)` dialogs.
- Phone number inputs use a global Turkey mask in `site.js` and the MVC `[TurkishPhone]` validation attribute. Backend domain methods normalize accepted numbers with `KitRental.SharedKernel.TurkishPhoneNumber` using `libphonenumber-csharp`, storing Turkey national format.
- User-facing button/action labels should use title case in Turkish, with each word's first letter capitalized.
- Shared MVC layout loads Bootstrap 5.3, jQuery 3.7, DataTables 2 with Bootstrap styling, Responsive, Buttons, Select, and JSZip from CDN before the versioned local site assets.
- Every MVC table is explicitly marked with `js-datatable`. `wwwroot/js/site.js` also enhances unmarked tables inside `main` for compatibility with already-running precompiled Razor views, and dynamically loads missing DataTables dependencies when an older compiled layout does not yet include them.
- DataTables provide Turkish search, sorting, responsive rows, a styled working column-visibility menu, client-side paging, and opt-in multi-row checkbox selection. Client-paged tables expose page size through a `Gösterilecek Satır` dropdown button styled like the `Sütunlar` button, with 10, 25, 50, 100, and all-row choices; the old inline `kayıt göster` selector is not rendered. The generic DataTables toolbar no longer includes clipboard copy, Excel export, or print buttons; feature-specific export actions remain available on their existing page-level buttons. Tables marked `data-datatable-server="true"` or paired with a `.pagination-shell` keep their existing server-side pagination and use DataTables only for the current page's search, sorting, visibility, and selection controls.
- DataTables table and toolbar styling is centralized in `wwwroot/css/site.css`; action/empty header columns are non-sortable, exports omit action columns, and mobile layouts use responsive detail rows instead of forcing wide horizontal tables.
- MVC server pagination links and DataTables-generated pagination links/buttons share one global `site.css` style based on the admin order student-list pagination: single-layer 36 px bordered buttons, green active state, muted disabled state, consistent hover/focus behavior, and horizontally scrollable mobile layout. Bootstrap DataTables pagination styles only the inner `.page-link`; its outer `.page-item.dt-paging-button` remains an unstyled layout wrapper to prevent nested-square buttons.

Map markers depend on latitude/longitude where present. Address text still appears in marker details.

Map location response rows include `ProductModelId`, `KitSku`, `Status`, and `LocationCategory`.
`LocationCategory` is produced by backend services:

- `faulty`: open fault exists, or the unit is in maintenance/quarantine.
- `returning`: an active assignment already has a non-received `KitReturnRequest`, matching the return-process cards.
- `expired`: an active assignment is past its end date and still has no return request, matching the customer portal expired-kit count.
- `active`: all other active rental assignment map rows.

Customer portal map filters are powered by `turkey-kit-map.js`.
The customer portal map exposes status checkboxes for faulty, return-process, expired, and active kits; a serial-number search; and product-model checkboxes that are selected by default.
Product-model filter labels show the education set/product model name (`KitName`), not the stock code/SKU.
Filter counts are calculated from all active rental map rows, not just rows with coordinates.
Rows without latitude/longitude are not rendered as markers and are shown as a small "missing location" count near the map.
The customer portal map no longer renders regional side summaries; the map canvas uses the full map layout width.
The customer portal map canvas uses a fixed desktop height.
Turkey map overview starts closer in its default state and uses tight initial fit-to-markers padding.

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
- Updated customer portal map and physical kit details to read current location from latest location event.
- Added migration `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents`.
- Migration preserves old delivery receipt data by copying it into `KitLocationEvents` before dropping `KitDeliveryReceipts`.
- Removed automatic address geocoding from public QR flows.
- Public fault, delivery, and return forms now include a small Leaflet map with a `Konumumu bul` action. GPS can place a nearby draggable pin, users can move the pin manually, and reverse geocoding fills only the free-text address from the selected point.
- Removed MVC range validation from public QR form latitude/longitude fields so invalid hidden coordinates do not block form submission.
- Added map filters for faulty kits, return-process kits, active kits, serial number, and product model.
- Added a missing-location label under the map filters; map counts now include coordinate-less active rental rows while markers still require coordinates.
- Split physical kit detail and lookup history into separate card groups for deliveries, faults, and return requests.
- Added customer portal summary cards for expired rental kits, return shipments awaiting dispatch, return shipments in transit, and kits with a completed return form.
- Customer portal return-pending counts now come from active assignments whose `EndDate` is before today and have no `KitReturnRequest` item; the two shipment counters are intentionally fixed at zero until shipment-state calculation is added, and the completed-return-form count is based on unique assignments included in any `KitReturnRequest`.
- Customer portal `Aktif Kitler` summary card now counts only healthy kits that still have an active assignment, are still in `WithCustomer`, and have no open fault.
- Customer portal maps now classify `faulty` only from open fault tickets, and kits whose returns were already received are removed from map rows.
- Added a dedicated customer portal `Returns` page with filters for pending, in-progress, and returned states, plus expired kits that have not started a return yet.
- Added customer portal navigation entry for `İadeler`.
- Customer portal expired-rental checks now use the app server local date (`DateTime.Today`) instead of UTC so locally expired kits appear immediately after midnight.
- Customer portal returns filter now matches on a dedicated state key (`pending`/`processing`/`returned`) while the table keeps separate Turkish status labels for display.
- Customer portal return semantics are assignment-based: on Wednesday, August 12, 2026, `pending` means an active rental ended before today and still has no return form, `processing` means a return request exists regardless of due date, and `returned` means warehouse/admin accepted the return back into available stock.
- Receiving a return is allowed from both `Requested` and `InTransit`.
- Customer portal returns list status mapping now treats `KitReturnStatus.Requested` as `processing` / `İade Sürecinde` so the list matches the summary cards once a return form exists.
- Map `returning` / `expired` categories now use the same assignment-based rules as the customer portal cards, so the filters stay aligned.
- Top map status filters on the customer portal now render as `col-md-3` items so the four status checkboxes share a single row on medium+ widths.
- Time-stamped operations and UI log/history displays are now standardized on Turkey time via shared helpers instead of mixing UTC and server-local conversions.
- Fixed repository-wide Turkish text encoding issues in MVC/Core user-facing strings, API descriptions, and related test data by normalizing mojibake back to proper UTF-8 Turkish characters.
- Verified with `dotnet build KitRental.slnx`.

2026-08-14:

- Customer portal overview summary cards now show: total rented kits, undelivered kits, open faults, completed faults, return-pending kits, return-process kits, and returned kits.
- Customer portal overview response now includes `TotalRentedKitCount` and `UndeliveredKitCount`; undelivered counts reserved/active rental assignments with no `DeliveryReceipt` location event.
- Customer portal `Kits` page supports a `deliveryFormMissing` filter, and the `Teslim Alınmamış Kitler` card opens that filtered list.
- Debug builds set `UseAppHost=false` in `Directory.Build.props` so local web apps run through the signed `dotnet` host instead of unsigned generated apphost executables, avoiding Smart App Control blocks on development DLL loads.

2026-08-15:

- Added customer/TACEV rental periods and student lists with Excel template/import support in the MVC customer portal.
- TACEV student Excel import reads student full name and guardian phone. The Excel upload popup selects one education kit for the whole uploaded list before opening the preview screen.
- Added `RentalCohort`, `RentalCohortStudent`, and `ProductUnitActivity` persistence, repository access, and migration `20260815175751_AddRentalCohortsAndProductUnitActivitiesSnapshotFix`.
- Admin order kit preparation can select a customer's rental period; selected periods drive kit quantities from student kit choices and link created/reused units to students.
- TACEV rental period detail now has an `Onayla ve sipariş oluştur` action that confirms the student list will be locked, then creates a pending rental order for admins from the student kit totals.
- Admin kit preparation for TACEV-created orders auto-selects the source period and creates student delivery form location events from the student list.
- Customer portal no longer has a separate order-list page; the `Siparişler` menu points to rental cohorts and shows linked order approval status in a datatable-style list before opening the student list detail.
- Customer portal `Siparişler` list has a top `Yeni sipariş oluştur` button that opens a popup rental period form, where the customer can type a new period name or choose a previous period name and enter the valid rental date range; saving returns to the list with the popup closed.
- Customer portal `Siparişler` rows open the related student-list screen. On that screen, unlocked/unapproved periods expose popup actions for single student creation and Excel bulk upload; the Excel template download link and whole-list education kit dropdown live inside the Excel upload popup, and imported rows continue through the preview screen before saving.
- Customer portal order-period creation now suggests previously used period names from existing rental cohorts and still accepts a brand-new period name in the same field.
- Approved customer portal order periods lock create/import/edit actions but still allow deleting students who have no physical-kit assignment and no Kargonomi shipment. The customer API enforces assignment/shipment eligibility independently of approval, removes one matching kit requirement throughout the approved order lifecycle when an eligible student is deleted, and rejects deletion once a kit is assigned or shipment exists. Disabled trash actions explain the blocking reason; fault reporting and return request actions stay available for linked active kits.
- Customer portal student list actions now use Lucide icons and an edit modal instead of navigating to a prefilled edit page.
- Student removal after assignment anonymizes student details while preserving the rented kit/assignment as an unassigned period kit.
- Physical kit details now show chronological operation history rows for kit creation, reservation, student assignment/removal, faults, deliveries, returns, and inspections.

2026-08-18:

- Global MVC confirmation handling now renders SweetAlert2 dialogs for `data-confirm` actions, including customer portal order approval; native browser confirm popups are no longer used.
- Standardized MVC button and action labels to title case across the system.
- Customer portal student-list detail no longer shows a separate `Siparişi Gör` action after the cohort order is created.
- Sidebar/topbar navigation items keep icons and labels left-aligned next to each other; only submenu chevrons are pushed to the right.
- Customer portal `Siparişler` list supports filtering by `Sipariş Dönemi` and `Onay Durumu`, and paginates filtered results with a fixed default of 20 records per page.
- Filter forms marked with `data-auto-filter="true"` auto-submit when an input/select changes, so filter screens no longer show manual `Temizle` or `Filtrele` buttons.
- Customer portal `Siparişler` list displays `Oluşturulma Tarihi` and sorts by `CreatedAt` descending by default.
- Customer portal student-list detail supports auto-filtering by student text search, education kit, assignment state, and address state, and paginates filtered students with 20 records per page.
- Customer portal `Kitler` list includes the assigned TACEV student name, guardian phone, and period when a kit is linked to a rental cohort student; student address is not shown in that list, though kit search still matches student fields.
- Customer portal overview map no longer renders status, serial-number, or product-model filters; it always shows all customer kit markers with coordinates.
- Customer portal `Siparişler` list now shows `Düzenle` and `Sil` actions for unapproved rental cohorts. Editing can change the period name and rental date range before admin approval; if a pending order exists, its rental period and kit quantity lines are synchronized from the cohort student list. Deleting removes the unapproved cohort and its linked unapproved order, but remains blocked after kit assignment or approval.
- Admin approval of a rental order linked to TACEV students no longer geocodes student addresses or stores separate student location coordinates from that flow.
- Customer portal TACEV student Excel templates and import preview screens use only student name and guardian phone columns.

2026-08-20:

- Customer portal overview summary cards now show the requested eight-card set: all non-returned rented kits, student-assigned kits, unassigned rented kits, open faults, closed faults, return-pending kits, return-process kits, and returned kits. The old `Teslim Alınmamış Kitler` overview card was removed.
- Customer portal `ActiveKitCount` now means rented kits currently assigned to a student. `UnassignedKitCount` counts currently rented kits without a student assignment, excluding returned assignments.
- Customer portal overview metric cards use a compact 8-column desktop layout at 992px and wider so all cards fit on one row at normal desktop zoom.
- Map filter and missing-location controls now render above the map grid.
- Rental cohort student records briefly persisted separate regional fields through older migrations; these student-specific fields were later removed by `20260901180730_RemoveRentalCohortStudentLocationFields`.
- Customer portal kits that have a received return are exposed as historical read-only records. Their detail page remains viewable, but QR/fault-report and return actions are hidden in MVC and rejected by the customer-portal application service/API.
- Core and Identity business endpoints were migrated from `Program.cs` Minimal API mappings into thin MVC API controllers. Core controllers are grouped by domain under `KitRental.Core.Api/Controllers`; Identity uses separate auth, users, and internal-notifications controllers. Gateway wildcard proxy routes remain infrastructure endpoints.
- API request DTOs now live under each API project's `Contracts/Requests` folder instead of `Program.cs`.
- Core and Identity exception translation is handled by dedicated `ApiExceptionMiddleware` classes; `Program.cs` only registers the middleware.
- Core and Identity persistence and application service registrations are grouped in `Extensions/ServiceCollectionExtensions.cs` and exposed through `AddCoreServices` / `AddIdentityServices`.
- Fault guide entries can target a specific kit model through `FaultGuideEntries.ProductModelId`; new admin entries require a kit selection, while legacy entries without a model remain general fallback guides. Public QR fault troubleshooting loads only active guides for the scanned kit model plus general fallback entries. Migration `AddFaultGuideProductModel` adds the nullable foreign key.
- Public QR troubleshooting now presents a more explicit `Çözülmedi, Servise Gönder` action, with the `Geri Dön` action placed at the bottom of the page.
- Public QR kit-return forms require a return reason: `Eğitim Tamamlandı` or `Kayıt Silindi`. The selected `KitReturnReason` is persisted on `KitReturnRequests`.
- MVC list-row actions now share a compact action style: text row links/buttons render as small chips, destructive actions use the red variant, and customer portal order rows use icon-only actions with accessible labels/tooltips.
- Customers can now be limited to specific education kits through `CustomerAllowedProductModels`; an empty allowed-kit list means all product models are available. Admin customer creation and editing expose a `Kullanıma açılan kitler` multi-select with `Tüm Eğitim Kitleri`, and customer portal student create/import kit dropdowns plus backend save/import validation use only the customer's allowed product models. Migration `20260820124317_AddCustomerAllowedProductModels` adds the allowed-kit table.

2026-08-21:

- Public QR form access now creates a 24-hour random access token from `/ariza/{qrCode}` and redirects users to `/ariza/form/{token}`. Public form links saved from the browser expire after one day and require scanning the QR again for a fresh token.
- Added `PublicFormAccessToken` persistence with SHA-256 `TokenHash`, `ProductUnitId`, `CreatedAt`, `ExpiresAt`, and `LastUsedAt`; migration `20260820222749_AddPublicFormAccessTokens` creates the table and indexes.
- Public fault, return, delivery context, fault-guide, and submit endpoints now require a valid token and resolve the kit server-side before invoking existing QR-code-based application logic. Tokens are not invalidated after successful submission.
- The public QR landing screen no longer shows the `Kit Teslim Al` option. Customer delivery confirmation remains available through the existing admin/customer automatic delivery flows, and the legacy public delivery endpoint/action was left in place for compatibility.

2026-08-27:

- Customer portal `Kitler` list supports an `assignmentState` filter (`all`, `assigned`, `unassigned`) based on whether `AssignedStudentName` is present. The overview `Atanmayan Kitler` card opens the list with `assignmentState=unassigned`, so it shows only rented kits that have not been assigned to a student.

2026-09-01:

- Removed separate regional collection from customer portal TACEV student create/edit and Excel import flows. The Excel template later contains only student full name and guardian phone; address can be collected after order submission through public student address links.
- Removed student regional fields from customer-portal API contracts, MVC view models, and `RentalCohortStudent`; migration `20260901180730_RemoveRentalCohortStudentLocationFields` drops the old student regional columns from `RentalCohortStudents`.
- Removed project-wide separate regional storage from customer addresses, order delivery snapshots, kit location events, public/customer portal request contracts, map popups, and QR forms. Migration `20260901185000_RemoveProjectWideCityDistrictFields` drops the remaining regional columns and lookup tables; free-text address and optional latitude/longitude remain.
- Admin kit preparation no longer geocodes TACEV student addresses or copies student regional fields/coordinates into generated delivery location events; the student free-text address remains the only student address source.

2026-09-07:

- Admin rental order creation no longer asks for a customer delivery address or kit quantity lines. The admin selects a customer, date range, one education kit, and student rows containing full name plus phone number.
- Creating an admin rental order now creates an order-linked `RentalCohort` behind the scenes, links all students to the order, and uses the student count as the order quantity.
- `RentalCohortStudent` can be created with an empty address for public address collection, stores `PublicAddressToken` and `AddressSubmittedAt`, and can update its address/optional coordinates from the tokenized public form.
- Added public student address collection routes: MVC `/adres/{token}` and Core API `GET/POST /api/public/student-addresses/{token}`.
- Public student address forms require city and district selection before saving; invalid or incomplete coordinates are discarded. City/district dropdown selections are prepended to the saved address text without reintroducing separate location columns, and reopening the same public form parses that saved prefix back into the city/district dropdowns while leaving the remaining open address in the textarea. On the public address form, `Konumumu Bul` reverse-geocoding also auto-selects the matching city and district dropdown values when Nominatim returns recognizable Turkey address fields.
- Admin order details show order-linked students, address completion status, entered addresses, and copyable public address links.
- Customer portal order-period details also show each student's public address link and include an Excel export for the student address/link list; admin order detail has the same Excel export.
- Admin order detail and customer portal order-period student tables show the real address and public address URL directly in truncated cells; address URLs open in a new tab and no `Göster` popup button is used on these two tables.
- Customer portal order-period student tables display address status and address in separate columns, with an `Adres Durumu` filter for completed vs pending public addresses.
- MVC tables and filter/form panels are globally compacted for the admin and customer portals. Student address and public-link columns render a one-line truncated preview in the row with the full value exposed by the native title tooltip, so long values do not change table row height or width.
- Admin kit preparation for order-linked student cohorts no longer waits for all public student addresses. Students without addresses can still receive a physical kit assignment, but order completion is blocked until all student addresses are present; completion writes the student addresses into kit location history.
- Added migration `20260907120000_AddStudentAddressCollectionTokens` for student public address token and submission timestamp columns.
- Customer portal student create/edit and Excel import now allow orders to be sent for approval with only student full name and guardian phone; address fields are left empty until public address collection is completed.
- Customer portal student Excel import preview shows the total parsed student count above the preview table.
- Customer portal rental-period detail no longer exposes a customer-side `Onayla Ve Sipariş Oluştur` action. When the first student is added or students are imported, a linked `PendingApproval` rental order is created automatically so the order appears in the admin panel immediately; later student add/update/delete operations before admin approval synchronize the linked order's product-model quantities. Once admin approval moves the order past `PendingApproval`, customer-side period and student mutations remain blocked.
- QR label print CSS keeps the same visual proportions as the on-screen label cards: A4 print still uses a three-column grid, but normal and student-recipient labels preserve their own card, QR, spacing, and text scale ratios instead of being forced into a taller shared print size.
- QR label output uses a printer-sized print page for Xprinter XP-470B-compatible 30×60 mm stock: one 60 mm wide × 30 mm high label per page, zero page margins, no A4 grid, and browser zoom disabled. The browser print dialog must use actual-size/100% and the XP-470B label driver/paper preset.
- QR label print text uses enlarged, bold print-specific font sizes for barcode-printer readability on the fixed 60×30 mm label; internal spacing is minimized, the fixed student caption is omitted, and the scan instruction stays on one line.
- Customer portal order-period student rows no longer repeat assigned kit serial/QR under the student name; the same values remain in the assigned physical kit column as a link to the portal kit detail page, while delivery summary lines starting with `Teslim:` still display under the student name.
- On admin order details, the `Siparişi Tamamla` control remains clickable when student addresses are missing, but it shows the popup warning `Eksik adres bilgisi olan kayıtlar var, önce adresleri doldurun.` instead of submitting the completion transition. Once every student has an address, the normal completion form is shown.
- Admin order detail loads the complete combined student/kit/shipment list into DataTables and uses client-side pagination with 10 rows per page.
- Data migration `20260907143000_SeedRedKitFaultGuides` replaces red-kit fault-guide seed rows with active kit-specific troubleshooting entries for DHT11, LDR, PIR, Ultrasonik Sensör, POT, Buton, RGB LED, LED, LED / PWM, Buzzer, and LCD. The migration resolves the red-kit product model by SKU/name/image URL and removes matching legacy general seed titles before inserting the new list.
- Core/Identity list-style GET endpoints now return a standard paged JSON envelope with `Page`, `PageSize`, `TotalCount`, `TotalPages`, and `Items`; MVC API client unwraps `Items` for existing dropdown, export, label, and list screens while sending explicit `pageSize` for whole-list support data.
- API action method names were normalized away from generated underscore/number names to PascalCase C# method names. Test method names were also normalized to remove underscores.
- REST route cleanup renamed command-style endpoints to resource-style names: product unit batch creation uses `POST /api/product-unit-batches`, kit model creation uses `POST /api/kit-models`, physical kit rental uses `POST /api/physical-kits/{id}/rentals`, bulk physical-kit rental uses `POST /api/physical-kit-rental-batches`, order transitions use `POST /api/orders/{orderId}/status-transitions`, order detail uses `GET /api/orders/{orderId}`, customer portal delivery confirmation uses `POST /api/customer-portal/orders/{orderId}/delivery-confirmations`, return receipt uses `POST /api/kit-returns/{returnId}/receipts`, fault status changes use `POST /api/faults/{ticketId}/status-events`, fault searching now uses filtered `GET /api/faults`, audit searching uses `GET /api/audit-entries`, component suggestions use `GET /api/component-suggestions`, and supply-need refresh/completion/approval use `POST /api/supply-need-recommendation-refreshes`, `POST /api/supply-needs/{id}/completions`, and `POST /api/supply-needs/{id}/approvals`.
- Migration `20260907150000_AddListPaginationIndexes` adds indexes for frequently filtered/listed fields across customers, product models/units, rental orders/cohorts/assignments, kit locations, faults, fault guides, kit returns, components, and audit entries.

## Recent UI Behavior

- Admin ve müşteri paneli sipariş detaylarındaki öğrenci DataTable kargo durumu çoklu filtre menüsü, tablo satırları azaldığında veya sonuç kalmadığında tablo tarafından kırpılmaz; seçenekler tablo yüksekliğinden bağımsız görünür ve menü içinde kaydırılabilir.

- Admin `Operations/Returns` now uses `GET /api/returns/table` to list each rental kit separately in the inventory-style DataTable. Expired active rentals without a return request are classified as `İade Bekleniyor`, return requests with an `InTransit` status or an external shipment record as `Kargoda`, and received requests as `Tamamlanmış`; the table omits the customer column, keeps per-column filters/address links/physical-kit details/return receipt actions, filters `Kargo Durumu` with the same multi-select status menu as admin order details, and lays the detail, receive, and barcode icon actions side by side using the order-detail compact action layout.
- Public QR return forms label the address-pickup submit action `Kurye Çağır`; while the synchronous Kargonomi request is pending, the form disables duplicate submission and shows a spinner until either the provider barcode or tracking code exists. The Core flow creates a reverse-direction Kargonomi shipment from the requester to the configured Robotik Bilim address, selects and confirms the HepsiJet quote, stores the resulting tracking code in the existing return `Carrier`/`TrackingNumber` fields, marks the return request `InTransit` with `ShippedAt`, and returns the provider barcode to the MVC success page. The page shows `Kurye Talebi Başarı ile Oluşturuldu.` plus the instruction to show a screenshot to the courier and renders the barcode and/or tracking code; the provider shipment ID remains separate for synchronization. Return-provider fields are added by migration `20260925132321_AddKargonomiReturnTracking`. Drop-off returns keep the existing Aras branch-code flow.
- A public QR scan now detects an active `InTransit` return before rendering the return form. Until an administrator receives the return, the customer sees a read-only page with the current Kargonomi status, carrier, and tracking code; direct duplicate public-return submissions are rejected. After the admin receipt transition, the active return context no longer blocks the normal flow.
- Admin operasyon dashboard eski kartlardan temizlendi. Yeni dashboard `GET /api/dashboard` üzerinden sipariş ve kargo özeti, `İade Bekleniyor`, `Kargoda` ve `İade Tamamlandı` iade kartları ile ayrı `Arıza Takibi` alanında inceleme bekleyen, onarımdaki, kargo bekleyen ve tamamlanmış arızalar için kartlar gösterir. Gelen iadeler ayrı `GET /api/returns` endpointinden okunur; kit-konum geocoding endpointi ve arka plan servisi kaldırılmıştır.

- Admin `Envanter` sayfası, sipariş detayındaki öğrenci/kargo DataTable standardını kullanır; tüm oluşturulmuş fiziksel kitleri tek client-side tabloda gösterir ve öğrenci, telefon, fiziksel kit, kargo durumu, takip no, adres ve adres linki alanlarında aynı sütun filtrelerini kullanır. Atama bilgileri mevcut kiralama ataması, öğrenci adresi ve Kargonomi kayıtlarından doldurulur; kit kapsamı siparişe değil tüm envantere aittir.

- Admin panel sol menüsü alt menüleri artık yalnızca ilgili ana menüye tıklanınca açar; hover veya focus ile kendiliğinden açılmaz. Tüm ana menü grupları ikonludur; `Katalog ve Üretim` için kitap-açık, `Yönetim` için ayarlar ikonu kullanılır.

- Customer portal kit detail pages now show a current location/student card, chronological rental-history entries with student/address/period/date data, chronological fault log entries, and chronological return-process log entries for the selected physical kit.
- Customer portal fault form data is server-enriched from the requested rental-period student assignment before rendering, so the selected assignment carries its own student and contact details instead of relying only on the aggregated kit summary.
- Customer portal fault creation preserves user-entered fields after validation errors while revalidating the submitted assignment against the customer's active physical kits.
- Customer portal fault creation opened from a student row now targets only that student's reserved or active, non-returned physical-kit assignment. The fault form no longer exposes kit selection or QR scanning; the address is prefixed from the selected kit's latest `KitLocationEvent` address, with student/customer details used only as fallback. The generic fault entry point opens the customer kit list so a physical kit must be selected first.
- Customer portal fault-report links from kit, student, return, and kit-list rows open the fixed-kit form in a new browser tab.
- Customer portal physical-kit detail links from student, return, and kit-list rows open the kit detail in a new browser tab.
- Customer portal physical-kit detail history loops close their Razor `foreach` blocks correctly so no literal closing brace is rendered below rental, fault, or return history cards.
- Customer portal physical-kit detail selection prefers the current non-returned active assignment when a physical unit has historical assignments; this keeps the detail page's fault link tied to the currently using student rather than an older assignment.
- Customer portal kit summaries use the assignment-to-student link as authoritative; physical-unit fallback is used only when that unit has a single linked student, preventing historical rows from being shown as the current user.
- Customer portal dashboard metrics are superseded by the shared admin classifications documented under `Customer Portal Operational Tracking`; the earlier student-assigned/delivered-kit-only headline layout is no longer rendered.
- Customer portal `Arıza Durumu` also shows temporary `Teknik Servis Yolunda` and `Öğrenci Yolunda` cards, both with a fixed value of `0`; their calculation rules are intentionally deferred.
- Customer portal dashboard status groups no longer use white bordered `dashboard-group` panels; each group is rendered as a compact heading followed by one metric row so the status cards fit together without unnecessary vertical space.
- The customer portal `İade Durumu` metric row shows `İade Beklenen Kitler`, `Kargo Bekleyen Kitler`, `Kargodaki Kitler`, and `İadesi Tamamlanmış Kitler`; the two shipment counters are currently fixed at zero.

- The customer portal `RentalPeriods`, `Kits`, and `Returns` DataTable wrappers have no outer border or frame; the common table and cell styling remains unchanged.
- The customer portal `Faults` table now follows the same client-side DataTables standard and no-frame wrapper as the other customer lists, including header filters, sorting, page-length and column-visibility menus, shared pagination, and compact icon-based detail actions.
- Every row in the customer portal `Kits` table now shows the fault-record action; it is active for assigned, non-returned kits and visibly disabled with an explanatory tooltip for unassigned or returned kits.
- Customer portal `Kits` rows show the actual Kargonomi `Kargo Durumu` label used by the admin order table. Kits without a shipment show `Başlatılmadı`, failed shipments show `Hata`, and the dashboard counts `Teslim Edildi` shipments as `Kullanımdaki Kitler`, `Teslim Sürecinde` shipments as `Gönderimdeki Kitler`, and every other shipment state as `Hazırlanan Kitler`; the dashboard also recognizes these labels when a legacy/internal shipment state is still pending.

- Customer portal \`RentalPeriods\`, \`Kits\`, and \`Returns\` list tables use the same client-side DataTables standard as the admin order detail: complete list data, compact no-horizontal-scroll layout, per-column filters in the header's top filter row, sortable columns, \`Gösterilecek Satır\` page-length menu, \`Sütunlar\` visibility menu, shared pagination styling, and icon-based row actions. Their former server-side filter forms and standalone pagination controls are no longer rendered.

- Admin order detail derives address completion from the address text for filtering, selection, and shipment eligibility, but the combined student table no longer displays a separate `Adres Durumu` column.
- Admin order detail's combined student table uses a dropdown in the `Adres` column filter with `Tümü`, `Adresi Girilenler`, and `Adresi Girilmeyenler` options, so empty address rows can be isolated without restoring a separate address-status column.
- The same admin order-detail table uses a select-styled multi-select dropdown in the `Kargo Durumu` column filter; it opens a compact checkbox menu whose labels display with only the first character uppercase, contains `Tümü`, `Başlatılmadı`, `Hata`, and the distinct provider status labels present in the order, and selected values are matched as alternatives so multiple shipment states can be viewed together.
- Admin order-detail exports and QR label printing are DataTables bulk actions. Excel and Kargonomi exports receive only the selected student IDs; QR label printing receives only selected students with assigned physical kits.
- Order-detail GET bulk-action forms carry the order ID as a hidden field so browser form submission preserves the target order for Excel, Kargonomi, and QR label actions.
- Admin order detail no longer exposes a DataTables bulk `Teslim Edildi` action. Delivery confirmation endpoints remain available for existing workflows, but the combined table's bulk actions are `Sil` and `Kargoya Ver`.
- Admin order detail no longer has a separate filter form above the combined table. DataTables provides dedicated per-column filters in a second header row directly below every data-column title, plus sortable column headers, global search, a `Gösterilecek Satır` page-size dropdown button, and client-side pagination across the complete student list.
- Admin order detail uses DataTables Select checkboxes for single or multiple row selection and shows a live selected-row count in the table toolbar. Selection checkboxes are fixed-size square controls centered vertically and horizontally in their cells; checked and indeterminate states use a soft site-brand green fill and mark instead of the browser/Bootstrap blue style. Selected rows use a soft tint derived from the active site-brand color, dark readable text, subtle brand-color separators, and a leading accent instead of DataTables' default dark-blue selection; the tint automatically follows the standard or TACEV theme. The header checkbox selects the current page, while `Tüm Sayfalardakileri Seç` selects every row matching the active filters across all client-side pages and then changes to `Tüm Seçimleri Kaldır`. The selection controls, selected-row count, and conditional bulk `Sil` / `Kargoya Ver` actions occupy their own toolbar row below the independent `Gösterilecek Satır` / `Sütunlar` row, and all visible selection/bulk buttons use the same soft site-brand green palette. Bulk action buttons stay hidden until at least one row is selected; deletion applies to every selected student. Bulk shipping is enabled only when every selected row has a completed address and can start a shipment; one ineligible selected row disables the whole `Kargoya Ver` action, while failed shipments remain retryable.
- Admin order detail now presents students and their assigned physical kits in one table; admin users can delete a student before preparation starts, which removes the student and kit assignment from the order and releases the reserved physical kit back to available inventory.
- System administrators and operations managers can update each order student's full name and phone from a row-level pencil action; the API validates that the student belongs to the rental order and records an `OrderStudentUpdated` audit entry.
- On pending-approval or approved rental orders, administrators can add a student from the student-table header. The new student is linked to the order's rental cohort, the first rental kit line quantity increases by one, the order and cohort student-count responses update automatically, and `OrderStudentAdded` / `StudentAddedToOrder` audit entries are recorded; later order states keep the add action hidden.
- Admin order detail shows the assigned physical kit serial number as a one-line truncated link in the physical-kit column; selecting the serial opens that kit's detail page.
- Admin order detail combined student table no longer displays a dedicated delivery-status column; delivery state remains part of the order-completion workflow but does not control table selection or expose a bulk-delivery action.
- Admin order detail places `Kargonomi`, `Kargo Etiketi Yazdır`, and `QR Etiketlerini Yazdır` beside `Excel Olarak İndir` above the combined student/kit/shipment table. `Kargonomi` downloads an XLSX file with the 24 supplied template columns. The table shows shipment status, carrier/update time, tracking number, the student's real address text, public URL under the `Adres Linki` heading, and assigned kit serial; long values are truncated to one line with the full value available from the native title tooltip. Public URLs open in a new tab, and kit serial links open the physical-kit detail page. Its action column uses accessible icon-only trash, truck, and printer buttons for `Sil`, `Kargoya Ver`, and `Kargo Etiketi Bastır`. The printer action is present on every student row, stays disabled until Kargonomi has created an external shipment, requests the PDF label from Kargonomi for eligible rows, opens it in a print window, and shows a retry message when the label is not ready. The `Kargo Etiketi Yazdır` bulk action requires every selected row to have a created Kargonomi shipment, requests all selected labels, and opens them together in a print window. `Kargoya Ver` is enabled only when the address is complete and no successful shipment exists; every unavailable truck icon, including missing-address and already-started rows, uses the same muted gray disabled style, while failed shipments can be retried through the active button. `Filtreyi Temizle` resets the global search, all column filters, and the Kargo Durumu multi-select. Global and per-column DataTables search cover the values rendered in the student, phone, assigned kit, shipment status, tracking number, address, and address-link columns.
- Admin order detail combined-list heading is shown only as `ÖĞRENCİ LİSTESİ`, without a secondary title or description.
- Admin order detail no longer shows a per-row or bulk `Teslim Et` action.
- Admin order detail combined student table uses a narrow checkbox column and a wider student-name column.
- Admin order detail combined student table uses fixed compact percentage widths, zero minimum cell widths, and truncated content so every column remains within the available screen width without a horizontal scrollbar or DataTables responsive child rows.
- Admin order detail's approved-order `Kit Oluştur` action is a DataTables bulk action. It is shown only when at least one rental student has no physical-kit assignment, accepts one or more selected student rows, creates/reuses and reserves only those students' kits, and preserves the order's remaining requested quantities for later selections. Rows already assigned a kit cannot be used for this action.
- Admin order summaries recalculate requested and assigned kit counts after an approved-order student/kit removal, excluding cancelled rental assignments.
- Admin order completion message uses the requested wording that all student kits must be delivered; the existing address validation remains unchanged.

- Customer portal rental-period student Excel exports include an `Atanan Fiziksel Kit QR Linki` column containing the assigned kit's public QR target URL when a physical kit is assigned.
- Customer portal rental-period detail now mirrors the admin combined student table: one client-side DataTable contains checkbox selection, per-column filters, sorting, page-size and column-visibility menus, client paging, student/phone, linked physical-kit serial, Kargonomi status and tracking number, inline truncated address and `Adres Linki`, and row actions. Customers can monitor shipment status but cannot start shipments. Bulk delete remains available after admin approval whenever at least one student has neither a physical-kit assignment nor shipment; if any selected row is not deletable the bulk action is disabled. Assigned/shipment-started rows expose a disabled trash icon and are rejected by the customer API if deletion is attempted.
- Public fault troubleshooting actions use equal-width buttons with a clear gap; they switch to equal-width stacked touch targets on narrow screens.
- Operations `FaultGuide` requires selecting a kit before listing guides, filters entries by `ProductModelId`, and uses a shared popup for creating and editing the selected kit's guide entries. The MVC route accepts `productModelId` as the filter query parameter.

## Development Checklist

## Customer Portal Performance (2026-09-18)

- The former all-in-one customer portal overview response was split by screen. `GET /api/customer-portal` now returns only dashboard metrics and map locations; dedicated reads are available at `/api/customer-portal/kits`, `/kits/{productUnitId}`, `/returns`, `/faults`, `/faults/{faultId}`, `/assignments/{assignmentId}/fault-context`, `/rental-periods/context`, and `/rental-periods/{periodId}/context`. MVC customer pages call only their matching endpoint. Portal kit loading also batches assignments across all rental orders and reads kit-location events within the current customer scope instead of loading the system-wide location history.
- Public QR form address context queries only the selected physical kit's current rental assignment and its latest location row instead of loading the system-wide location history; historical assignments cannot leak an earlier customer's address.
- Direct single and bulk physical-kit rental completion now writes a `DeliveryReceipt` location event, so the initially entered delivery address participates in the same latest-address chronology as later fault and return addresses. Existing rentals without an event remain supported by assignment/student/order/customer fallback data.
- Public QR token, kit, fault, return, delivery, and guide flows resolve a physical kit with a QR-filtered repository query. They no longer materialize every physical kit together with its complete status history just to find one QR code; this removes SQL memory-grant (`RESOURCE_SEMAPHORE`) waits observed when opening `/ariza/{qrCode}` on larger local datasets.

## Performance and Database Notes (2026-09-17)

- Customer portal kit-oriented reads batch physical-kit lookups by id instead of querying each assignment, fault, return item, and cohort student separately. Reused product models, returns, kit-location events, and rental cohorts are shared within each relevant screen request. MVC and Gateway responses use compression, and MVC static assets receive a one-week client cache header.

- The relational domain foreign keys are represented with `Guid` values. No string-valued relational foreign key was found; audit/polymorphic fields such as `AuditEntries.EntityId` and `KitLocationEvents.SourceId` remain string identifiers by design and must not be converted without a domain-specific migration.
- Added supporting indexes for assignment/order activity lookups, order-linked student filtering, order/assignment fault filtering, order-scoped kit-location history, return-inspection and stock-movement lookups, and owned collection foreign keys.
- Order aggregate reads now use EF Core split queries because the aggregate includes multiple collection navigations (`Lines`, `ProductUnits`, and `History`); this prevents cartesian row multiplication in SQL result sets.
- The repository still contains several intentionally broad list methods that materialize complete collections before API-level paging. These are documented follow-up hotspots: orders, customers, fault tickets, stock movements, kit-location events, rental cohorts, and Kargonomi shipments. They should be converted to server-side projection/paging in a separate compatibility-focused change.

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

