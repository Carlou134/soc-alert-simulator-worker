using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using SplunkSocWorker.BackgroundServices;
using SplunkSocWorker.Endpoints;
using SplunkSocWorker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AlertsDatasetPool>();
builder.Services.AddHostedService<BatchSenderWorker>();

builder.Services.AddHttpClient<IHecClient, HecClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Hec:BaseUrl"] ?? "https://localhost:8088";
    var token = configuration["Hec:Token"];

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Splunk", token);
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var handler = new HttpClientHandler();

    if (configuration.GetValue("Hec:IgnoreSslErrors", false))
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

    return handler;
});

var app = builder.Build();

app.MapDatasetEndpoints();

app.Run();
