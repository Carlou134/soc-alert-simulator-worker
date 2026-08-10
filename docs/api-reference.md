# API Reference

The Worker exposes a single HTTP endpoint. Everything else (batch sampling, sending to Splunk) runs on an internal timer and is not reachable over HTTP.

## `POST /api/v1/dataset/upload`

Uploads a dataset of alerts. Replaces whatever dataset was previously loaded — this is not additive.

**Content-Type:** `multipart/form-data`
**Form field:** `file` — a `.csv` or `.json` file

### Example request

```bash
curl -X POST "http://localhost:5000/api/v1/dataset/upload" \
  -F "file=@dataset.csv"
```

Testing with Postman: `POST` to the same URL, body type `form-data`, key `file` set to type `File` pointing at your dataset — no pre-built collection to keep in sync, this is the whole request.

### Success response — `200 OK`

```json
{ "loaded": 30 }
```

`loaded` is the number of alert records parsed and cached.

### Error responses

| Status | Body | When |
|---|---|---|
| `400 Bad Request` | `{ "error": "El archivo esta vacio." }` | Empty file |
| `400 Bad Request` | `{ "error": "Extension no soportada: .xlsx. Usa .csv o .json." }` | Unsupported extension |
| `500 Internal Server Error` | `{ "error": "Ocurrio un error inesperado procesando la solicitud.", "traceId": "..." }` | Any unhandled exception (e.g., a required column missing from the CSV header) — caught by `Middleware/GlobalExceptionHandler.cs`, full details are written to the log file, never to the response |

### JSON upload shape

A JSON upload is an array of objects using the same field names as the CSV header (snake_case), e.g.:

```json
[
  {
    "event_category": "Network Security",
    "protocol": "TCP",
    "traffic_type": "Inbound",
    "mitre_tactic": "Initial Access",
    "kill_chain_stage": "Delivery",
    "severity": "HIGH",
    "ids_ips_alert": "ET MALWARE Suspicious User-Agent",
    "asset_criticality": "CRITICAL",
    "log_source": "PaloAlto-Firewall",
    "firewall_action": "DENY",
    "failed_login_attempts": 12,
    "request_rate_per_min": 600,
    "attack_type": "Brute Force",
    "label": "SUSPICIOUS"
  }
]
```

## Alert schema (`AlertRecord`)

This is the exact vocabulary the Worker recognizes, both for parsing an upload and for the `event` body it sends to Splunk. It mirrors the Django backend's `Alert` model / `PredictionRequestSerializer` (`Api/soc-alert-prioritization-ml/soc_project/predictor/`), so a record produced here lines up with what the downstream ML pipeline expects if/when it starts consuming from Splunk.

Any column present in an uploaded file that is **not** in this table is silently ignored — it does not cause an error.

### Required

| Field | Type | Notes |
|---|---|---|
| `event_category` | string | e.g. `intrusion_attempt`, `credential_access` |
| `protocol` | string | e.g. `tcp`, `udp` |
| `traffic_type` | string | e.g. `ssh`, `https`, `dns` |
| `mitre_tactic` | string | MITRE ATT&CK tactic |
| `kill_chain_stage` | string | Kill chain stage |
| `severity` | string | e.g. `low`, `medium`, `high`, `critical` |
| `ids_ips_alert` | string | IDS/IPS verdict |
| `asset_criticality` | string | e.g. `low`, `medium`, `high` |
| `log_source` | string | e.g. `edr`, `siem`, `firewall` |
| `firewall_action` | string | e.g. `allowed`, `blocked` |
| `failed_login_attempts` | int | ≥ 0 |
| `request_rate_per_min` | double | ≥ 0, decimal (e.g. `35.45`) |

### Optional (default applied when missing)

| Field | Type | Default | Notes |
|---|---|---|---|
| `has_threat_family` | int (0/1) | `0` | 1 if a known malware family was matched |
| `evidence_role` | string | `"unknown"` | e.g. `attacker`, `impacted`, `related` |
| `os_family` | string | `"unknown"` | Affected asset's OS |
| `correlation_id` | string | `"unknown"` | Groups alerts belonging to the same incident |
| `mitre_techniques` | string | `""` | Semicolon-separated technique IDs, e.g. `"T1110;T1078.004"` — **not** one-hot columns |
| `anomaly_score` | double | `0.0` | 0.0–1.0; if omitted, the downstream ML pipeline calculates it |

### Display-only (stored, not used by the ML model downstream)

| Field | Type | Notes |
|---|---|---|
| `attack_type` | string | e.g. `Brute Force`, `Phishing` |
| `attack_signature` | string | Free-text signature/rule name |
| `malware_indicator` | string | Free-text indicator/hash/family — **not** boolean, despite the name |
| `label` | string | Ground-truth label, if known |

### A note on real-world datasets

A common real-world export includes 51 additional `mitre_t1078`, `mitre_t1110`, ... one-hot columns instead of a single `mitre_techniques` field (from an earlier ML training pipeline). These are **not** part of the schema above and are dropped on upload — `mitre_techniques` is marked optional specifically so a file like this one does not fail validation, it just uploads without technique-level detail.

## What the Worker sends to Splunk

Independent of the endpoint above, `BatchSenderWorker` wraps each sampled `AlertRecord` in a Splunk HEC event and POSTs a batch as newline-delimited JSON to `{Hec:BaseUrl}/services/collector`:

```json
{
  "time": 1723000000,
  "index": "soc_alerts",
  "sourcetype": "_json",
  "event": { "...": "an AlertRecord, see table above" }
}
```

with header `Authorization: Splunk <token>`.
