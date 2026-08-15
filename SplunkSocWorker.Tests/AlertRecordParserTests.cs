using System.Text;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.Tests;

public class AlertRecordParserTests
{
    private const string ValidHeader =
        "event_category,protocol,traffic_type,mitre_tactic,kill_chain_stage,severity,ids_ips_alert," +
        "asset_criticality,log_source,firewall_action,failed_login_attempts,request_rate_per_min," +
        "has_threat_family,evidence_role,os_family,correlation_id,anomaly_score,attack_type," +
        "attack_signature,malware_indicator,label";

    private const string ValidRow =
        "intrusion_attempt,tcp,ssh,initial access,initial access,high,ET MALWARE Suspicious User-Agent," +
        "CRITICAL,PaloAlto-Firewall,DENY,12,35.45,0,attacker,windows,INC-001,0.42,Brute Force," +
        "SSH_AUTH_FAILED,Mirai_Variant_X,SUSPICIOUS";

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void FromCsv_ParsesRequiredFields_WithCorrectTypes()
    {
        using var stream = ToStream($"{ValidHeader}\n{ValidRow}\n");

        var records = AlertRecordParser.FromCsv(stream);

        var record = Assert.Single(records);
        Assert.Equal("intrusion_attempt", record.EventCategory);
        Assert.Equal(12, record.FailedLoginAttempts);
        Assert.Equal(35.45, record.RequestRatePerMin);
        Assert.Equal("INC-001", record.CorrelationId);
        Assert.Equal(0.42, record.AnomalyScore);
    }

    [Fact]
    public void FromCsv_WithoutMitreTechniquesColumn_DoesNotThrow()
    {
        // Regresion: el dataset real de prueba (93 columnas, mitre_t* one-hot) no trae
        // mitre_techniques. Sin [Optional] en AlertRecord esto tiraba HeaderValidationException.
        using var stream = ToStream($"{ValidHeader}\n{ValidRow}\n");

        var records = AlertRecordParser.FromCsv(stream);

        Assert.Equal(string.Empty, records[0].MitreTechniques);
    }

    [Fact]
    public void FromCsv_WithMitreTechniquesColumn_ParsesIt()
    {
        var header = ValidHeader + ",mitre_techniques";
        var row = ValidRow + ",T1110;T1078.004";
        using var stream = ToStream($"{header}\n{row}\n");

        var records = AlertRecordParser.FromCsv(stream);

        Assert.Equal("T1110;T1078.004", records[0].MitreTechniques);
    }

    [Fact]
    public void FromCsv_MissingRequiredColumn_Throws()
    {
        var headerWithoutSeverity = ValidHeader.Replace("severity,", "");
        using var stream = ToStream($"{headerWithoutSeverity}\n");

        Assert.Throws<CsvHelper.HeaderValidationException>(() => AlertRecordParser.FromCsv(stream));
    }

    [Fact]
    public async Task FromJsonAsync_ParsesArrayOfAlerts()
    {
        const string json = """
        [
          {
            "event_category": "Network Security",
            "protocol": "TCP",
            "severity": "HIGH",
            "failed_login_attempts": 12,
            "request_rate_per_min": 600,
            "label": "SUSPICIOUS"
          }
        ]
        """;
        using var stream = ToStream(json);

        var records = await AlertRecordParser.FromJsonAsync(stream);

        var record = Assert.Single(records);
        Assert.Equal("Network Security", record.EventCategory);
        Assert.Equal(600, record.RequestRatePerMin);
        Assert.Equal("SUSPICIOUS", record.Label);
    }

    [Fact]
    public async Task FromJsonAsync_EmptyArray_ReturnsEmptyList()
    {
        using var stream = ToStream("[]");

        var records = await AlertRecordParser.FromJsonAsync(stream);

        Assert.Empty(records);
    }
}
