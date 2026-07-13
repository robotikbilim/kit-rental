# API ve yetkilendirme

## Adresleme

| Uygulama | Yerel adres | Açıklama |
|---|---|---|
| Gateway | `http://localhost:5100` | Dış API giriş noktası ve merkezi Swagger |
| Identity | `http://localhost:5101` | Doğrudan geliştirme adresi |
| Core | `http://localhost:5102` | Doğrudan geliştirme adresi |
| Web | `http://localhost:5200` | MVC portal |

Gateway üzerinden Identity rotalarının önüne `/identity`, Core rotalarının önüne `/core` gelir. Örneğin doğrudan Core rotası `/api/components`, Gateway üzerinden `/core/api/components` olur.

Swagger adresleri:

- Gateway: `/swagger`
- Identity: `/identity/swagger/v1/swagger.json`
- Core: `/core/swagger/v1/swagger.json`

## Token ve claim modeli

Access token aşağıdaki claim'leri taşır:

| Claim | Amaç |
|---|---|
| `sub` | Kullanıcı GUID'i ve audit aktörü |
| `email` | Kullanıcı e-postası |
| `role` | Tek uygulama rolü |
| `customer_id` | Müşteri kullanıcılarında veri izolasyonu |
| `iss`, `aud`, `iat`, `exp` | Token veren, hedef, üretim ve son kullanım zamanı |

Issuer `KitRental.Identity`, audience `KitRental`, algoritma HS256 ve varsayılan ömür 8 saattir. Üretimde `Security:TokenSecret`/`KIT_RENTAL_TOKEN_SECRET` mutlaka gizli ve güçlü bir değerle değiştirilmelidir.

## Roller

| Rol | Temel yetki alanı |
|---|---|
| `SystemAdmin` | Tüm yönetim ve kullanıcı işlemleri |
| `OperationsManager` | Katalog, kiralama, sipariş ve operasyon yönetimi |
| `WarehouseStaff` | Fiziksel kit, komponent, raf, stok, kargo ve iade işlemleri |
| `ServiceTechnician` | Arıza/servis durumları |
| `CustomerAccountManager` | Bağlı müşteri hesabının kiralama ve arıza işlemleri |
| `CustomerUser` | Bağlı müşteri hesabının portal işlemleri |
| `Auditor` | Audit ve sınırlı operasyon görüntüleme |

## Identity API

| Metot ve rota | Yetki | Amaç |
|---|---|---|
| `POST /api/auth/login` | Anonim | E-posta/parola ile token üretir. |
| `GET /api/auth/me` | Giriş yapmış kullanıcı | Token claim'lerini döndürür. |
| `GET /api/users` | `SystemAdmin` | Kullanıcıları listeler. |
| `POST /api/users` | `SystemAdmin` | Rol ve opsiyonel müşteri bağıyla kullanıcı oluşturur. |
| `GET /health` | Anonim | Servis health durumunu döndürür. |

## Core API

Core `/api` grubunun tamamı Bearer authentication gerektirir.

### Müşteri portalı

| Metot ve rota | Yetki | Amaç |
|---|---|---|
| `GET /api/customer-portal` | Müşteri rolleri | Hesaba ait portal özetini getirir. |
| `POST /api/customer-portal/rental-requests` | Müşteri rolleri | Kiralama talebi oluşturur. |
| `POST /api/customer-portal/faults` | Müşteri rolleri | Kiralanan kit için arıza açar. |
| `GET /api/order-summaries` | Giriş yapmış kullanıcı | Token müşteri kapsamına göre sipariş özetlerini döndürür. |

### Katalog, fiziksel kit ve atölye

| Rota grubu | Yetki | Amaç |
|---|---|---|
| `GET/POST /api/product-models` | Okuma: giriş; yazma: operasyon | Eğitim kiti model kataloğu |
| `GET/POST /api/product-units` | Okuma: giriş; yazma: depo | Seri numaralı fiziksel kitler |
| `GET /api/physical-kits/*` | Depo rolleri | Liste, dashboard ve geçmiş detayı |
| `POST /api/physical-kits/{id}/rent` | Operasyon rolleri | Seri numaralı kiti hızlı kiralama |
| `GET/POST /api/components` | Depo rolleri | Komponent kataloğu ve düşük stok |
| `GET /api/components/search` | Depo rolleri | Ad/SKU autocomplete araması |
| `GET /api/components/{id}/locator` | Depo rolleri | Raf bazlı miktar ve görsel |
| `GET/POST /api/storage-locations` | Depo rolleri | Depo/koridor/raf/göz tanımları |
| `/api/component-stock/*` | Depo rolleri | Mal kabul, tüketim, transfer, bakiye ve hareketler |
| `GET/POST /api/product-models/{id}/bom` | Depo rolleri | Aktif reçete ve yeni sürüm |
| `GET /api/manufacturing/buildable-kits[/{id}]` | Depo rolleri | Stoktan üretilebilirlik hesabı |
| `POST /api/kits` | Operasyon rolleri | Kiti ilk reçetesiyle birlikte oluşturma |

### Operasyon, servis ve rapor

| Rota grubu | Yetki | Amaç |
|---|---|---|
| `GET/POST /api/customers` | Operasyon rolleri | Müşteri yönetimi |
| `GET/POST /api/orders` | Giriş + müşteri kapsamı | Sipariş listeleme/oluşturma |
| `POST /api/orders/{id}/transitions` | Operasyon rolleri | Sipariş durum değişikliği |
| `POST /api/rental-assignments` | Operasyon rolleri | Fiziksel kit rezervasyonu |
| `/api/shipments/*` | Depo rolleri | Kargo ve hareket kaydı |
| `GET/POST /api/faults` | Liste: kapsamlı; oluşturma: giriş | Arıza kaydı |
| `POST /api/faults/{id}/status` | Sistem/operasyon/servis | Servis durum değişikliği |
| `POST /api/return-inspections` | Depo rolleri | İade kontrolünü tamamlama |
| `GET /api/dashboard` | Operasyon rolleri | Yönetim sayaçları |
| `GET /api/audit` | `SystemAdmin`, `Auditor` | Audit geçmişi |
| `GET /api/reports/inventory.csv` | Operasyon rolleri | Fiziksel envanter CSV'si |
| `GET /health` | Anonim | Servis health durumu |

## Hata sözleşmesi

Core, hataları `application/problem+json` Problem Details biçimine dönüştürür.

| Durum | Kaynak | Örnek kod |
|---|---|---|
| `400` | Domain doğrulama hatası | Domain exception kodu |
| `403` | Müşteri kapsamı veya erişim ihlali | `resource.forbidden` |
| `404` | İstenen entity yok | `resource.not_found` |
| `409` | Tekillik, stok, rezervasyon veya iş kuralı çakışması | Servise özgü conflict kodu |
| `500` | Beklenmeyen hata | `server.error` |

Her gözlemlenebilir istek yanıtında `X-Correlation-ID` bulunur. İstemci bu header'ı gönderirse değer korunur; göndermezse servis üretir.

## MVC sayfa yetkileri

- `/Catalog`, `/Workshop`, `/Manufacturing`, `/PhysicalKits`: sistem, operasyon veya depo rolleri.
- `/CustomerPortal`: yalnız müşteri rolleri.
- `/Operations`: rol bazında dashboard, sipariş ve servis işlemleri.
- Tüm POST form işlemleri anti-forgery token doğrular.
- Yetkisiz kullanıcı `/account/login`, rolü yetersiz kullanıcı `/account/access-denied` adresine yönlendirilir.

