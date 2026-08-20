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
- `KitRental.Core.Api`: MVC API controllers grouped by public, customer portal, returns, inventory, physical kits, workshop, supply, manufacturing, operations, support, and reporting domains; also owns DI, auth policies, problem details, and health checks.
- `KitRental.Identity.Api`: MVC API controllers for authentication, users, and internal notification-recipient access.
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
- Customer/TACEV rental planning uses `RentalCohort` with owned `RentalCohortStudent` rows for named date ranges and student kit choices.
- Customer/TACEV period names are persisted on `RentalCohort.Name`; the customer portal order-period form offers distinct previous period names as selectable suggestions while still allowing a new name to be typed.
- TACEV can create a rental order from a rental cohort in the customer portal. Active students are linked to the created order through `RentalCohortStudent.OrderId`.
- In the customer portal, rental cohorts are presented as `Siparişler`: the former customer `Orders` page redirects to `RentalPeriods`, and the list shows each cohort's linked order number plus approved/unapproved state. The detail action opens the cohort's student list.
- Rental cohort responses include `IsApproved`; once the linked order reaches `Approved` or any later non-cancelled/non-rejected status, the customer portal treats the student list/order as locked only for student-list mutations. Student add/update/import/delete and order-period plan edits are hidden in MVC and rejected by the application service/API, while linked-kit fault reporting and return request flows remain available.
- When an admin approves an order linked to a TACEV rental cohort, active student addresses are geocoded through `IAddressGeocoder`; successful latitude/longitude values are persisted on `RentalCohortStudents` and reused for student delivery location events. If approval-time geocoding does not return coordinates, kit assignment retries geocoding immediately before creating the delivery location event.
- Admin order kit preparation can select a customer's rental cohort. When selected, kit quantities are calculated from unassigned cohort students, and reserved/created kits are linked to the matching students.
- When admin kit preparation assigns a rental cohort student to a kit, the generated `KitLocationEvent` now copies the student's `District`, `City`, `Latitude`, and `Longitude` values.
- If an admin opens kit preparation for an order created from a TACEV cohort, the cohort is inferred from student `OrderId` links and selected automatically.
- Preparing kits for a TACEV cohort creates `DeliveryReceipt` kit-location events from each student's name, guardian phone, and address, so the student delivery forms are prefilled immediately after assignment.
- TACEV rental period student rows include assigned kit serial/QR plus delivery-form summary fields when the kit has been delivered or auto-filled from the student list.
- TACEV rental period student rows show only the student's defined address; delivery-form summary fields do not repeat the delivery address in the student list.
- TACEV rental period student create/edit forms and Excel import preserve student `City` and `District` as separate fields; the fields are also passed to approved-order geocoding.
- TACEV rental period student updates are handled from an in-page modal opened by compact icon-only row actions; delete, return-request, and fault actions also use compact color-coded Lucide icon buttons.
- Removing an already assigned student anonymizes the student row and hides it from the active student list, while the kit and rental assignment remain rented/reserved and appear as unassigned cohort kits.
- Customer-portal student kit returns open a prefilled return form instead of creating the request immediately; the form uses the delivery-form recipient/address when present, otherwise the student record, requires a return reason, and does not ask for map coordinates.
- When an admin accepts a kit return, the TACEV student row keeps its assigned kit serial/QR as historical context; completed-return rows disable customer fault and return-request actions.
- `ProductUnitActivity` stores chronological kit operation logs with action, description, timestamp, actor id, and actor display-name snapshot.

Faults:

- Main domain: `FaultTicket`, `FaultStatusEvent`.
- Public QR fault flow can create a new fault or update an existing open fault.
- Fault updates preserve history and now also insert a new kit location event.
- `FaultTicket.Origin` distinguishes internal, public QR form, and customer-portal fault records. Customer-portal fault creation uses the same reporter name/phone/city/district/address/description fields as the public form, without showing map/location input, and operations fault lists show the source column.
- Customer-portal fault forms prefill reporter/location fields from the linked student's delivery form when present, then fall back to the student list address and finally the customer address. The city/district selects use the public QR city-district dataset.
- Fault notification emails are queued in-process by Core API through `EmailNotificationQueue` / `EmailNotificationWorker`; public QR and customer-portal fault save flows enqueue the admin email and return without waiting for SMTP delivery.

