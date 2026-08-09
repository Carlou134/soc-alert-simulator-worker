using SplunkSocWorker.Models;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.BackgroundServices;

public class BatchSenderWorker : BackgroundService
{
    private readonly AlertsDatasetPool _pool;
    private readonly IHecClient _hecClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BatchSenderWorker> _logger;

    public BatchSenderWorker(
        AlertsDatasetPool pool,
        IHecClient hecClient,
        IConfiguration configuration,
        ILogger<BatchSenderWorker> logger)
    {
        _pool = pool;
        _hecClient = hecClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue("Batch:IntervalMinutes", 5);
        var minSize = _configuration.GetValue("Batch:MinSize", 20);
        var maxSize = _configuration.GetValue("Batch:MaxSize", 30);
        var index = _configuration["Hec:Index"] ?? "soc_alerts";
        var sourcetype = _configuration["Hec:Sourcetype"] ?? "_json";

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        var random = new Random();

        do
        {
            if (_pool.Count == 0)
            {
                _logger.LogWarning("No hay dataset cargado, se omite el lote. Subilo via POST /api/v1/dataset/upload.");
                continue;
            }

            var batchSize = random.Next(minSize, maxSize + 1);
            var batch = _pool.TakeRandomBatch(batchSize);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var events = batch.Select(record => new HecEvent
            {
                Time = now,
                Index = index,
                Sourcetype = sourcetype,
                Event = record
            });

            try
            {
                await _hecClient.SendBulkAsync(events, stoppingToken);
                _logger.LogInformation("Lote de {Count} alertas enviado a Splunk.", batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo el envio del lote a Splunk HEC.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
