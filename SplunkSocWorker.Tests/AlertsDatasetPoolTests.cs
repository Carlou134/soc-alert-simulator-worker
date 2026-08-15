using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SplunkSocWorker.Models;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.Tests;

public class AlertsDatasetPoolTests : IDisposable
{
    private readonly string _storagePath;
    private readonly AlertsDatasetPool _pool;

    public AlertsDatasetPoolTests()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"alerts_pool_test_{Guid.NewGuid():N}.json");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dataset:StoragePath"] = _storagePath
            })
            .Build();

        _pool = new AlertsDatasetPool(configuration, NullLogger<AlertsDatasetPool>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_storagePath))
            File.Delete(_storagePath);
    }

    private static List<AlertRecord> MakeRecords(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new AlertRecord { CorrelationId = $"alert-{i}" })];

    [Fact]
    public async Task ReplaceAllAsync_UpdatesCount_AndPersistsToDisk()
    {
        await _pool.ReplaceAllAsync(MakeRecords(5));

        Assert.Equal(5, _pool.Count);
        Assert.True(File.Exists(_storagePath));
    }

    [Fact]
    public async Task Constructor_LoadsExistingDatasetFromDisk()
    {
        await _pool.ReplaceAllAsync(MakeRecords(3));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Dataset:StoragePath"] = _storagePath })
            .Build();
        var reloadedPool = new AlertsDatasetPool(configuration, NullLogger<AlertsDatasetPool>.Instance);

        Assert.Equal(3, reloadedPool.Count);
    }

    [Fact]
    public async Task TakeRandomBatchAsync_ConsumesRecords_DoesNotResample()
    {
        await _pool.ReplaceAllAsync(MakeRecords(10));

        var firstBatch = await _pool.TakeRandomBatchAsync(4);
        var secondBatch = await _pool.TakeRandomBatchAsync(4);

        Assert.Equal(4, firstBatch.Count);
        Assert.Equal(4, secondBatch.Count);
        Assert.Equal(2, _pool.Count);

        var firstIds = firstBatch.Select(r => r.CorrelationId);
        var secondIds = secondBatch.Select(r => r.CorrelationId);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [Fact]
    public async Task TakeRandomBatchAsync_WhenPoolSmallerThanRequestedSize_TakesAllAvailable()
    {
        await _pool.ReplaceAllAsync(MakeRecords(3));

        var batch = await _pool.TakeRandomBatchAsync(30);

        Assert.Equal(3, batch.Count);
        Assert.Equal(0, _pool.Count);
    }

    [Fact]
    public async Task TakeRandomBatchAsync_OnEmptyPool_ReturnsEmptyList()
    {
        var batch = await _pool.TakeRandomBatchAsync(10);

        Assert.Empty(batch);
    }

    [Fact]
    public async Task WaitForChangeAsync_ReturnsTrue_WhenReplaceAllAsyncIsCalled()
    {
        var waitTask = _pool.WaitForChangeAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await _pool.ReplaceAllAsync(MakeRecords(1));

        var wasSignaled = await waitTask;
        Assert.True(wasSignaled);
    }

    [Fact]
    public async Task WaitForChangeAsync_ReturnsFalse_OnTimeoutWithNoUpload()
    {
        var wasSignaled = await _pool.WaitForChangeAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.False(wasSignaled);
    }
}
