# KitRental

Robotik kodlama eğitim setlerinin siparişten iadeye kadar izlenmesi için .NET 10 tabanlı kiralama yönetim sistemi.

Katmanlar, servis sorumlulukları, API yetkileri, veri modeli ve iş akışları için [teknik dokümantasyon dizinine](docs/README.md) bakın.

## Tamamlanan MVP kapsamı

- PBKDF2 parola güvenliği, imzalı Bearer token, rol ve müşteri hesabı izolasyonu
- Müşteri ve değiştirilemez sipariş adresi kopyası
- Ürün modeli ile seri numaralı/QR kodlu fiziksel ürün biriminin ayrılması
- Komponent kartı, minimum stok seviyesi ve raf/lokasyon bazlı miktar takibi
- Komponent adıyla görsel destekli hızlı arama ve raf bulucu ekranı
- Giriş, tüketim ve raflar arası transfer için değiştirilemez stok hareket geçmişi
- Kit reçetesi (BOM), sürümleme ve stoktan üretilebilir kit/darboğaz hesabı
- Sipariş, onay, hazırlık, çıkış kargosu, teslimat ve aktif kiralama akışı
- Tarih aralığına göre atomik çakışan rezervasyon kontrolü
- İade kargosu, depo kabulü, kontrol listesi, hasar bedeli ve ürün sonucu
- Kiralamaya bağlı arıza kaydı ve servis durum geçmişi
- Envanter ve sipariş durumlarının aktör/zaman bilgili olay geçmişi
- Yönetim panosu, envanter, sipariş ve arıza ekranları
- Identity ve Core servislerini birleştiren API Gateway
- Korelasyon kimliği, health endpoint’leri ve RFC 9457 problem yanıtları
- Docker Compose ile dört uygulamanın birlikte çalıştırılması

## Çözüm yapısı

| Proje | Görev |
|---|---|
| `KitRental.BuildingBlocks` | Ortak domain, güvenlik, sözleşme ve gözlemlenebilirlik bileşenleri |
| `KitRental.Identity` | Kullanıcı, rol, parola ve erişim belirteci servisi |
| `KitRental.Core` | Müşteri, katalog, envanter, sipariş, kiralama, kargo, arıza ve iade iş kuralları |
| `KitRental.Gateway` | `/identity/*` ve `/core/*` ters vekil giriş noktası |
| `KitRental.Web` | Sunucu taraflı yönetici/operasyon MVC portalı |

## Hızlı başlangıç

Docker ile bütün sistemi başlatmak için:

```powershell
docker compose up --build
```

Portal: `http://localhost:5200`

Atölye komponent bulucu: `http://localhost:5200/Workshop`

Eğitim kiti yönetimi: `http://localhost:5200/Catalog/Kits`

Komponent yönetimi: `http://localhost:5200/Catalog/Components`

Swagger/OpenAPI:

```text
Gateway merkezi Swagger UI: http://localhost:5100/swagger
Identity Swagger UI:         http://localhost:5101/swagger
Core Swagger UI:             http://localhost:5102/swagger
```

Gateway Swagger ekranındaki açılır doküman listesinden Gateway, Identity ve Core API tanımları arasında geçiş yapılabilir. Yetkili endpoint’leri denemek için önce Identity dokümanındaki `/api/auth/login` çağrısından token alın, ardından **Authorize** düğmesine yalnız token değerini girin.

Geliştirme yöneticisi:

```text
E-posta: admin@kitrental.local
Parola:  Admin12345!
```

Bu hesap ve varsayılan token sırrı yalnız yerel geliştirme içindir. Üretimde `KIT_RENTAL_TOKEN_SECRET` güçlü ve gizli bir değerle tanımlanmalıdır.

## Docker olmadan çalıştırma

Yerel geliştirme bağlantıları:

```text
SQL Server: (localdb)\MSSQLLocalDB
Veritabanı: KitRentalCore
Kimlik doğrulama: Windows Integrated Security

MongoDB: mongodb://localhost:27017
Veritabanı: KitRentalIdentity
Koleksiyon: users
```

Core migration'larını uygulayın ve MongoDB sunucusunun `27017` portunda çalıştığını doğrulayın:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project KitRental.Core/src/KitRental.Core.Infrastructure --startup-project KitRental.Core/src/KitRental.Core.Api
Test-NetConnection localhost -Port 27017
```

Ardından dört ayrı terminal açın:

```powershell
dotnet run --project KitRental.Identity/src/KitRental.Identity.Api --urls http://localhost:5101
dotnet run --project KitRental.Core/src/KitRental.Core.Api --urls http://localhost:5102
dotnet run --project KitRental.Gateway/src/KitRental.ApiGateway --urls http://localhost:5100
dotnet run --project KitRental.Web/src/KitRental.Web.Mvc --urls http://localhost:5200
```

## Başlıca API rotaları

Identity, Gateway üzerinden `http://localhost:5100/identity` altında:

```text
POST /api/auth/login
GET  /api/auth/me
GET  /api/users
POST /api/users
```

Core, Gateway üzerinden `http://localhost:5100/core` altında:

```text
POST /api/product-models
POST /api/product-units
GET  /api/product-units
POST /api/components
GET  /api/components
GET  /api/components/low-stock
POST /api/storage-locations
GET  /api/storage-locations
POST /api/component-stock/receipts
POST /api/component-stock/consumptions
POST /api/component-stock/transfers
GET  /api/component-stock
GET  /api/component-stock/movements
POST /api/product-models/{id}/bom
GET  /api/product-models/{id}/bom
GET  /api/manufacturing/buildable-kits
GET  /api/manufacturing/buildable-kits/{productModelId}
POST /api/kits
POST /api/customers
GET  /api/customers
POST /api/orders
GET  /api/orders
POST /api/orders/{id}/transitions
POST /api/rental-assignments
POST /api/shipments
POST /api/shipments/{id}/events
GET  /api/orders/{id}/shipments
POST /api/faults
GET  /api/faults
POST /api/faults/{id}/status
POST /api/return-inspections
GET  /api/dashboard
GET  /api/audit
GET  /api/reports/inventory.csv
```

## Derleme ve test

```powershell
dotnet build KitRental.slnx --configuration Release
dotnet test KitRental.slnx --configuration Release
dotnet format KitRental.slnx --verify-no-changes --no-restore
```

Testler parola/token güvenliğini, müşteri rol kapsamını, Identity girişini, ürün durum makinesini, rezervasyon çakışmasını ve siparişten iade kontrolüne tam kiralama yaşam döngüsünü kapsar.

## Kalıcılık notu

Core operasyon verileri EF Core üzerinden SQL Server'da, kullanıcı ve rol verileri MongoDB'de kalıcıdır. Core API açılışta bekleyen migration'ları uygular; Identity API benzersiz e-posta indeksini ve yalnız geliştirme amaçlı yönetici hesabını oluşturur. `Testing` ortamında entegrasyon testlerinin yalıtımı için bellek içi adaptörler kullanılmaya devam eder.

Yerel ayarlarda `Persistence:SeedDemoData=true` olduğu için Core API; eksik demo kayıtlarını idempotent olarak tamamlar. Başlangıç kataloğunda 20 komponent, 8 depo/raf lokasyonu, 40 raf bakiyesi, 6 eğitim kiti ve bunların üretim reçeteleri bulunur.

Yeni Core migration'ı oluşturmak için:

```powershell
dotnet tool run dotnet-ef migrations add MigrationAdi --project KitRental.Core/src/KitRental.Core.Infrastructure --startup-project KitRental.Core/src/KitRental.Core.Api --output-dir Persistence/Migrations
```
