# Technical Reference

## Requirements

- .NET 8 SDK
- A reachable Splunk Enterprise instance with the HTTP Event Collector (HEC) enabled (for the batch-sending path — the upload endpoint works without Splunk running)

![console](/docs/screnshots/worker-console.png)

## Local setup

```bash
cd SplunkSocWorker
dotnet restore
```

### Configuration

All configuration lives under `appsettings.json` / `appsettings.Development.json`, with the Splunk HEC token kept out of both.

```json
{
  "Hec": {
    "BaseUrl": "https://localhost:8088",
    "Token": "",
    "Index": "soc_alerts",
    "Sourcetype": "_json",
    "IgnoreSslErrors": false
  },
  "Batch": {
    "Enabled": false,
    "IntervalMinutes": 60,
    "MinSize": 15,
    "MaxSize": 30
  },
  "Dataset": {
    "StoragePath": "Data/alerts_pool.json"
  }
}
```

| Key | Meaning |
|---|---|
| `Hec:BaseUrl` | Splunk HEC base URL, e.g. `https://localhost:8088` |
| `Hec:Token` | HEC token, set via user-secrets — see below, never in a committed file |
| `Hec:Index` / `Hec:Sourcetype` | Sent as `index` / `sourcetype` on every HEC event |
| `Hec:IgnoreSslErrors` | Bypasses certificate validation, for Splunk's self-signed local dev cert |
| `Batch:Enabled` | **Defaults to `false`.** `BatchSenderWorker` starts with the host regardless, but sends nothing to Splunk unless this is `true` — starting/restarting the Worker (e.g. to test the upload endpoint) never fires alerts as a side effect. Set to `true` explicitly (or override per environment) when you actually want the simulation running. |
| `Batch:IntervalMinutes` | How often `BatchSenderWorker` ticks, when enabled |
| `Batch:MinSize` / `Batch:MaxSize` | Random batch size range sampled from the loaded dataset per tick |
| `Dataset:StoragePath` | Where the uploaded dataset is cached on disk |

`appsettings.Development.json` overrides `IgnoreSslErrors` to `true` (Splunk's local HEC certificate is self-signed) and `IntervalMinutes` to `5` (faster feedback loop for demos). `Batch:Enabled` defaults to `false` in both files on purpose — turn it on deliberately, per run, not as a standing default.

### Setting the HEC token

The token must never be written to `appsettings.json` or any other file tracked by git. The project already has a `UserSecretsId` configured in the `.csproj`, so:

```bash
dotnet user-secrets set "Hec:Token" "<your-hec-token>"
```

This stores it outside the repository (under the user profile), where `IConfiguration` picks it up automatically in Development.

In **Visual Studio 2022**, the equivalent is right-clicking the `SplunkSocWorker` project → **Manage User Secrets**, which opens the same underlying `secrets.json` in an editor:

```json
{
  "Hec": { "Token": "<your-hec-token>" }
}
```

### Running

**CLI / VS Code:**

```bash
dotnet run
```

**Visual Studio 2022:** open `SplunkSocWorker.sln`, then **F5** (with debugging) or **Ctrl+F5** (without). The `SplunkSocWorker` profile from `Properties/launchSettings.json` is picked automatically — there is no IIS Express option since Kestrel is the only host here.

Default URLs come from `Properties/launchSettings.json`: `https://localhost:7000` and `http://localhost:5000`.

## Dependencies and why they were chosen

| Package | Purpose | Why this one |
|---|---|---|
| `Microsoft.AspNetCore.App` (FrameworkReference) | Kestrel + Minimal APIs | Lets a `Sdk.Worker` project host an HTTP endpoint alongside its `BackgroundService`, without switching to `Sdk.Web` or running two processes |
| `CsvHelper` | CSV parsing | Attribute-based mapping (`[Name]`, `[Optional]`) keeps `AlertRecord` as the single source of truth for both CSV and JSON shapes, instead of hand-rolled `string.Split` parsing |
| `Serilog.AspNetCore` + `Serilog.Sinks.File` | Structured logging with file persistence | The default console-only logging provider does not survive a terminal restart; see `docs/architecture.md` for why file logging specifically (not Splunk, not the Django DB) |

## Folder structure

See `docs/architecture.md` for the responsibility of each folder. In short: `Models/` (DTOs), `Services/` (business logic), `BackgroundServices/` (the timer job), `Endpoints/` (HTTP surface), `Middleware/` (cross-cutting HTTP concerns).

## Logging

- Console: always, all environments.
- File: `Logs/worker-YYYYMMDD.log`, rolling daily, 14-day retention. Gitignored — this is runtime output, not source.
- Any unhandled exception in the HTTP pipeline is caught by `Middleware/GlobalExceptionHandler.cs`, logged with the request path and method, and returned to the caller as `{"error": "...", "traceId": "..."}` with a `500` status — never a raw stack trace.
- `BatchSenderWorker` catches exceptions per tick (e.g., Splunk unreachable) and logs them without stopping the timer loop.

## Known limitations

- No automated test project yet (no xUnit/NUnit suite). The CSV/JSON upload path has been exercised manually against a real 93-column dataset export — see the project's conversation history / commit log for that verification, not an automated regression test.
- `DatasetEndpoints` does not enforce a maximum upload file size (Django's equivalent endpoint caps at 10 MB; this Worker has no such cap today).
- `AlertsDatasetPool` holds a single dataset at a time — uploading a new file replaces the previous one entirely, there is no dataset history or versioning.
