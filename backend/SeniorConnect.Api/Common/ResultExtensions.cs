using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SeniorConnect.Domain;

namespace SeniorConnect.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, string? location = null)
    {
        if (result.IsSuccess)
        {
            return location is not null
                ? Results.Created(location, result.Value)
                : Results.Ok(result.Value);
        }

        return MapErrorToProblemDetails(result.Error!);
    }

    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return MapErrorToProblemDetails(result.Error!);
    }

    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var guid) ? guid : null;
    }

    public static string? GetClientIp(this HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ip = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static IResult MapErrorToProblemDetails(Error error)
    {
        var statusCode = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Concurrency => StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status500InternalServerError
        };

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = error.Code
        };

        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                extensions[key] = value;
            }
        }

        return Results.Problem(
            statusCode: statusCode,
            title: error.Detail,
            type: $"https://SeniorConnect.at/errors/{error.Code.ToLowerInvariant().Replace('_', '-')}",
            detail: error.Detail,
            extensions: extensions);
    }
}
