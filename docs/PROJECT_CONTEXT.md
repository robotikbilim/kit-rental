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
- Customer allowed product model rows use client-generated `CustomerAllowedProductModel.Id` values so replacing the allowed-kit list in the admin customer edit flow does not create EF owned-entity tracking key collisions.
- Main service: `OperationsService`.

Rentals:

- Main domain: `RentalAssignment`, `RentalPeriod`.
- Reservation overlap is handled atomically in repository methods such as `TryCreateReservationAsync` and `TryCreateReservationsAsync`.
- Customer/TACEV rental planning uses `RentalCohort` with owned `RentalCohortStudent` rows for named date ranges and student kit choices.
- Customer/TACEV period names are persisted on `RentalCohort.Name`; the customer portal order-period form offers distinct previous period names as selectable suggestions while still allowing a new name to be typed.
- TACEV can create a rental order from a rental cohort in the customer portal. Active students are linked to the created order through `RentalCohortStudent.OrderId`.
- Admin rental order creation collects one education kit selection plus student full name/guardian phone rows, creates an order-linked `RentalCohort`, and computes the order quantity from the student count.
- Order-linked rental cohort students can start without an address. Public student address links under `/adres/{token}` collect the current free-text address and optional coordinates into the student row; admin order detail and customer portal order-period detail show student address status, addresses, and copyable links.
- Public student address collection treats coordinates as optional: an open address is sufficient, and missing/invalid map coordinates are ignored instead of blocking save. The MVC form also offers city/district dropdowns from `wwwroot/js/turkey-address-dropdowns.js`; selected city/district are folded into the saved free-text address rather than stored in separate schema columns.
- Admin order detail and customer portal order-period detail can export the order student/address-link list as Excel, including student full name, phone, kit, address status, address, and public link.
- Admin kit preparation for order-linked student cohorts can continue even when some student addresses are still missing.
- Admin order details use a shortened order flow: approve the incoming order, create/reserve kits, then complete the order directly. The previous admin "prepare for shipment" and "mark shipped" actions are no longer shown; completion requires every order student to have an address, writes each assigned student's address to `KitLocationEvents`, moves reserved rental kits to customer/rented state, and moves purchase kits to sold state without requiring shipment statuses.
- Admin kit preparation assigns physical kits to order-linked students but no longer writes student address location events at reservation time; student kit location is written when the order is completed.
- If a student address is edited from the public address form after the order is already completed, the kit's latest location history is updated from that new address.
- In the customer portal, rental cohorts are presented as `Siparişler`: the former customer `Orders` page redirects to `RentalPeriods`, and the list shows each cohort's linked order number plus approved/unapproved state. The detail action opens the cohort's student list.
- In the customer portal `Siparişler` list, delivered/active/completed linked orders (`Delivered`, `RentalActive`, `Completed`) display the detail label `Tamamlandı`.
- Rental cohort responses include `IsApproved`; once the linked order reaches `Approved` or any later non-cancelled/non-rejected status, the customer portal treats the student list/order as locked only for student-list mutations. Student add/update/import/delete and order-period plan edits are hidden in MVC and rejected by the application service/API, while linked-kit fault reporting and return request flows remain available.
- Admin approval and kit preparation for orders linked to TACEV rental cohorts do not geocode student addresses; the student free-text address is used as entered.
- Admin order kit preparation can select a customer's rental cohort. When selected, kit quantities are calculated from unassigned cohort students, and reserved/created kits are linked to the matching students.
- When admin kit preparation assigns a rental cohort student to a kit, the generated `KitLocationEvent` uses the student's free-text address and does not copy separate student regional fields or coordinates.
- If an admin opens kit preparation for an order created from a TACEV cohort, the cohort is inferred from student `OrderId` links and selected automatically.
- Preparing kits for a TACEV cohort assigns kits to students without requiring addresses. Completing the order creates `DeliveryReceipt` kit-location events from each student's name, guardian phone, and address, and completion is blocked until every order student has an address.
- Order-scoped QR label printing keeps the physical kit serial number and QR code unchanged, but includes the currently assigned cohort student's name and guardian phone when a kit is linked to a TACEV order; student address is intentionally not printed on the label. Printed labels use the ordering customer's name as the label heading and the "Arıza bildirimi veya iade için okutun" instruction; non-order label printing falls back to `Robotik Bilim`. Print CSS pins the A4 layout to fixed-width label cards instead of allowing mobile rules to collapse labels to a single column.
- TACEV rental period student rows include assigned kit serial/QR plus delivery-form summary fields when the kit has been delivered or auto-filled from the student list.
- TACEV rental period student rows show address status and address in separate columns; rows created for public address collection show that the address is still pending, and the single-line student filter bar includes address-status filtering.
- Admin and customer student/address tables keep address and public-link cells empty when there is no value; filled cells expose full values through compact popup buttons and copyable public links. Global MVC table/form styling loads at 90% zoom by default, keeps table rows, filters, dropdowns, and action buttons compact, and preserves full-height/responsive desktop sidebar behavior when the sidebar is collapsed.
- TACEV rental period student create/edit forms and Excel import collect student full name and guardian phone without requiring address. Separate regional student fields are no longer stored.
- TACEV rental period student updates are handled from an in-page modal opened by compact icon-only row actions; delete, return-request, and fault actions also use compact color-coded Lucide icon buttons.
- Removing an already assigned student anonymizes the student row and hides it from the active student list, while the kit and rental assignment remain rented/reserved and appear as unassigned cohort kits.
- Customer-portal student kit returns open a prefilled return form instead of creating the request immediately; the form uses the delivery-form recipient/address when present, otherwise the student record, requires a return reason, and does not ask for map coordinates.
- When an admin accepts a kit return, the TACEV student row keeps its assigned kit serial/QR as historical context; completed-return rows disable customer fault and return-request actions.
- `ProductUnitActivity` stores chronological kit operation logs with action, description, timestamp, actor id, and actor display-name snapshot.

