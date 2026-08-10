using SplunkSocWorker.Models;

namespace SplunkSocWorker.Services;

public interface IHecClient
{
    Task SendBulkAsync(IEnumerable<HecEvent> events, CancellationToken ct);
}
