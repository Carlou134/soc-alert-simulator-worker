using System.Globalization;
using System.Text.Json;
using CsvHelper;
using SplunkSocWorker.Models;

namespace SplunkSocWorker.Services;

public static class AlertRecordParser
{
    public static List<AlertRecord> FromCsv(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<AlertRecord>().ToList();
    }

    public static async Task<List<AlertRecord>> FromJsonAsync(Stream stream)
    {
        var records = await JsonSerializer.DeserializeAsync<List<AlertRecord>>(stream);
        return records ?? [];
    }
}
