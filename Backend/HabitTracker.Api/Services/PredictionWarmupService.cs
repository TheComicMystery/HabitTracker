namespace HabitTracker.Api.Services;

public class PredictionWarmupService : BackgroundService
{
    private readonly HabitPredictionService _predictionService;
    private readonly ILogger<PredictionWarmupService> _logger;

    public PredictionWarmupService(
        HabitPredictionService predictionService,
        ILogger<PredictionWarmupService> logger)
    {
        _predictionService = predictionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        if (stoppingToken.IsCancellationRequested) return;

        try
        {
            await _predictionService.TrainModelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Prediction warmup skipped.");
        }
    }
}