Physical kit detail history:

- `PhysicalKitService.GetDetailAsync` now exposes separate histories for delivery/receipt events, fault records, and return-request starts.
- `PhysicalKitService.GetDetailAsync` also exposes `ActivityHistory` for the chronological kit operation log.
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
- Public QR forms treat latitude/longitude as optional and untrusted. Invalid or missing coordinates must not block saving; backend stores null coordinates when no valid map selection is provided.
- Public QR fault, delivery, and return forms now collect Turkey il/ilce with dropdowns backed by a bundled city-district dataset. Reopening the forms refills the last saved city, district, address, and any stored coordinates from the latest kit location context.
- Dashboard and portal maps read latest location events, with order delivery address as fallback for old kits with no event.
- Physical kit detail uses assignment-specific latest location for rental history, and product-unit latest location for current location.

Migration status:

- `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents` creates `KitLocationEvents`.
- The migration copies existing `KitDeliveryReceipts` rows into `KitLocationEvents` with source `DeliveryReceipt`, then drops `KitDeliveryReceipts`.
- `20260818123000_AddRentalCohortStudentCoordinates` adds nullable latitude/longitude columns to `RentalCohortStudents`.
- `20260820162000_AddFaultTicketOrigin` adds `FaultTickets.Origin` with default `Internal`.

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
- `GET/POST/PUT /api/customer-portal/rental-periods`
- `DELETE /api/customer-portal/rental-periods/{periodId}`
- `POST/PUT/DELETE /api/customer-portal/rental-periods/{periodId}/students`
- `POST /api/customer-portal/rental-periods/{periodId}/students/import`
- `POST /api/customer-portal/rental-periods/{periodId}/students/{studentId}/return`
- `POST /api/customer-portal/rental-periods/{periodId}/order`
- `POST /api/customer-portal/returns`
- `POST /api/customer-portal/returns/{returnId}/ship`
- `GET /api/customers/{customerId}/rental-periods`

Web API client:

- `KitRental.Web/src/KitRental.Web.Mvc/Services/KitRentalApiClient.cs`

The delivery context endpoint now reads the latest kit location event, not delivery receipts.

## UI And Map Notes

Map UI is rendered in MVC views and powered by:

- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/turkey-kit-map.js`
- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/public-location.js`
- `KitRental.Web/src/KitRental.Web.Mvc/Views/Operations/Dashboard.cshtml`
- `KitRental.Web/src/KitRental.Web.Mvc/Views/CustomerPortal/Index.cshtml`

Global MVC UI behavior:

- `KitRental.Web/src/KitRental.Web.Mvc/wwwroot/js/site.js` sets a page-level busy state for form submits, same-origin navigation links, and same-origin mutating `fetch` calls so backend-bound actions disable other buttons/links and show a small loader until the response navigates, completes, or the page is restored.
- Same-page, external, telephone/mail, dialog, and explicit download links are excluded from the navigation busy lock; same-page downloads recover through a fallback timeout if no navigation occurs.
- MVC confirmation prompts use SweetAlert2 through `data-confirm` on forms or submit buttons; avoid inline `onsubmit`/`onclick` browser `confirm(...)` dialogs.
- Phone number inputs use a global Turkey mask in `site.js` and the MVC `[TurkishPhone]` validation attribute. Backend domain methods normalize accepted numbers with `KitRental.SharedKernel.TurkishPhoneNumber` using `libphonenumber-csharp`, storing Turkey national format.
- User-facing button/action labels should use title case in Turkish, with each word's first letter capitalized.

Map markers depend on latitude/longitude where present. Address text still appears in marker details.

Map location response rows include `ProductModelId`, `KitSku`, `Status`, and `LocationCategory`.
`LocationCategory` is produced by backend services:

