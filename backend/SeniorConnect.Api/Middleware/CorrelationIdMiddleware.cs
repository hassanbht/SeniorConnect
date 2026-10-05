using Microsoft.AspNetCore.Http;

namespace SeniorConnect.Api.Middleware;

public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// A client-supplied correlation id is a join key for logs and a reflected
    /// response header. It must never carry control characters (log forging /
    /// response splitting), must be bounded in length (log and header bloat),
    /// and is restricted to a visible-ASCII set so it stays inert in every sink.
    /// Anything outside the set is discarded and replaced server-side; the raw
    /// header value is never logged and never echoed.
    /// </summary>
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadSanitized(context) ?? Guid.NewGuid().ToString("D");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>Returns the client value when it is safe, otherwise null.</summary>
    public static string? ReadSanitized(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValues))
        {
            return null;
        }

        var raw = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.Length == 0 || value.Length > MaxLength)
        {
            return null;
        }

        foreach (var c in value)
        {
            // Visible ASCII except the double quote and backslash: enough for
            // UUIDs and opaque ids, nothing that can break a log line, a JSON
            // string sink, or an HTTP header field.
            var safe = c is >= '!' and <= '~' and not ('"' or '\\');
            if (!safe)
            {
                return null;
            }
        }

        return value;
    }
}