Faults:

- Main domain: `FaultTicket`, `FaultStatusEvent`.
- Public QR fault flow can create a new fault or update an existing open fault.
- Fault updates preserve history and now also insert a new kit location event.
- `FaultTicket.Origin` distinguishes internal, public QR form, and customer-portal fault records. Customer-portal fault creation uses reporter name, phone, free-text address, and description fields, and operations fault lists show the source column.
- Customer-portal fault forms prefill reporter/address fields from the linked student's delivery form when present, then fall back to the student list address and finally the customer address.
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
- `KitLocationEvents` rows without coordinates are resolved through the configured Gemini model from the admin dashboard `Kit Konumlarını Güncelle` action. The manual action checks each physical kit's latest address event and updates that event's latitude/longitude when the address is filled and coordinates are missing.
- Gemini geocoding is configured under `Gemini` in Core API configuration. `Gemini:ApiKey` must be supplied through user secrets, environment variables, or deployment secret storage; it is intentionally empty in tracked `appsettings.json` and must never be committed.
- Delivery form inserts a `KitLocationEvent` with source `DeliveryReceipt`.
- Public fault creation inserts source `FaultReport`.
- Public fault update inserts source `FaultUpdate`.
- Existing open public fault edits also insert source `FaultUpdate`.
- Public return request inserts source `ReturnRequest`.
- Public QR form access now uses 24-hour random access tokens. The printed QR still opens `/ariza/{qrCode}`, but MVC immediately requests `POST /api/public/form-access/{qrCode}` and redirects to `/ariza/form/{token}`. Public fault, return, delivery context, fault-guide, and submit endpoints resolve the token server-side before using the kit QR code. Tokens are stored only as SHA-256 hashes in `PublicFormAccessTokens` with `ExpiresAt`; successful submissions do not invalidate tokens before expiry.
- Public return requests store `DeliveryMethod` (`Adresimden Alınsın` or `Kendim Bırakacağım`). Drop-off returns do not show the fixed Aras Kargo return code until the form is saved; after save, the public success page shows a pop-up with code `1234567890`. Drop-off returns do not require pickup address/map fields and store the Aras drop-off instruction as the return address.
- Reopening the public QR return form before admin return receipt loads the active return request through `/api/public/returns/context/{token}` and allows updating the return reason, requester details, pickup/drop-off delivery method, and pickup location fields instead of creating a duplicate return.
- Public QR forms treat latitude/longitude as optional and untrusted. Invalid or missing coordinates must not block saving; backend stores null coordinates when no valid map selection is provided.
- Public QR fault, delivery, and return forms collect free-text address plus optional latitude/longitude only. Reopening the forms with a valid token refills the last saved address and any stored coordinates from the latest kit location context.
- The public QR landing screen offers only fault reporting and kit return; the user-facing `Kit Teslim Al` option is hidden because admins mark customer delivery automatically. The public delivery endpoint and MVC action still exist for internal compatibility.
- Dashboard and portal maps read latest location events, with order delivery address as fallback for old kits with no event.
- Physical kit detail uses assignment-specific latest location for rental history, and product-unit latest location for current location.
- Operations dashboard rental expiry cards show only counts; clicking expired or upcoming counts opens the inventory list filtered by `rentalExpiry=expired` or `rentalExpiry=upcoming`.
- Inventory supports a rental-expiry filter for active customer rentals and shows customer, order number, rental end date, and remaining/overdue days when rental information is present.
- Operations dashboard no longer renders the return-delivery table inline; the `Gelen Teslimat` attention card opens `Operations/Returns`, which lists active kit return requests and allows receiving them.
- Operations dashboard renders the kit location map as the final dashboard group after the summary/card sections.

Migration status:

