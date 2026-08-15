using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Serilog;
using SplunkSocWorker.BackgroundServices;
using SplunkSocWorker.Endpoints;
using SplunkSocWorker.Middleware;
using SplunkSocWorker.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/worker-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

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

    app.UseExceptionHandler();
    app.MapDatasetEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El Worker termino de forma inesperada durante el arranque.");
}
finally
{
    Log.CloseAndFlush();
}

// Expone el tipo Program para que SplunkSocWorker.Tests pueda usar WebApplicationFactory<Program>
// en los tests de integracion — necesario porque este archivo usa top-level statements.
public partial class Program { }
