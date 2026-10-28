using System.Net.Mime;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Writes the health report as JSON naming each check, so "Unhealthy" says what is unhealthy.
/// The status code (200, or 503 when unhealthy) is set by the health check middleware.
/// </summary>
public static class HealthReportWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
    }
}
