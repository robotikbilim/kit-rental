# Core uygulama servisleri

Bu doküman `KitRental.Core.Application` ve `KitRental.Identity.Application` içindeki servislerin mevcut sorumluluklarını açıklar. Uygulama servisleri HTTP bilmez; repository portları ve domain modelleri üzerinden use-case yürütür.

## InventoryService

Amaç: eğitim kiti katalog modellerini ve seri numaralı fiziksel kit kayıtlarını yönetmek.

| İşlem | Davranış ve kurallar |
|---|---|
| `CreateModelAsync` | Ad, SKU, açıklama ve görselle `ProductModel` oluşturur; SKU tekilliğini conflict'e çevirir; audit yazar. |
| `GetModelsAsync` / `GetModelAsync` | Kit kataloğunu listeler veya tek modeli getirir. Bulunamayan kayıt 404 olur. |
| `CreateUnitAsync` | Var olan modele seri numarası ve QR kodla fiziksel birim ekler; seri/QR tekilliğini korur; ilk durum `Available` olur. |
| `GetUnitsAsync` | Tüm fiziksel birimleri temel kimlik ve durum bilgileriyle döndürür. |

## WorkshopService

Amaç: komponent kataloğu, raf/lokasyon stokları, stok hareketleri, reçeteler ve üretilebilirlik hesabını tek atölye/depo use-case sınırında yönetmek.

### Komponent ve raf işlemleri

- Komponent oluştururken SKU tekilliğini, birim ve negatif olmayan minimum stok değerini domain ile doğrular.
- Komponent listesinde tüm raflardaki toplamı hesaplar; `toplam <= minimum stok` ise düşük stok kabul eder.
- Hızlı arama en az iki karakter ister, ad veya SKU içinde arar, ada başlangıç eşleşmesini öne alır ve sonucu 1–20 aralığında sınırlar.
- Raf bulucu komponent görselini, toplam stok miktarını ve depo/koridor/raf/göz ayrıntılı tüm bakiyeleri döndürür.
- Lokasyon kodu benzersizdir.

### Stok hareketleri

| İşlem | Hareket tipi | Etki |
|---|---|---|
| Mal kabul | `Receipt` | Seçilen raf bakiyesini artırır. |
| Tüketim | `Consumption` | Yeterli stok varsa bakiyeyi azaltır. |
| Transfer | `TransferOut` + `TransferIn` | Aynı `TransferId` ile kaynak ve hedefte iki hareket oluşturur. |

Kaynak ve hedef aynı olamaz. Komponent/lokasyon varlığı ve pozitif miktar doğrulanır. Repository hareketleri ve bakiyeyi birlikte uygular; yetersiz stokta işlem başarısız olur.

### Reçete ve üretilebilirlik

- `CreateBomAsync`, var olan ürün modeli için sürümlü reçete oluşturur.
- `(ProductModelId, Version)` benzersizdir; reçetedeki tüm komponentlerin var olması gerekir.
- `CreateKitAsync`, ürün modeli ve ilk BOM sürümünü tek use-case içinde oluşturur.
- Aktif reçete, ilgili ürünün en güncel aktif sürümüdür.
- Üretilebilir kit sayısı her reçete satırı için `floor(toplam stok / kit başına ihtiyaç)` hesaplanıp minimum kapasitenin alınmasıyla bulunur.
- Sonuç her komponentin mevcut miktarını, tekil kapasitesini, darboğaz ve düşük stok durumunu, bir sonraki kiti üretebilmek için eksik miktarı içerir.

## RentalAssignmentService

Amaç: onaylanmış sipariş satırına belirli tarih aralığı için seri numaralı fiziksel kit atamak.

Servis şu kontrolleri yapar:

1. Sipariş satırı, müşteri ve fiziksel kit var olmalıdır.
2. Atanan fiziksel kit, sipariş satırındaki ürün modeliyle eşleşmelidir.
3. Tarih aralığı geçerli olmalıdır.
4. Aynı fiziksel kit için çakışan aktif rezervasyon oluşmamalıdır.
5. Başarıda birim `Reserved` durumuna geçirilir ve audit kaydı oluşturulur.

Çakışma kontrolü `ICoreRepository.TryCreateReservationAsync` ile kalıcılık sınırında atomik yapılır.

## PhysicalKitService

