using KitRental.Core.Application.Abstractions;
using System.Net.Http.Json;
using System.Text.Json;

namespace KitRental.Core.Api;

public sealed record KitLocationGeocodingResult(
    int LatestAddressCount,
    int CandidateCount,
    int UpdatedCount,
    int UnresolvedCount,
    int FailedCount,
    bool IsConfigured);

public sealed class KitLocationGeocodingService(
    ICoreRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<KitLocationGeocodingService> logger)
{
    public async Task<KitLocationGeocodingResult> UpdateLatestMissingCoordinatesAsync(
        CancellationToken cancellationToken)
    {
        var latestLocations = (await repository.GetKitLocationEventsAsync(cancellationToken))
            .GroupBy(location => location.ProductUnitId)
            .Select(group => group.OrderByDescending(location => location.OccurredAt)
                .ThenByDescending(location => location.Id).First())
            .Where(location => !string.IsNullOrWhiteSpace(location.AddressLine))
            .ToArray();
        var candidates = latestLocations
            .Where(location => !location.Latitude.HasValue || !location.Longitude.HasValue)
            .ToArray();
        if (candidates.Length == 0)
            return new KitLocationGeocodingResult(latestLocations.Length, 0, 0, 0, 0, IsConfigured());
        if (!IsConfigured())
        {
            logger.LogWarning("Gemini geocoding yapılandırması eksik veya kapalı; kit konumları güncellenemedi.");
            return new KitLocationGeocodingResult(latestLocations.Length, candidates.Length, 0, 0,
                candidates.Length, false);
        }

        var updated = 0;
        var unresolved = 0;
        var failed = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                var coordinates = await ResolveAsync(candidate.AddressLine, cancellationToken);
                if (coordinates is null)
                {
                    unresolved++;
                    logger.LogWarning("Adres için Gemini koordinat üretemedi. KitLocationEventId={EventId}",
                        candidate.Id);
                    continue;
                }

                var locationEvent = await repository.GetKitLocationEventAsync(candidate.Id, cancellationToken);
                if (locationEvent is null || locationEvent.Latitude.HasValue && locationEvent.Longitude.HasValue)
                    continue;
                locationEvent.SetCoordinates(coordinates.Value.Latitude, coordinates.Value.Longitude);
                await repository.SaveChangesAsync(cancellationToken);
                updated++;
                logger.LogInformation("Kit adres koordinatları güncellendi. KitLocationEventId={EventId}",
                    candidate.Id);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
            {
                failed++;
                logger.LogError(exception,
                    "Kit adresi için Gemini geocoding işlemi başarısız. KitLocationEventId={EventId}",
                    candidate.Id);
            }
        }

        return new KitLocationGeocodingResult(latestLocations.Length, candidates.Length, updated, unresolved, failed,
            true);
    }

    private bool IsConfigured() =>
        configuration.GetValue("Gemini:Enabled", true) &&
        !string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"]);

    private async Task<(double Latitude, double Longitude)?> ResolveAsync(string address,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Gemini:ApiKey"]!;
        var model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        var client = httpClientFactory.CreateClient("gemini");
        var prompt = "Aşağıdaki Türkiye adresinin enlem ve boylamını bul. " +
            "Yalnızca JSON döndür: {\"latitude\": number, \"longitude\": number}. " +
            "Adres kesin olarak belirlenemiyorsa veya Türkiye dışında ise null döndür: " +
            "{\"latitude\": null, \"longitude\": null}. Adres: " + address;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1beta/models/{model}:generateContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        });
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text)) return null;
        var json = text.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal).Trim();
        using var result = JsonDocument.Parse(json);
        var root = result.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;
        if (!root.TryGetProperty("latitude", out var latitude) || !root.TryGetProperty("longitude", out var longitude) ||
            latitude.ValueKind != JsonValueKind.Number || longitude.ValueKind != JsonValueKind.Number)
            return null;
        var lat = latitude.GetDouble();
        var lon = longitude.GetDouble();
        return lat is >= -90 and <= 90 && lon is >= -180 and <= 180 ? (lat, lon) : null;
    }
}
