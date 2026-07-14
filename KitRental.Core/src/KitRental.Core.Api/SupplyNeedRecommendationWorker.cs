using KitRental.Core.Application.Procurement;

public sealed class SupplyNeedRecommendationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SupplyNeedRecommendationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SupplyNeedService>()
                    .RefreshRecommendationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Günlük ihtiyaç listesi tavsiyesi güncellenemedi.");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
