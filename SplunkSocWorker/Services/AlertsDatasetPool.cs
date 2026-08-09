using System.Text.Json;
using SplunkSocWorker.Models;

namespace SplunkSocWorker.Services;

public class AlertsDatasetPool
{
    private readonly string _storagePath;
    private readonly ILogger<AlertsDatasetPool> _logger;
    private readonly object _lock = new();
    private readonly Random _random = new();
    private List<AlertRecord> _records = [];

    public AlertsDatasetPool(IConfiguration configuration, ILogger<AlertsDatasetPool> logger)
    {
        _logger = logger;
        _storagePath = configuration["Dataset:StoragePath"] ?? "Data/alerts_pool.json";
        LoadFromDisk();
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _records.Count;
            }
        }
    }

    public async Task ReplaceAllAsync(List<AlertRecord> records, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records = records;
        }

        await SaveToDiskAsync(records, ct);
    }

    public List<AlertRecord> TakeRandomBatch(int size)
    {
        lock (_lock)
        {
            if (_records.Count == 0)
                return [];

            var effectiveSize = Math.Min(size, _records.Count);
            return [.. _records.OrderBy(_ => _random.Next()).Take(effectiveSize)];
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_storagePath))
            return;

        try
        {
            var json = File.ReadAllText(_storagePath);
            var records = JsonSerializer.Deserialize<List<AlertRecord>>(json);
            if (records is not null)
            {
                _records = records;
                _logger.LogInformation("Dataset restaurado desde disco: {Count} alertas.", records.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo restaurar el dataset desde {Path}.", _storagePath);
        }
    }

    private async Task SaveToDiskAsync(List<AlertRecord> records, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(records);
        await File.WriteAllTextAsync(_storagePath, json, ct);
    }
}