Amaç: fiziksel kitlerin operasyon ekranlarına uygun toplu görünümünü, detay geçmişini ve hızlı kiralama akışını sağlamak.

| İşlem | Çıktı/etki |
|---|---|
| `GetDashboardAsync` | Toplam, hazır/kiralanabilir, kirada ve bakımda adetleri ile seri numaralı listeleri üretir. |
| `GetListAsync` | Model adı/görseli, seri numarası, QR kod, durum ve aktif kiralama özetini birleştirir. |
| `GetDetailAsync` | Birim durum geçmişi, tüm kiralama geçmişi ve bu birime bağlı arıza geçmişini getirir. |
| `RentAsync` | E-posta ile müşteri bulur veya oluşturur; adres, sipariş ve atamayı üretir; fiziksel kiti rezerve eder. |

Hızlı kiralama use-case'i müşteri, teslimat adresi, sipariş ve seri numaralı atamayı tek uygulama operasyonunda koordine eder.

## CustomerPortalService

Amaç: TACEV/müşteri rollerine yalnız bağlı oldukları `CustomerId` kapsamındaki verileri sunmak.

- Portal özetinde müşteri, adresler, siparişler, kiralanan fiziksel kitler ve arıza kayıtları bir araya getirilir.
- Yeni kiralama talebi `PendingApproval` sipariş olarak oluşturulur ve yönetici sipariş listesine düşer.
- Müşteri arıza açarken seçilen assignment'ın kendi hesabına ait olduğu doğrulanır; order ve product unit bilgileri assignment'tan türetilir.
- Sipariş özetleri kullanıcı token'ındaki müşteri claim'i varsa otomatik filtrelenir.

## OperationsService

Amaç: müşteri, sipariş, kargo, arıza, iade ve yönetim panosu use-case'lerini yürütmek.

### Müşteri ve sipariş

- Müşteriyi ilk teslimat adresiyle oluşturur; e-posta tekilliğini korur.
- Sipariş numarası üretir, adres snapshot'ı alır, kiralama dönemi ve satırlarını doğrular.
- Sipariş durum değişikliklerini domain durum makinesine devreder ve audit kaydeder.

### Kargo

- Sipariş veya arıza ile ilişkili çıkış/iade/değişim kargosu oluşturur; takip numarası benzersizdir.
- Kargo `Delivered` olduğunda çıkış için siparişi teslim/aktif kiralama, fiziksel kiti müşteri yanında durumuna taşır.
- İade kargosu teslim edildiğinde siparişi ve fiziksel kiti inceleme aşamasına geçirir.

### Arıza ve servis

- Arızanın müşteri, sipariş, atama ve fiziksel kit zinciriyle tutarlı olmasını zorunlu kılar.
- Arıza numarası üretir; kategori, önem, açıklama ve durum geçmişini saklar.
- Servis durum değişikliklerini aktör, zaman ve not ile kaydeder.

### İade kontrolü

- Kontrol listesi, eksik/hasarlı işaretleri, notlar ve hasar bedelini saklar.
- Sonuca göre fiziksel kit durumunu günceller ve siparişi tamamlar.

### Dashboard

Müşteri sayısı, toplam fiziksel kit, açık sipariş, açık arıza ve bakımdaki kit sayısını hesaplar.

## ReportingService

Amaç: audit geçmişi ve dışa aktarılabilir envanter raporu sağlamak.

- Audit kayıtlarını repository üzerinden listeler.
- Fiziksel kit envanterini UTF-8 BOM içeren CSV olarak üretir.
- Spreadsheet formula injection riskine karşı `=`, `+`, `-`, `@` ile başlayan alanların başına tek tırnak ekler ve CSV escaping uygular.

## IdentityService

Amaç: kullanıcı kimlik doğrulaması ve kullanıcı yönetimi.

- Login sırasında e-postayı normalize eder, aktif kullanıcıyı bulur ve PBKDF2 hash'i doğrular.
- Başarılı girişte 8 saat ömürlü, HS256 imzalı access token üretir.
- Kullanıcı oluştururken en az 10 karakter parola, benzersiz e-posta ve role göre müşteri bağı doğrulaması uygular.
- Kullanıcı oluşturma ve listeleme API'leri yalnız `SystemAdmin` rolüne açıktır.

