using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;

namespace SplunkSocWorker.Models;

// Espejo exacto del vocabulario de columnas del backend Django
// (Api/soc-alert-prioritization-ml/soc_project/predictor/{models,serializers,pipeline}.py).
public class AlertRecord
{
    // ── Campos requeridos por el modelo ML ────────────────────────────────
    [JsonPropertyName("event_category")]
    [Name("event_category")]
    public string EventCategory { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    [Name("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("traffic_type")]
    [Name("traffic_type")]
    public string TrafficType { get; set; } = string.Empty;

    [JsonPropertyName("mitre_tactic")]
    [Name("mitre_tactic")]
    public string MitreTactic { get; set; } = string.Empty;

    [JsonPropertyName("kill_chain_stage")]
    [Name("kill_chain_stage")]
    public string KillChainStage { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    [Name("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("ids_ips_alert")]
    [Name("ids_ips_alert")]
    public string IdsIpsAlert { get; set; } = string.Empty;

    [JsonPropertyName("asset_criticality")]
    [Name("asset_criticality")]
    public string AssetCriticality { get; set; } = string.Empty;

    [JsonPropertyName("log_source")]
    [Name("log_source")]
    public string LogSource { get; set; } = string.Empty;

    [JsonPropertyName("firewall_action")]
    [Name("firewall_action")]
    public string FirewallAction { get; set; } = string.Empty;

    [JsonPropertyName("failed_login_attempts")]
    [Name("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; }

    [JsonPropertyName("request_rate_per_min")]
    [Name("request_rate_per_min")]
    public double RequestRatePerMin { get; set; }

    // ── Campos opcionales que mejoran la prediccion (Django les pone default si faltan) ──
    [JsonPropertyName("has_threat_family")]
    [Name("has_threat_family")]
    public int HasThreatFamily { get; set; }

    [JsonPropertyName("evidence_role")]
    [Name("evidence_role")]
    public string EvidenceRole { get; set; } = "unknown";

    [JsonPropertyName("os_family")]
    [Name("os_family")]
    public string OsFamily { get; set; } = "unknown";

    [JsonPropertyName("correlation_id")]
    [Name("correlation_id")]
    public string CorrelationId { get; set; } = "unknown";

    // Tecnicas MITRE separadas por ";" (ej: "T1110;T1078.004"), no one-hot.
    // [Optional]: el dataset de prueba real trae 51 columnas mitre_t* one-hot en vez de esta,
    // asi que no siempre esta presente en el header - sin [Optional] CsvHelper tira HeaderValidationException.
    [JsonPropertyName("mitre_techniques")]
    [Name("mitre_techniques")]
    [Optional]
    public string MitreTechniques { get; set; } = string.Empty;

    // Si no se incluye, Django lo calcula automaticamente (pipeline.py).
    [JsonPropertyName("anomaly_score")]
    [Name("anomaly_score")]
    public double AnomalyScore { get; set; }

    // ── Campos de contexto — display/busqueda, no usados por el modelo ML ──
    [JsonPropertyName("attack_type")]
    [Name("attack_type")]
    public string AttackType { get; set; } = string.Empty;

    [JsonPropertyName("attack_signature")]
    [Name("attack_signature")]
    public string AttackSignature { get; set; } = string.Empty;

    [JsonPropertyName("malware_indicator")]
    [Name("malware_indicator")]
    public string MalwareIndicator { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    [Name("label")]
    public string Label { get; set; } = string.Empty;
}
