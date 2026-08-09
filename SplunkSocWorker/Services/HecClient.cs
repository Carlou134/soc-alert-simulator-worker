using System.Text;
using System.Text.Json;
using SplunkSocWorker.Models;

namespace SplunkSocWorker.Services;

public class HecClient : IHecClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HecClient> _logger;

    public HecClient(HttpClient httpClient, ILogger<HecClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendBulkAsync(IEnumerable<HecEvent> events, CancellationToken ct)
    {
        var ndjson = string.Join('\n', events.Select(e => JsonSerializer.Serialize(e)));
        using var content = new StringContent(ndjson, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("services/collector", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Splunk HEC rechazo el lote ({StatusCode}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Splunk HEC respondio: {Body}", body);
    }
}