- `faulty`: open fault exists, or the unit is in maintenance/quarantine.
- `returning`: an active assignment already has a non-received `KitReturnRequest`, matching the return-process cards.
- `expired`: an active assignment is past its end date and still has no return request, matching the expired cards.
- `active`: all other active rental assignment map rows.

Operations dashboard and customer portal map filters are powered by `turkey-kit-map.js`.
Both screens expose status checkboxes for faulty, return-process, expired, and active kits; a serial-number search; and product-model checkboxes that are selected by default.
Product-model filter labels show the education set/product model name (`KitName`), not the stock code/SKU.
Filter counts are calculated from all active rental map rows, not just rows with coordinates.
Rows without latitude/longitude are not rendered as markers and are shown as a small "missing location" count below the filters.
Dashboard and customer portal map side summaries list all cities, ordered by kit count, instead of truncating to a top subset.
Dashboard and customer portal map canvases use a fixed desktop height, and the side city summary uses the same desktop height as the map canvas while scrolling independently when all cities do not fit.
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
- Updated operation dashboard, customer portal map, and physical kit details to read current location from latest location event.
- Added migration `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents`.
- Migration preserves old delivery receipt data by copying it into `KitLocationEvents` before dropping `KitDeliveryReceipts`.
- Removed automatic address geocoding from public QR flows.
- Public fault, delivery, and return forms now include a small Leaflet map with a `Konumumu bul` action. GPS can place a nearby draggable pin, users can move the pin manually, and reverse geocoding fills the free-text address plus il/ilce from the selected point.
- Removed MVC range validation from public QR form latitude/longitude fields so invalid hidden coordinates do not block form submission.
- Added map filters for faulty kits, return-process kits, active kits, serial number, and product model.
- Added a missing-location label under the map filters; map counts now include coordinate-less active rental rows while markers still require coordinates.
- Split physical kit detail and lookup history into separate card groups for deliveries, faults, and return requests.
- Added customer portal summary cards for expired rental kits, kits with started return flow, and returned kits.
- Customer portal return counts now come from `KitReturnRequest` states and rental expiry counts from active assignments whose `EndDate` is before today.
- Customer portal `Aktif Kitler` summary card now counts only healthy kits that still have an active assignment, are still in `WithCustomer`, and have no open fault.
- Dashboard and customer portal maps now classify `faulty` only from open fault tickets, and kits whose returns were already received are removed from map rows.
- Added a dedicated customer portal `Returns` page with filters for pending, in-progress, and returned states, plus expired kits that have not started a return yet.
- Added customer portal navigation entry for `İadeler`.
- Customer portal expired-rental checks now use the app server local date (`DateTime.Today`) instead of UTC so locally expired kits appear immediately after midnight.
- Customer portal returns filter now matches on a dedicated state key (`pending`/`processing`/`returned`) while the table keeps separate Turkish status labels for display.
- Customer portal return semantics are assignment-based: on Wednesday, August 12, 2026, `pending` means an active rental ended before today and still has no return form, `processing` means a return request exists regardless of due date, and `returned` means warehouse/admin accepted the return back into available stock.
- Admin dashboard now exposes `Iadeyi kabul et` for active return requests, and receiving a return is allowed from both `Requested` and `InTransit`.
- Customer portal returns list status mapping now treats `KitReturnStatus.Requested` as `processing` / `İade Sürecinde` so the list matches the summary cards once a return form exists.
- Map `returning` / `expired` categories now use the same assignment-based rules as the dashboard and customer portal cards, so the filters stay aligned.
- Top map status filters on dashboard and customer portal now render as `col-md-3` items so the four status checkboxes share a single row on medium+ widths.
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
- TACEV student Excel import reads student full name, guardian phone, address, city, and district. The Excel upload popup selects one education kit for the whole uploaded list before opening the preview screen.
- Added `RentalCohort`, `RentalCohortStudent`, and `ProductUnitActivity` persistence, repository access, and migration `20260815175751_AddRentalCohortsAndProductUnitActivitiesSnapshotFix`.
- Admin order kit preparation can select a customer's rental period; selected periods drive kit quantities from student kit choices and link created/reused units to students.
- TACEV rental period detail now has an `Onayla ve sipariş oluştur` action that confirms the student list will be locked, then creates a pending rental order for admins from the student kit totals.
- Admin kit preparation for TACEV-created orders auto-selects the source period and creates student delivery form location events from the student list.
- Customer portal no longer has a separate order-list page; the `Siparişler` menu points to rental cohorts and shows linked order approval status in a datatable-style list before opening the student list detail.
- Customer portal `Siparişler` list has a top `Yeni sipariş oluştur` button that opens a popup rental period form, where the customer can type a new period name or choose a previous period name and enter the valid rental date range; saving returns to the list with the popup closed.
- Customer portal `Siparişler` rows open the related student-list screen. On that screen, unlocked/unapproved periods expose popup actions for single student creation and Excel bulk upload; the Excel template download link and whole-list education kit dropdown live inside the Excel upload popup, and imported rows continue through the preview screen before saving.
- Customer portal order-period creation now suggests previously used period names from existing rental cohorts and still accepts a brand-new period name in the same field.
- Approved customer portal order periods lock only student-list mutations. MVC hides create/import/edit/delete actions on the student-list screen and the customer-portal API rejects matching mutation attempts, but fault reporting and return request actions stay available for linked active kits.
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
- Customer portal student-list detail supports auto-filtering by student text search, education kit, and assignment state, and paginates filtered students with 20 records per page.
- Customer portal `Kitler` list includes the assigned TACEV student name, guardian phone, and period when a kit is linked to a rental cohort student; student address is not shown in that list, though kit search still matches student fields.
- Customer portal overview map no longer renders status, serial-number, or product-model filters; it always shows all customer kit markers with coordinates, while the admin dashboard map keeps its filters.
- Customer portal `Siparişler` list now shows `Düzenle` and `Sil` actions for unapproved rental cohorts. Editing can change the period name and rental date range before admin approval; if a pending order exists, its rental period and kit quantity lines are synchronized from the cohort student list. Deleting removes the unapproved cohort and its linked unapproved order, but remains blocked after kit assignment or approval.
- Admin approval of a rental order linked to TACEV students now geocodes student addresses via Nominatim and stores successful latitude/longitude values on `RentalCohortStudents`; kit preparation copies those coordinates into generated delivery location events.
- Customer portal TACEV student Excel templates and import preview screens now include `İl` and `İlçe` columns in addition to the address column; import confirmation appends those values to the stored student address text.

