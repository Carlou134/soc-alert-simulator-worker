using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SplunkSocWorker.Tests;

public class DatasetEndpointsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _storagePath;
    private readonly HttpClient _client;

    private const string ValidHeader =
        "event_category,protocol,traffic_type,mitre_tactic,kill_chain_stage,severity,ids_ips_alert," +
        "asset_criticality,log_source,firewall_action,failed_login_attempts,request_rate_per_min," +
        "has_threat_family,evidence_role,os_family,correlation_id,anomaly_score,attack_type," +
        "attack_signature,malware_indicator,label";

    private const string ValidRow =
        "intrusion_attempt,tcp,ssh,initial access,initial access,high,ET MALWARE Suspicious User-Agent," +
        "CRITICAL,PaloAlto-Firewall,DENY,12,35.45,0,attacker,windows,INC-001,0.42,Brute Force," +
        "SSH_AUTH_FAILED,Mirai_Variant_X,SUSPICIOUS";

    public DatasetEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"dataset_endpoints_test_{Guid.NewGuid():N}.json");

        var isolatedFactory = factory.WithWebHostBuilder(builder =>
        {
            // Nunca queremos que el BatchSenderWorker real corra durante estos tests, ni que
            // toquen el Data/alerts_pool.json de un dev real - cada test corre aislado.
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Batch:Enabled"] = "false",
                    ["Dataset:StoragePath"] = _storagePath,
                });
            });
        });

        _client = isolatedFactory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        if (File.Exists(_storagePath))
            File.Delete(_storagePath);
    }

    private static MultipartFormDataContent BuildUpload(string content, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Upload_ValidCsv_Returns200WithLoadedCount()
    {
        using var form = BuildUpload($"{ValidHeader}\n{ValidRow}\n{ValidRow}\n", "dataset.csv");

        var response = await _client.PostAsync("/api/v1/dataset/upload", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("loaded").GetInt32());
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        using var form = BuildUpload(string.Empty, "dataset.csv");

        var response = await _client.PostAsync("/api/v1/dataset/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns400()
    {
        using var form = BuildUpload($"{ValidHeader}\n{ValidRow}\n", "dataset.xlsx");

        var response = await _client.PostAsync("/api/v1/dataset/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_CsvMissingRequiredColumn_Returns500WithCleanJsonError()
    {
        // El header no trae "severity" - CsvHelper tira HeaderValidationException, que debe
        // ser atrapada por GlobalExceptionHandler y devuelta como JSON limpio, no stack trace.
        var headerWithoutSeverity = ValidHeader.Replace("severity,", "");
        using var form = BuildUpload($"{headerWithoutSeverity}\n", "dataset.csv");

        var response = await _client.PostAsync("/api/v1/dataset/upload", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("error", out _));
        Assert.True(body.TryGetProperty("traceId", out _));
    }
}
