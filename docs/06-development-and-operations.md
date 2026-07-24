# Geliştirme ve operasyon

## Gereksinimler

- .NET 10 SDK
- Docker Desktop veya uyumlu Docker Engine (Compose ile çalışma için)
- Docker olmadan çalışmada SQL Server LocalDB ve MongoDB 8 uyumlu sunucu

## Yerel çalıştırma

Tüm bileşenler:

```powershell
docker compose up --build
```

Docker olmadan:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project KitRental.Core/src/KitRental.Core.Infrastructure --startup-project KitRental.Core/src/KitRental.Core.Api
dotnet run --project KitRental.Identity/src/KitRental.Identity.Api --urls http://localhost:5101
dotnet run --project KitRental.Core/src/KitRental.Core.Api --urls http://localhost:5102
dotnet run --project KitRental.Gateway/src/KitRental.ApiGateway --urls http://localhost:5100
dotnet run --project KitRental.Web/src/KitRental.Web.Mvc --urls http://localhost:5200
```

Sabit `--urls` kullanımı, IDE'nin dinamik HTTPS portlarıyla oluşabilecek “address already in use” hatalarını azaltır. Bir port doluysa `Get-NetTCPConnection -LocalPort <port>` ile kullanan süreç bulunmalıdır.

## Ortam bazlı appsettings

Çalıştırılabilir dört uygulama ortak ayarları `appsettings.json`, ortama özel adresleri ise standart ASP.NET Core dosyalarında tutar:

| Uygulama | Development | Production (Docker ağı) |
|---|---|---|
| Identity API | `mongodb://localhost:27017` | `mongodb://mongo:27017` |
| Core API | `(localdb)\\MSSQLLocalDB` | `mssql:1433` |
| API Gateway | Yerel Identity/Core HTTPS adresleri | `identity:8080`, `core:8080` |
| MVC Web | Yerel Gateway HTTPS adresi | `gateway:8080` |

Compose tüm .NET container'larında `ASPNETCORE_ENVIRONMENT=Production` tanımlar. Docker Compose environment değerleri `appsettings.Production.json` değerlerinin üzerinde önceliğe sahiptir; SQL parolası ve token sırrı deployment sırasında secret/environment variable ile verilmelidir.

## Kalite kontrolleri

```powershell
dotnet build KitRental.slnx --configuration Release
dotnet test KitRental.slnx --configuration Release
dotnet format KitRental.slnx --verify-no-changes --no-restore
```

Test türleri:

| Test projesi | Amaç |
|---|---|
| `KitRental.BuildingBlocks.Tests` | Parola ve token güvenliği |
| `KitRental.Identity.UnitTests` | Kullanıcı domain kuralları |
| `KitRental.Identity.IntegrationTests` | Login, yetki ve Swagger HTTP sözleşmeleri |
| `KitRental.Core.UnitTests` | Durum makineleri, dönem ve atölye kuralları |
| `KitRental.Core.IntegrationTests` | Portal, kiralama, yaşam döngüsü, Swagger ve atölye endpoint'leri |
| `KitRental.ApiGateway.Tests` | Gateway Swagger/route sözleşmesi |

`Testing` ortamı SQL Server ve MongoDB yerine in-memory adaptörleri kullanır.

## Migration yönetimi

Yeni migration:

```powershell
dotnet tool run dotnet-ef migrations add MigrationAdi --project KitRental.Core/src/KitRental.Core.Infrastructure --startup-project KitRental.Core/src/KitRental.Core.Api --output-dir Persistence/Migrations
```

Migration dosyaları kodla birlikte commit edilmelidir. Core API normal başlangıçta bekleyen migration'ları otomatik uygular. Çoklu instance production dağıtımında eşzamanlı migration riski nedeniyle ayrı deployment job tercih edilmelidir.

## Container yapısı

Compose servisleri:

| Servis | Port | Bağımlılık |
|---|---:|---|
| `mongo` | 27017 | Kalıcı `mongo-data` volume |
| `mssql` | 1433 | Kalıcı `mssql-data` volume |
| `identity` | 5101 | Mongo health check |
| `core` | 5102 | SQL Server health check |
| `gateway` | 5100 | Identity ve Core |
| `web` | 5200 | Gateway |

Her uygulamanın bağımsız Dockerfile'ı vardır; böylece tek repository içinden dört ayrı deploy edilebilir image üretilir.

## Domain bazlı marka yayını

MVC uygulaması `Host` başlığına göre `Branding:Hosts` yapılandırmasından marka seçer. Production ortamında
`atolye.et-edu.net` ve `tacev.et-edu.net` aynı `kit_rental_web` container'ına yönlendirilmelidir.
Örnek Nginx yapılandırması:

```nginx
server {
    listen 443 ssl http2;
    server_name atolye.et-edu.net tacev.et-edu.net;

    # ssl_certificate ve ssl_certificate_key iki domain için geçerli olmalıdır.

    location / {
        proxy_pass http://kit_rental_web:93;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Her iki DNS kaydı aynı reverse proxy sunucusuna yönlendirilmelidir. Cookie için ortak `.et-edu.net`
domain'i tanımlanmadığından iki subdomain'in oturumları birbirinden ayrıdır. Resmî logolar
`wwwroot/images/brands` altına eklendikten sonra ilgili markanın `LogoPath` değeri (örneğin
`/images/brands/tacev.svg`) güncellenmelidir; boş değer marka adını yazı olarak gösterir.

## GitHub Actions ve GHCR

`.github/workflows/publish-images.yml` matrisi şu image'ları oluşturur:

- `ghcr.io/robotikbilim/kit-rental-core`
- `ghcr.io/robotikbilim/kit-rental-identity`
- `ghcr.io/robotikbilim/kit-rental-gateway`
- `ghcr.io/robotikbilim/kit-rental-web`

Davranış:

- Workflow yalnız `main` dalına merge/push tamamlandığında çalışır; pull request ve Git tag event'lerinde çalışmaz.
- Dört image yalnız `main` etiketiyle yayımlanır (`ghcr.io/robotikbilim/kit-rental-*:main`).
- `latest`, semver ve commit SHA etiketleri üretilmez.
- GitHub Actions cache her image için ayrı scope kullanır.
- Image'lara SBOM ve provenance eklenir; yayınlarda provenance attestation registry'ye gönderilir.

## Production kontrol listesi

- Geliştirme token secret ve SQL parolasını secret manager değerleriyle değiştirin.
- TLS sonlandırma, forwarded headers ve secure cookie ayarlarını deployment ortamına göre yapılandırın.
- MongoDB ve SQL Server için backup/restore prosedürü oluşturun.
- Database/readiness health check'leri ekleyin.
- Merkezi log, trace ve metrik exporter bağlayın.
- Token key rotation, refresh/revocation veya standart OIDC sağlayıcısına geçişi değerlendirin.
- Gateway için rate limiting, timeout, retry/circuit breaker ve body limit politikaları belirleyin.
- Demo data seeding'i production ortamında kapatın.
- Container package görünürlüğü ve retention ayarlarını GitHub organization politikasına göre düzenleyin.

## Dokümantasyon bakım kuralı

Aşağıdaki değişikliklerde ilgili belge aynı pull request içinde güncellenmelidir:

- yeni veya kaldırılan endpoint/rol,
- yeni application service veya domain aggregate,
- veri tabanı/migration ve veri sahipliği değişikliği,
- yeni container, port veya environment variable,
- iş akışı/durum makinesi değişikliği,
- CI/CD yayın davranışı değişikliği.
