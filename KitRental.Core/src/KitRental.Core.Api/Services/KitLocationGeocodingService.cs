using KitRental.Core.Application.Abstractions;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace KitRental.Core.Api;

public sealed class KitLocationGeocodingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<Guid, byte> _queued = new();

    public bool TryEnqueue(Guid locationEventId) =>
        _queued.TryAdd(locationEventId, 0) && _channel.Writer.TryWrite(locationEventId);

    public void MarkDequeued(Guid locationEventId) => _queued.TryRemove(locationEventId, out _);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class KitLocationGeocodingWorker(
    KitLocationGeocodingQueue queue,
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<KitLocationGeocodingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueuePendingEventsAsync(stoppingToken);
        using var refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5, configuration.GetValue("Gemini:QueueScanSeconds", 30))));
        var refreshTask = RefreshQueueAsync(refreshTimer, stoppingToken);

        try
        {
            await foreach (var eventId in queue.ReadAllAsync(stoppingToken))
            {
                queue.MarkDequeued(eventId);
                await ProcessAsync(eventId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            try { await refreshTask; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }

    private async Task RefreshQueueAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await EnqueuePendingEventsAsync(cancellationToken);
    }

    private async Task EnqueuePendingEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICoreRepository>();
        foreach (var locationEvent in await repository.GetKitLocationEventsAsync(cancellationToken))
        {
            if (!locationEvent.Latitude.HasValue || !locationEvent.Longitude.HasValue)
                queue.TryEnqueue(locationEvent.Id);
        }
    }

    private async Task ProcessAsync(Guid eventId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICoreRepository>();
        var locationEvent = await repository.GetKitLocationEventAsync(eventId, cancellationToken);
        if (locationEvent is null || locationEvent.Latitude.HasValue && locationEvent.Longitude.HasValue)
            return;

        try
        {
            var coordinates = await ResolveAsync(locationEvent.AddressLine, cancellationToken);
            if (coordinates is null)
            {
                logger.LogWarning("Adres için Gemini koordinat üretemedi. KitLocationEventId={EventId}", eventId);
                return;
            }

            locationEvent.SetCoordinates(coordinates.Value.Latitude, coordinates.Value.Longitude);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Kit adres koordinatları güncellendi. KitLocationEventId={EventId}", eventId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogError(exception, "Kit adresi için Gemini geocoding işlemi başarısız. KitLocationEventId={EventId}", eventId);
        }
    }

    private async Task<(double Latitude, double Longitude)?> ResolveAsync(string address,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Gemini:Enabled", true))
            return null;
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Gemini:ApiKey tanımlı değil; adres geocoding kuyruğu bekletiliyor.");
            return null;
        }

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
        if (!root.TryGetProperty("latitude", out var latitude) || !root.TryGetProperty("longitude", out var longitude) ||
            latitude.ValueKind != JsonValueKind.Number || longitude.ValueKind != JsonValueKind.Number)
            return null;
        var lat = latitude.GetDouble();
        var lon = longitude.GetDouble();
        return lat is >= -90 and <= 90 && lon is >= -180 and <= 180 ? (lat, lon) : null;
    }
}
