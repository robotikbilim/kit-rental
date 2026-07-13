# Veri, kalıcılık ve altyapı

## Veri sahipliği

| Servis | Teknoloji | Veritabanı | Sahip olunan veri |
|---|---|---|---|
| Core API | EF Core 10 + SQL Server | `KitRentalCore` | Katalog, seri numaralı kit, komponent, stok, BOM, müşteri, sipariş, kiralama, kargo, arıza, iade ve audit |
| Identity API | MongoDB Driver | `KitRentalIdentity` | Kullanıcı, parola hash'i, rol, müşteri bağı ve aktiflik |

Core ve Identity arasında database join yapılmaz. Kullanıcının Core müşteri kaydıyla bağı access token içindeki `customer_id` GUID'i üzerinden kurulur.

## Core SQL modeli

Başlıca tablolar:

| Aggregate | Ana tablolar / owned koleksiyonlar | Kritik indeks |
|---|---|---|
| Ürün kataloğu | `ProductModels` | Benzersiz SKU |
| Fiziksel kit | `ProductUnits`, `InventoryEvents` | Benzersiz seri numarası ve QR kod |
| Müşteri | `Customers`, `CustomerAddresses` | Benzersiz e-posta |
| Sipariş | `RentalOrders`, `RentalOrderLines`, `OrderStatusEvents` | Benzersiz sipariş no; müşteri+durum |
| Kiralama | `RentalAssignments` | Fiziksel kit+durum |
| Kargo | `Shipments`, `ShipmentEvents` | Benzersiz takip no; sipariş |
| Arıza | `FaultTickets`, `FaultStatusEvents` | Benzersiz arıza no; müşteri+durum |
| İade | `ReturnInspections`, `InspectionItems` | Sipariş+fiziksel kit |
| Atölye | `Components`, `StorageLocations` | Benzersiz komponent SKU ve lokasyon kodu |
| Stok | `ComponentStocks`, `StockMovements` | Komponent+lokasyon benzersiz bakiye; hareket zamanı; transfer id |
| Üretim | `BillsOfMaterials`, `BillOfMaterialsLines` | Ürün modeli+BOM sürümü |
| Audit | `AuditEntries` | Entity tipi+id+zaman |

Owned event ve satır koleksiyonları aggregate kökü üzerinden yönetilir. Sipariş adresi owned value object olarak siparişe kopyalanır.

## Repository sınırı

`ICoreRepository`, Application katmanının ihtiyaç duyduğu veri erişim portudur.

- `EfCoreRepository`: üretim/geliştirme SQL Server adaptörü.
- `InMemoryCoreRepository`: entegrasyon testlerinde hızlı ve dış bağımlılıksız adaptör.
- `TryCreateReservationAsync`: tarih çakışması kontrolü ve assignment eklemeyi tek kritik işlem olarak ele alır.
- `ApplyStockMovementsAsync`: stok hareketlerini ve lokasyon bakiyelerini birlikte uygular.
- `SaveChangesAsync`: use-case transaction sınırını tamamlar.

Core API açılışta bekleyen EF migration'larını uygular. `Persistence:SeedDemoData=true` olduğunda seeder eksik demo kayıtlarını idempotent biçimde tamamlar.

## Mongo Identity modeli

`IUserRepository` için iki adaptör bulunur:

- `MongoUserRepository`: gerçek MongoDB koleksiyonu ve benzersiz normalize e-posta indeksi.
- `InMemoryUserRepository`: `Testing` ortamı.

Başlangıç initializer'ı koleksiyon indeksini ve geliştirme amaçlı varsayılan yönetici hesabını oluşturur. Parolanın kendisi saklanmaz; yalnız PBKDF2 türevi hash kaydedilir.

## Güvenlik altyapısı

- Parola hashleme PBKDF2 ve rastgele salt kullanır.
- Token, HMAC-SHA256 ile imzalanan JWT biçimli üç parçalı belirteçtir.
- İmza karşılaştırması timing saldırılarına karşı sabit zamanlı yapılır.
- Issuer, audience ve expiration doğrulanır.
- MVC oturum cookie'si `HttpOnly` ve `SameSite=Lax` olarak ayarlanmıştır.
- API tarafındaki rol ve müşteri kapsamı kontrolleri, MVC kontrolünden bağımsız olarak zorunludur.

Not: Mevcut token uygulaması refresh token, key rotation, token iptali veya standart OIDC discovery sağlamaz. Bunlar production hardening kapsamında ayrıca tasarlanmalıdır.

## Gözlemlenebilirlik

`KitRental.Observability` aşağıdaki ortak davranışı sağlar:

- `X-Correlation-ID` kabulü veya üretimi,
- correlation kimliğini logging scope içine ekleme,
- `/health` endpoint'i için health check altyapısı.

Mevcut health check'ler uygulamanın ayakta olduğunu gösterir; SQL/Mongo/readiness için özel bağımlılık probe'u henüz tanımlı değildir. Dağıtık tracing exporter ve metrik backend'i de henüz bağlı değildir.

## Yapılandırma

Yerel varsayılanlar:

```text
SQL Server: (localdb)\MSSQLLocalDB
Database:   KitRentalCore
MongoDB:    mongodb://localhost:27017
Database:   KitRentalIdentity
```

Container ortamında configuration environment variable ile ezilir:

| Değişken | Kullanım |
|---|---|
| `KIT_RENTAL_TOKEN_SECRET` | Identity ve Core için ortak token imza sırrı |
| `KIT_RENTAL_SQL_PASSWORD` | Compose SQL Server `sa` parolası |
| `ConnectionStrings__CoreDatabase` | Core SQL bağlantısı |
| `Mongo__ConnectionString`, `Mongo__Database` | Identity Mongo bağlantısı |
| `Services__Identity`, `Services__Core` | Gateway downstream adresleri |
| `GatewayUrl` | MVC'nin Gateway adresi |

Repository'deki varsayılan secret ve parola yalnız yerel geliştirme içindir; production secret store ile değiştirilmelidir.

## Demo veri

Core seeder başlangıç kataloğunu gerçekçi örneklerle doldurur: komponentler, depo/raf lokasyonları, raf bakiyeleri, eğitim kitleri ve reçeteler. Seeder idempotent tasarlandığı için uygulamanın tekrar başlaması aynı iş anahtarlarını çoğaltmaz.

