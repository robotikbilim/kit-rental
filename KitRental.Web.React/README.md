# KitRental.Web.React

Bağımsız React + TypeScript arayüzü. MVC sunucusuna, .NET çalışma zamanına veya Core/Identity servis adreslerine ihtiyaç duymaz. Tüm iş istekleri gateway üzerinden `/core/api/*` ve `/identity/api/*` adreslerine gider.

## Çalıştırma

Node.js 22.12+ kullanın. Bu dizinde:

```sh
npm ci
cp .env.example .env.local
npm run dev
```

PowerShell'de dosyayı `Copy-Item .env.example .env.local` ile kopyalayabilirsiniz. Varsayılan geliştirme adresi `http://localhost:5173`.

Gateway adresi aşağıdaki sırayla seçilir:

1. `public/config.js`: `window.__KIT_RENTAL_CONFIG__ = { gatewayBaseUrl: "https://gateway.example.com" };`
2. `VITE_GATEWAY_BASE_URL` ortam değişkeni (derleme zamanı).
3. Giriş ekranındaki **Bağlantı Ayarları** ile tarayıcıya kaydedilen adres.

Adres `/core` veya `/api` eki içermez. Gateway bir alt dizinde barındırılıyorsa o dizin base URL'ye dahil edilebilir. Yerel gateway profili `https://localhost:61327` veya `http://localhost:61328` kullanır. HTTPS UI, HTTPS gateway gerektirir. Yerel HTTPS sertifikasının tarayıcı tarafından güvenilir olması gerekir.

## Bağımsız dağıtım

```sh
npm run build
npm run preview
```

`dist/` dizinini herhangi bir statik sunucuya yükleyin. **Tüm UI yolları `/index.html` dosyasına düşmelidir**; QR/adres ve detay bağlantılarının doğrudan açılması bunu gerektirir. UI origin kökünde barındırılır. Backend proxy'si veya MVC uygulaması gerekmez.

Derleme sonrasında `dist/config.js` dosyasındaki gateway adresi değiştirilebilir. Aynı `dist` çıktısı farklı ortamlarda yeniden derlenmeden kullanılabilir. `config.js` önbelleğe alınmamalıdır. Örnek Nginx yapılandırması `nginx.conf` dosyasındadır.

Docker alternatifi:

```sh
docker build -t kit-rental-react .
docker run --rm -p 8080:80 -v /absolute/path/config.js:/usr/share/nginx/html/config.js:ro kit-rental-react
```

Repository kuralı gereği build/derleme komutları çalıştırılmadan önce kullanıcı onayı alınır.

## Gateway ve oturum

- Gateway `StandaloneUi` CORS politikası tarayıcıdan `Authorization` başlığına ve preflight isteklerine izin verir. Varsayılan `Cors:AllowedOrigins: ["*"]`, yalnızca base URL girilerek farklı originlerden çalışmayı sağlar; cookie credentials açılmaz.
- İstenirse gateway dağıtımında `Cors__AllowedOrigins__0=https://ui.example.com` ile izin verilen origin sınırlandırılır. Değerin sonunda `/` olmamalıdır.
- Identity login yanıtındaki erişim belirteci sekmeye ve gateway adresine bağlı `sessionStorage` içinde tutulur. Çıkış, süre dolması veya korumalı bir isteğin 401 dönmesi oturumu sonlandırır. Refresh endpoint'i bulunmadığından yeniden giriş gerekir.
- Menü ve işlem görünürlüğü rollere göre ayarlanır. Nihai yetkilendirme ve müşteri izolasyonu mevcut backend servislerindedir.
- API hata ayrıntıları kullanıcıya gösterilir; veriler gelmeden örnek sayılar gösterilmez.
- Mevcut MVC katalog görselleri ve il/ilçe verileri bu projeye kopyalanmıştır; çalışma sırasında MVC'ye bağlı değildir. Harita döşemeleri OpenStreetMap, fontlar Google Fonts üzerinden gelir; sistem fontu geri dönüşü vardır.

## Ekranlar

| Alan                                                   | React yolları                                                                                 |
| ------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| Giriş / bağlantı                                       | `/login`                                                                                      |
| Yönetim özeti / kit konumları                          | `/`                                                                                           |
| Envanter / fiziksel kit / geçmiş / toplu kiralama      | `/inventory`, `/kits`, `/kits/:id`, `/lookup`, `/kits/bulk-rental`                            |
| Siparişler / öğrenci adresleri / kit hazırlama         | `/orders`, `/orders/:id`, `/purchase-order`                                                   |
| Katalog / reçete / üretilebilirlik                     | `/models`, `/models/:id`, `/manufacturing`, `/manufacturing/:id`                              |
| Komponent bulma / stok giriş-tüketim-transfer / raflar | `/components`, `/components/:id`, `/locations`                                                |
| İhtiyaç / tedarik                                      | `/supply`, `/supply/:id`                                                                      |
| Arıza / rehber / gelen iadeler                         | `/faults`, `/guides`, `/returns`                                                              |
| Müşteri / adres / hesap / denetim / e-posta            | `/customers`, `/customers/:id`, `/users`, `/audit`, `/emails`                                 |
| QR etiket yazdırma                                     | `/labels/order/:id`, `/labels/model/:id`, `/labels/kit/:id`                                   |
| Müşteri özeti / sipariş / öğrenci Excel önizlemesi     | `/portal`, `/portal/orders`, `/portal/orders/:id`                                             |
| Müşteri kit / arıza / iade                             | `/portal/kits`, `/portal/kits/:id`, `/portal/faults`, `/portal/returns`                       |
| QR giriş / süreli form / arıza / iade                  | `/ariza/:qrCode`, `/ariza/form/:token`, `/ariza/form/:token/ariza`, `/ariza/form/:token/iade` |
| Öğrenci adresi / uyumluluk teslim formu                | `/adres/:token`, `/ariza/form/:token/teslim`                                                  |

Öğrenci listeleri admin onayından sonra kilitlenir. Öğrenci adresi eksik olsa da kit atanabilir; sipariş tamamlama adresler dolmadan gönderilmez. İade edilmiş kitlerde müşteri arıza/iade eylemleri kapalıdır. Yeni QR etiket ve adres bağlantıları React originini kullanır. Önceden basılmış, MVC hostunu içeren etiketler için DNS/reverse proxy geçişi ayrıca planlanmalıdır.

## Kod haritası

- `src/api.ts`: yapılandırma, HTTP, oturum saklama, ProblemDetails, tüm sayfaları okuma.
- `src/auth.tsx`: oturum, rol kontrolleri ve rota koruması.
- `src/resources.tsx`: ekran sözleşmeleri, form alanları ve listeler.
- `src/ui.tsx`: tablo, modal, form, bildirim, Excel ve yükleme durumları.
- `src/pages/`: iş akışlarına özel ekranlar.
- `src/KitMap.tsx`: kit haritası ve isteğe bağlı cihaz konumu.
- `src/styles.css`: tasarım sistemi, responsive ve A4 yazdırma kuralları.

`npm test` yalnızca Node'un yerleşik test çalıştırıcısını kullanır; derleme yapmaz. `npm run build` TypeScript kontrolünü ve üretim paketlemesini çalıştırır. Tablolarda sunucu sayfalaması envanter/arıza/denetim için kullanılır; kalan mevcut liste endpointlerinin bütün sayfaları okunup istemcide filtrelenir. Sunucu sayfalı listelerde tablo Excel düğmesi yalnızca mevcut sayfayı, envanter üstündeki dışa aktar düğmesi backend'in tam raporunu indirir.