2026-08-20:

- Customer portal overview summary cards now show the requested eight-card set: all non-returned rented kits, student-assigned kits, unassigned rented kits, open faults, closed faults, return-pending kits, return-process kits, and returned kits. The old `Teslim Alınmamış Kitler` overview card was removed.
- Customer portal `ActiveKitCount` now means rented kits currently assigned to a student. `UnassignedKitCount` counts currently rented kits without a student assignment, excluding returned assignments.
- Customer portal overview metric cards use a compact 8-column desktop layout at 992px and wider so all cards fit on one row at normal desktop zoom.
- Map side city/district summaries now match the map canvas height on desktop instead of extending taller than the map.
- Map filter and missing-location controls now render above the map/summary grid, so the map top edge and city/district summary top edge start on the same line in both customer and operations views.
- Rental cohort student records now persist separate `City` and `District` fields for single-entry and Excel-imported students; migration `20260820140000_AddRentalCohortStudentLocationFields` adds the columns.
- Rental cohort student entry and Excel import now use city/district dropdown-derived IDs. `RentalCohortStudents.CityId` and `DistrictId` reference the seeded `LocationCities` and `LocationDistricts` tables; the existing `City` and `District` strings remain synchronized for geocoding and historical display. Migration `20260820150000_NormalizeRentalCohortStudentLocations` seeds the Turkey catalog and backfills matching legacy names.
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

