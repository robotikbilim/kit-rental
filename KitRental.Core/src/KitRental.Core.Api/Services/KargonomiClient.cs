using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using KitRental.Core.Application.Common;
using KitRental.Core.Application.Kargonomi;

namespace KitRental.Core.Api.Services;

public sealed class KargonomiOptions
{
    public string BaseUrl { get; set; } = "https://app.kargonomi.com.tr/api/v1";
    public string ApiToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Robotik Bilim";
    public string SenderEmail { get; set; } = "admin@robotikbilim.com.tr";
    public string SenderPhone { get; set; } = string.Empty;
    public string SenderTaxNumber { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    public int SenderStateId { get; set; }
    public int SenderCityId { get; set; }
}

public sealed class KargonomiClient(HttpClient httpClient, IConfiguration configuration, IMemoryCache? memoryCache = null) : IKargonomiClient
{
    private readonly KargonomiOptions options = configuration.GetSection("Kargonomi").Get<KargonomiOptions>() ?? new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMemoryCache regionCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());

    public KargonomiReturnDestination GetReturnDestination() =>
        new(options.SenderName, options.SenderPhone, KargonomiAddressSanitizer.Clean(options.SenderAddress),
            options.SenderStateId, options.SenderCityId);

    public async Task<KargonomiShipmentSnapshot> CreateShipmentAsync(KargonomiCreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            shipment = new
            {
                sender_name = options.SenderName,
                sender_email = options.SenderEmail,
                sender_phone = ToKargonomiMobilePhone(options.SenderPhone, "Gönderici telefon numarası"),
                sender_address = KargonomiAddressSanitizer.Clean(options.SenderAddress),
                sender_state_id = options.SenderStateId,
                sender_city_id = options.SenderCityId,
                warehouse_id = ParseNullableInt(options.WarehouseId),
                buyer_name = request.BuyerName,
                buyer_phone = ToKargonomiMobilePhone(request.BuyerPhone, "Alıcı telefon numarası"),
                buyer_address = KargonomiAddressSanitizer.Clean(request.BuyerAddress),
                buyer_state_id = request.BuyerStateId,
                buyer_city_id = request.BuyerCityId,
                packages = new[] { new { content = request.PackageContent, barcode = request.PackageBarcode, desi = request.PackageDesi } }
            }
        };
        return ParseShipment(await SendAsync(HttpMethod.Post, "shipments", payload, cancellationToken));
    }

    public async Task<KargonomiShipmentSnapshot> CreateReturnShipmentAsync(KargonomiReturnShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            shipment = new
            {
                sender_name = request.SenderName,
                sender_email = options.SenderEmail,
                // Reverse shipments omit warehouse_id, so Kargonomi requires the configured sender identity.
                sender_tax_number = GetSenderTaxNumber(options.SenderTaxNumber),
                sender_phone = ToKargonomiMobilePhone(request.SenderPhone, "İade gönderen telefon numarası"),
                sender_address = KargonomiAddressSanitizer.Clean(request.SenderAddress),
                sender_state_id = request.SenderStateId,
                sender_city_id = request.SenderCityId,
                // Kargonomi uses warehouse_id to replace sender address fields from the warehouse record.
                // Reverse shipments must keep the selected fault/return address as the courier pickup.
                buyer_name = options.SenderName,
                buyer_phone = ToKargonomiMobilePhone(options.SenderPhone, "İade teslim alıcısı telefon numarası"),
                buyer_address = KargonomiAddressSanitizer.Clean(options.SenderAddress),
                buyer_state_id = options.SenderStateId,
                buyer_city_id = options.SenderCityId,
                packages = new[] { new { content = request.PackageContent, barcode = request.PackageBarcode, desi = request.PackageDesi } }
            }
        };
        return ParseShipment(await SendAsync(HttpMethod.Post, "shipments", payload, cancellationToken));
    }

    public async Task<IReadOnlyCollection<KargonomiCarrierQuote>> GetPriceQuotesAsync(int shipmentId,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await SendAsync(HttpMethod.Get, $"shipment-price-comparison/{shipmentId}", null, cancellationToken));
        if (!document.RootElement.TryGetProperty("shipping_provider_with_price", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
            return [];
        return quotes.EnumerateArray().Select(item => new KargonomiCarrierQuote(
            ReadInt(item, "id"), ReadString(item, "name") ?? string.Empty, ReadString(item, "slug") ?? string.Empty,
            ReadString(item, "price"))).ToArray();
    }

    public async Task<KargonomiShipmentSnapshot> ConfirmShippingPriceAsync(int shipmentId, int providerId,
        CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["shipment_id"] = shipmentId.ToString(CultureInfo.InvariantCulture),
            ["shipping_provider_id"] = providerId.ToString(CultureInfo.InvariantCulture)
        });
        return ParseShipment(await SendAsync(HttpMethod.Post, "confirm-shipping-price", form, cancellationToken));
    }

    public async Task<KargonomiShipmentSnapshot> GetShipmentAsync(int shipmentId, CancellationToken cancellationToken) =>
        ParseShipment(await SendAsync(HttpMethod.Get, $"shipments/{shipmentId}", null, cancellationToken));

    public async Task<IReadOnlyCollection<KargonomiShipmentListSnapshot>> GetShipmentsAsync(
        CancellationToken cancellationToken)
    {
        var shipments = new List<KargonomiShipmentListSnapshot>();
        var page = 1;
        var lastPage = 1;

        do
        {
            using var document = JsonDocument.Parse(await SendAsync(
                HttpMethod.Get, $"shipments?page={page}", null, cancellationToken));
            var root = document.RootElement;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                shipments.AddRange(data.EnumerateArray().Select(ParseShipmentListItem));

            lastPage = root.TryGetProperty("meta", out var meta)
                ? Math.Max(page, ReadInt(meta, "last_page"))
                : page;
            page++;
        } while (page <= lastPage);

        return shipments;
    }

    public async Task<string> GetBarcodeAsync(int shipmentId, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await SendAsync(HttpMethod.Get, $"shipments/{shipmentId}/barcode?format=pdf", null, cancellationToken));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.String) return root.GetString() ?? string.Empty;
        foreach (var name in new[] { "data", "barcode", "file", "content" })
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        throw new InvalidOperationException("Kargonomi barkod yanıtı okunamadı.");
    }

    public Task<IReadOnlyCollection<KargonomiRegionResponse>> GetStatesAsync(CancellationToken cancellationToken) =>
        GetRegionsAsync("states/1", cancellationToken);

    public Task<IReadOnlyCollection<KargonomiRegionResponse>> GetCitiesAsync(int stateId, CancellationToken cancellationToken)
    {
        if (stateId <= 0)
            throw new ConflictException("kargonomi.state_required", "Lütfen il seçin.");
        return GetRegionsAsync($"cities/{stateId}", cancellationToken);
    }

    public async Task<(int StateId, int CityId)> ResolveLocationAsync(int? cityId, int? districtId,
        CancellationToken cancellationToken)
    {
        if (cityId is null or <= 0 || districtId is null or <= 0)
            throw new ConflictException("kargonomi.address_region_missing",
                "Gönderi adresinde il/ilçe bilgisi eksik. Adres formundan il ve ilçeyi seçerek adresi yeniden kaydedin.");
        var states = await GetStatesAsync(cancellationToken);
        if (!states.Any(item => item.Id == cityId))
            throw new ConflictException("kargonomi.state_not_found", "Seçilen il Kargonomi listesinde bulunamadı.");
        var cities = await GetCitiesAsync(cityId.Value, cancellationToken);
        if (!cities.Any(item => item.Id == districtId))
            throw new ConflictException("kargonomi.city_not_found", "Seçilen ilçe bu ile ait değil. İl ve ilçeyi yeniden seçin.");
        return (cityId.Value, districtId.Value);
    }

    private async Task<IReadOnlyCollection<KargonomiRegionResponse>> GetRegionsAsync(string path,
        CancellationToken cancellationToken)
    {
        var key = $"kargonomi-regions:{options.BaseUrl}:{path}";
        if (regionCache.TryGetValue<IReadOnlyCollection<KargonomiRegionResponse>>(key, out var cached) && cached is not null)
            return cached;
        var json = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root :
            root.TryGetProperty("data", out var data) ? data : default;
        if (items.ValueKind != JsonValueKind.Array)
            throw new HttpRequestException("Kargonomi il/ilçe listesi okunamadı.");
        var regions = items.EnumerateArray().Select(item => new KargonomiRegionResponse(
            ReadInt(item, "id"), ReadString(item, "name") ?? ReadString(item, "title") ?? string.Empty))
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name)).ToArray();
        if (regions.Length == 0)
            throw new HttpRequestException("Kargonomi il/ilçe listesi boş döndü. Lütfen tekrar deneyin.");
        regionCache.Set<IReadOnlyCollection<KargonomiRegionResponse>>(key, regions, TimeSpan.FromHours(24));
        return regions;
    }

    private async Task<string> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        const int maximumRateLimitRetries = 3;
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), path));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
            if (body is HttpContent content) request.Content = content;
            else if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var contentText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && method == HttpMethod.Get &&
                attempt < maximumRateLimitRetries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow
                    ?? TimeSpan.FromSeconds(10);
                retryAfter = TimeSpan.FromSeconds(Math.Clamp(retryAfter.TotalSeconds, 1, 30));
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Kargonomi API hatası ({(int)response.StatusCode}): {contentText}");
            return contentText;
        }
    }

    private static KargonomiShipmentSnapshot ParseShipment(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("shipment", out var shipment)) root = shipment;
        return new(ReadInt(root, "id"), ReadString(root, "status"), ReadString(root, "status_label"),
            ReadString(root, "shipping_provider_name"), ReadString(root, "shipping_webservice_tracking_code"),
            ReadString(root, "shipping_webservice_barcode"), ReadDate(root, "updated_at"));
    }

    private static KargonomiShipmentListSnapshot ParseShipmentListItem(JsonElement item)
    {
        var buyer = item.TryGetProperty("buyer", out var buyerElement) && buyerElement.ValueKind == JsonValueKind.Object
            ? buyerElement
            : default;
        var packages = item.TryGetProperty("shipment_packages", out var packageItems) &&
                       packageItems.ValueKind == JsonValueKind.Array
            ? packageItems.GetArrayLength()
            : 0;
        var packageCount = ReadInt(item, "package_count");

        return new KargonomiShipmentListSnapshot(
            ReadInt(item, "id"),
            ReadString(item, "buyer_name") ?? ReadString(buyer, "buyer_name") ?? "-",
            ReadString(buyer, "buyer_phone"),
            ReadString(buyer, "buyer_address") ?? string.Empty,
            ReadString(buyer, "buyer_state"),
            ReadString(buyer, "buyer_city"),
            ReadString(item, "shipping_webservice_tracking_code"),
            ReadString(item, "shipping_provider_name"),
            ReadString(item, "status"),
            ReadString(item, "status_label") ?? ReadString(item, "status") ?? "Bilinmiyor",
            packageCount > 0 ? packageCount : packages,
            ReadDate(item, "created_at"),
            ReadDate(item, "updated_at"));
    }

    private static string GetSenderTaxNumber(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 10 or > 11 || (digits.Length == 11 && !HasValidTurkishIdentityChecksum(digits)))
            throw new ConflictException("kargonomi.sender_tax_number_invalid",
                "Kargonomi gönderici numarası, göndericiye ait 10 haneli vergi numarası veya kontrol basamakları geçerli 11 haneli T.C. kimlik numarası olarak yapılandırılmalıdır.");

        return digits;
    }

    private static bool HasValidTurkishIdentityChecksum(string value)
    {
        if (value.Length != 11 || value[0] == '0') return false;
        var digits = value.Select(character => character - '0').ToArray();
        var oddPositionSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenPositionSum = digits[1] + digits[3] + digits[5] + digits[7];
        var tenthDigit = ((oddPositionSum * 7 - evenPositionSum) % 10 + 10) % 10;
        var eleventhDigit = digits.Take(10).Sum() % 10;
        return digits[9] == tenthDigit && digits[10] == eleventhDigit;
    }

    private static string ToKargonomiMobilePhone(string value, string fieldName)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 14 && digits.StartsWith("0090", StringComparison.Ordinal))
            digits = digits[4..];
        else if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0'))
            digits = digits[1..];

        if (digits.Length != 10 || digits[0] != '5')
            throw new ConflictException("kargonomi.mobile_phone_invalid",
                $"{fieldName} 05xx xxx xx xx biçiminde geçerli bir cep telefonu olmalıdır.");

        return digits;
    }
    private static int ParseNullableInt(string value) => int.TryParse(value, out var result) ? result : 0;
    private static int ReadInt(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : int.TryParse(ReadString(item, name), out result) ? result : 0;
    private static string? ReadString(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static DateTimeOffset? ReadDate(JsonElement item, string name) => DateTimeOffset.TryParse(ReadString(item, name), out var result) ? result : null;
}