- `20260812211046_ReplaceKitDeliveryReceiptsWithLocationEvents` creates `KitLocationEvents`.
- The migration copies existing `KitDeliveryReceipts` rows into `KitLocationEvents` with source `DeliveryReceipt`, then drops `KitDeliveryReceipts`.
- `20260818123000_AddRentalCohortStudentCoordinates` adds nullable latitude/longitude columns to `RentalCohortStudents`.
- `20260820162000_AddFaultTicketOrigin` adds `FaultTickets.Origin` with default `Internal`.
- `20260820222749_AddPublicFormAccessTokens` adds `PublicFormAccessTokens` for 24-hour hashed public form access tokens.
- `20260907120000_AddStudentAddressCollectionTokens` adds `RentalCohortStudents.PublicAddressToken` and `AddressSubmittedAt` for order-specific public student address collection.

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

Customer portal map filters are powered by `turkey-kit-map.js`.
The customer portal map exposes status checkboxes for faulty, return-process, expired, and active kits; a serial-number search; and product-model checkboxes that are selected by default.
The operations dashboard kit location map intentionally has no visible filters and shows every kit with coordinates.
Product-model filter labels show the education set/product model name (`KitName`), not the stock code/SKU.
Filter counts are calculated from all active rental map rows, not just rows with coordinates.
Rows without latitude/longitude are not rendered as markers and are shown as a small "missing location" count near the map.
Dashboard and customer portal maps no longer render regional side summaries; the map canvas uses the full map layout width.
Dashboard and customer portal map canvases use a fixed desktop height.
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
- Public fault, delivery, and return forms now include a small Leaflet map with a `Konumumu bul` action. GPS can place a nearby draggable pin, users can move the pin manually, and reverse geocoding fills only the free-text address from the selected point.
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
- TACEV student Excel import reads student full name and guardian phone. The Excel upload popup selects one education kit for the whole uploaded list before opening the preview screen.
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
- Customer portal student-list detail supports auto-filtering by student text search, education kit, assignment state, and address state, and paginates filtered students with 20 records per page.
- Customer portal `Kitler` list includes the assigned TACEV student name, guardian phone, and period when a kit is linked to a rental cohort student; student address is not shown in that list, though kit search still matches student fields.
- Customer portal overview map no longer renders status, serial-number, or product-model filters; it always shows all customer kit markers with coordinates, while the admin dashboard map keeps its filters.
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
- Admin order detail and customer portal order-period student tables show public address links through the shared `Göster` text-preview popup only; the popup's copy action is used instead of a separate row-level copy button.
- Customer portal order-period student tables display address status and address in separate columns, with an `Adres Durumu` filter for completed vs pending public addresses.
- MVC tables and filter/form panels are globally compacted for the admin and customer portals. Student address and public-link columns render a one-line truncated preview in the row and open the full value in the shared text-preview popup, so long values do not change table row height or width.
- Admin kit preparation for order-linked student cohorts no longer waits for all public student addresses. Students without addresses can still receive a physical kit assignment, but order completion is blocked until all student addresses are present; completion writes the student addresses into kit location history.
- Added migration `20260907120000_AddStudentAddressCollectionTokens` for student public address token and submission timestamp columns.
- Customer portal student create/edit and Excel import now allow orders to be sent for approval with only student full name and guardian phone; address fields are left empty until public address collection is completed.
- Customer portal student Excel import preview shows the total parsed student count above the preview table.
- Customer portal rental-period detail no longer exposes a customer-side `Onayla Ve Sipariş Oluştur` action. When the first student is added or students are imported, a linked `PendingApproval` rental order is created automatically so the order appears in the admin panel immediately; later student add/update/delete operations before admin approval synchronize the linked order's product-model quantities. Once admin approval moves the order past `PendingApproval`, customer-side period and student mutations remain blocked.
- QR label print CSS keeps the same visual proportions as the on-screen label cards: A4 print still uses a three-column grid, but normal and student-recipient labels preserve their own card, QR, spacing, and text scale ratios instead of being forced into a taller shared print size.
- On admin order details, the `Siparişi Tamamla` control remains clickable when student addresses are missing, but it shows the popup warning `Eksik adres bilgisi olan kayıtlar var, önce adresleri doldurun.` instead of submitting the completion transition. Once every student has an address, the normal completion form is shown.
- Admin order detail paginates the student address table and the order-linked physical kit table independently with `studentPage` and `kitPage` query parameters, showing 10 rows per card and preserving the other card's current page while navigating.
- Admin dashboard now exposes `Kit Konumlarını Güncelle` for `SystemAdmin` and `OperationsManager`. It calls Core API `POST /api/dashboard/kit-locations/update`, checks each kit's latest filled `KitLocationEvents` address, and updates missing latitude/longitude values through Gemini geocoding.
- Data migration `20260907143000_SeedRedKitFaultGuides` replaces red-kit fault-guide seed rows with active kit-specific troubleshooting entries for DHT11, LDR, PIR, Ultrasonik Sensör, POT, Buton, RGB LED, LED, LED / PWM, Buzzer, and LCD. The migration resolves the red-kit product model by SKU/name/image URL and removes matching legacy general seed titles before inserting the new list.

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

