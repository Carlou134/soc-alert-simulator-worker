using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace SplunkSocWorker.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Excepcion no controlada en {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = "Ocurrio un error inesperado procesando la solicitud.",
            traceId = httpContext.TraceIdentifier
        }, cancellationToken);

        return true;
    }
}
