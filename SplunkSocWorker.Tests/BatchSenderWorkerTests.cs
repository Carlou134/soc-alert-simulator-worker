using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SplunkSocWorker.BackgroundServices;
using SplunkSocWorker.Models;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.Tests;

public class BatchSenderWorkerTests : IDisposable
{
    private sealed class FakeHecClient : IHecClient
    {
        public int CallCount { get; private set; }
        public List<HecEvent> LastEvents { get; private set; } = [];

        public Task SendBulkAsync(IEnumerable<HecEvent> events, CancellationToken ct)
        {
            CallCount++;
            LastEvents = [.. events];
            return Task.CompletedTask;
        }
    }

    private readonly string _storagePath;

    public BatchSenderWorkerTests()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"batch_worker_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_storagePath))
            File.Delete(_storagePath);
    }

    private (BatchSenderWorker worker, AlertsDatasetPool pool, FakeHecClient hecClient) CreateWorker(
        bool enabled, int intervalMinutes = 60)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dataset:StoragePath"] = _storagePath,
                ["Batch:Enabled"] = enabled.ToString(),
                ["Batch:IntervalMinutes"] = intervalMinutes.ToString(),
                ["Batch:MinSize"] = "1",
                ["Batch:MaxSize"] = "3",
            })
            .Build();

        var pool = new AlertsDatasetPool(configuration, NullLogger<AlertsDatasetPool>.Instance);
        var hecClient = new FakeHecClient();
        var worker = new BatchSenderWorker(pool, hecClient, configuration, NullLogger<BatchSenderWorker>.Instance);
        return (worker, pool, hecClient);
    }

    [Fact]
    public async Task WhenDisabled_NeverCallsHecClient()
    {
        var (worker, pool, hecClient) = CreateWorker(enabled: false);
        await pool.ReplaceAllAsync([new AlertRecord { CorrelationId = "1" }]);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, hecClient.CallCount);
    }

    [Fact]
    public async Task WhenEnabledAndPoolEmpty_NeverCallsHecClient()
    {
        var (worker, _, hecClient) = CreateWorker(enabled: true);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, hecClient.CallCount);
    }

    [Fact]
    public async Task WhenEnabledAndPoolHasData_SendsBatchAndConsumesPool()
    {
        var (worker, pool, hecClient) = CreateWorker(enabled: true);
        await pool.ReplaceAllAsync([
            new AlertRecord { CorrelationId = "1" },
            new AlertRecord { CorrelationId = "2" },
        ]);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(hecClient.CallCount >= 1);
        Assert.True(pool.Count < 2);
    }
}
