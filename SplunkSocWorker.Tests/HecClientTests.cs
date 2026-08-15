using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using SplunkSocWorker.Models;
using SplunkSocWorker.Services;

namespace SplunkSocWorker.Tests;

public class HecClientTests
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static (HecClient client, FakeHttpMessageHandler handler) CreateClient(HttpStatusCode statusCode, string responseBody = "{\"text\":\"Success\",\"code\":0}")
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody)
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://splunk.local:8088/") };
        var client = new HecClient(httpClient, NullLogger<HecClient>.Instance);
        return (client, handler);
    }

    private static List<HecEvent> MakeEvents(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new HecEvent
        {
            Time = 1723000000,
            Index = "soc_alerts",
            Sourcetype = "_json",
            Event = new AlertRecord { CorrelationId = $"alert-{i}" }
        })];

    [Fact]
    public async Task SendBulkAsync_PostsToBulkCollectorEndpoint()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK);

        await client.SendBulkAsync(MakeEvents(1), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://splunk.local:8088/services/collector", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendBulkAsync_SendsOneNdjsonLinePerEvent()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK);

        await client.SendBulkAsync(MakeEvents(3), CancellationToken.None);

        var lines = handler.LastRequestBody!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, line => Assert.Contains("\"index\":\"soc_alerts\"", line));
    }

    [Fact]
    public async Task SendBulkAsync_OnSuccess_DoesNotThrow()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);

        await client.SendBulkAsync(MakeEvents(1), CancellationToken.None);
    }

    [Fact]
    public async Task SendBulkAsync_OnUnauthorized_ThrowsHttpRequestException()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized, "{\"text\":\"Invalid token\",\"code\":4}");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendBulkAsync(MakeEvents(1), CancellationToken.None));
    }
}
