using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace SeniorConnect.Api.Middleware;

/// <summary>
/// Replays the stored response for a repeated POST/PUT with the same
/// Idempotency-Key. Must run after authentication: the cache is keyed per
/// user, so one caller can never receive another caller's response, and
/// anonymous requests (login/OTP, whose responses carry tokens) are never cached.
/// </summary>
public class IdempotencyMiddleware
{
    public const string IdempotencyHeader = "Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    // ponytail: in-process cache, single instance only; move to a shared store when scaling out.
    private const int MaxEntries = 10_000;
    private readonly RequestDelegate _next;
    private static readonly ConcurrentDictionary<string, IdempotencyRecord> Cache = new();

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) && !HttpMethods.IsPut(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out StringValues headerValues) ||
            string.IsNullOrWhiteSpace(headerValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        var key = headerValues.First()!;
        var path = context.Request.Path.Value ?? string.Empty;
        var cacheKey = $"{userId}:{context.Request.Method}:{path}:{key}";

        if (Cache.TryGetValue(cacheKey, out var existingRecord))
        {
            if (DateTime.UtcNow - existingRecord.CreatedAt < Ttl)
            {
                context.Response.StatusCode = existingRecord.StatusCode;
                context.Response.ContentType = existingRecord.ContentType ?? "application/json";
                context.Response.Headers[IdempotencyHeader] = key;
                await context.Response.Body.WriteAsync(existingRecord.Body.AsMemory());
                return;
            }
            Cache.TryRemove(cacheKey, out _);
        }

        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Seek(0, SeekOrigin.Begin);
            var responseBodyBytes = memoryStream.ToArray();

            if (context.Response.StatusCode is >= 200 and < 300 or 409)
            {
                var record = new IdempotencyRecord(
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    responseBodyBytes,
                    DateTime.UtcNow);

                if (Cache.Count >= MaxEntries)
                {
                    PurgeExpired();
                }

                if (Cache.Count < MaxEntries)
                {
                    Cache[cacheKey] = record;
                }
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static void PurgeExpired()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var entry in Cache)
        {
            if (entry.Value.CreatedAt < cutoff)
            {
                Cache.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed record IdempotencyRecord(
        int StatusCode,
        string? ContentType,
        byte[] Body,
        DateTime CreatedAt);
}
