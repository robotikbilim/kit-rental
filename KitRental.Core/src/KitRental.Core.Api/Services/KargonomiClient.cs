using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    public string SenderAddress { get; set; } = string.Empty;
    public int SenderStateId { get; set; }
    public int SenderCityId { get; set; }
}

public sealed class KargonomiClient(HttpClient httpClient, IConfiguration configuration) : IKargonomiClient
{
    private readonly KargonomiOptions options = configuration.GetSection("Kargonomi").Get<KargonomiOptions>() ?? new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public KargonomiReturnDestination GetReturnDestination() =>
        new(options.SenderName, options.SenderPhone, options.SenderAddress);

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
                sender_address = options.SenderAddress,
                sender_state_id = options.SenderStateId,
                sender_city_id = options.SenderCityId,
                warehouse_id = ParseNullableInt(options.WarehouseId),
                buyer_name = request.BuyerName,
                buyer_phone = ToKargonomiMobilePhone(request.BuyerPhone, "Alıcı telefon numarası"),
                buyer_address = request.BuyerAddress,
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
                sender_phone = ToKargonomiMobilePhone(request.SenderPhone, "İade gönderen telefon numarası"),
                sender_address = request.SenderAddress,
                sender_state_id = request.SenderStateId,
                sender_city_id = request.SenderCityId,
                warehouse_id = ParseNullableInt(options.WarehouseId),
                buyer_name = options.SenderName,
                buyer_phone = ToKargonomiMobilePhone(options.SenderPhone, "İade teslim alıcısı telefon numarası"),
                buyer_address = options.SenderAddress,
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

    public async Task<(int StateId, int CityId)> ResolveLocationAsync(string address, CancellationToken cancellationToken)
    {
        var parts = address.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[0].Contains('/'))
            throw new ConflictException("kargonomi.address_region_missing", "Adres şehir / ilçe formatında olmalıdır.");
        var region = parts[0].Split('/', 2, StringSplitOptions.TrimEntries);
        var states = await SendAsync(HttpMethod.Get, "states/1", null, cancellationToken);
        using var stateDocument = JsonDocument.Parse(states);
        var state = FindByName(stateDocument.RootElement, region[0]);
        if (state.Id <= 0) throw new ConflictException("kargonomi.state_not_found", $"Şehir bulunamadı: {region[0]}");
        var cities = await SendAsync(HttpMethod.Get, $"cities/{state.Id}", null, cancellationToken);
        using var cityDocument = JsonDocument.Parse(cities);
        var city = FindByName(cityDocument.RootElement, region[1]);
        if (city.Id <= 0) throw new ConflictException("kargonomi.city_not_found", $"İlçe bulunamadı: {region[1]}");
        return (state.Id, city.Id);
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

    private static (int Id, string Name) FindByName(JsonElement root, string name)
    {
        var items = root.ValueKind == JsonValueKind.Array ? root : root.TryGetProperty("data", out var data) ? data : default;
        if (items.ValueKind != JsonValueKind.Array) return (0, string.Empty);
        var normalized = Normalize(name);
        foreach (var item in items.EnumerateArray())
            if (Normalize(ReadString(item, "name") ?? ReadString(item, "title") ?? string.Empty) == normalized)
                return (ReadInt(item, "id"), ReadString(item, "name") ?? string.Empty);
        return (0, string.Empty);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant().Replace('İ', 'I');
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
