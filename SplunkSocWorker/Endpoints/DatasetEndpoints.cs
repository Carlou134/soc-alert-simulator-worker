using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SplunkSocWorker.Models;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.Endpoints;

public static class DatasetEndpoints
{
    public static void MapDatasetEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/dataset/upload", async (
            IFormFile file,
            AlertsDatasetPool pool,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (file.Length == 0)
                return Results.BadRequest(new { error = "El archivo esta vacio." });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (extension is not (".csv" or ".json"))
                return Results.BadRequest(new { error = $"Extension no soportada: {extension}. Usa .csv o .json." });

            List<AlertRecord> records;
            await using (var stream = file.OpenReadStream())
            {
                records = extension == ".csv"
                    ? AlertRecordParser.FromCsv(stream)
                    : await AlertRecordParser.FromJsonAsync(stream);
            }

            await pool.ReplaceAllAsync(records, ct);
            logger.LogInformation("Dataset cargado: {Count} alertas desde {FileName}.", records.Count, file.FileName);

            return Results.Ok(new { loaded = records.Count });
        })
        .WithName("UploadDataset")
        .DisableAntiforgery();
    }
}
