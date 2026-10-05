using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SeniorConnect.Api.Middleware;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Wave-3: a client-supplied correlation id is a join key for logs and a
/// reflected response header. It must never carry control characters (log
/// forging / response splitting), must be bounded, and must stay within a
/// visible-ASCII set. Anything unsafe is discarded and replaced server-side;
/// the raw header value is never logged and never echoed.
/// </summary>
public sealed class CorrelationIdValidationTests
{
    private static DefaultHttpContext ContextWith(string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = headerValue;
        }

        return context;
    }

    [Fact]
    public void A_missing_header_yields_no_client_value()
    {
        CorrelationIdMiddleware.ReadSanitized(new DefaultHttpContext()).Should().BeNull(
            "the middleware generates a fresh id when the client sends none");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_header_yields_no_client_value(string headerValue)
    {
        CorrelationIdMiddleware.ReadSanitized(ContextWith(headerValue)).Should().BeNull();
    }

    [Theory]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("opaque-trace_01:span.02")]
    [InlineData("a")]
    [InlineData("ABCxyz019-_.:")]
    public void A_safe_value_is_kept_verbatim(string headerValue)
    {
        CorrelationIdMiddleware.ReadSanitized(ContextWith(headerValue)).Should().Be(headerValue);
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_not_rejected()
    {
        CorrelationIdMiddleware.ReadSanitized(ContextWith("  abc-123  ")).Should().Be("abc-123");
    }

    [Theory]
    [InlineData("evil\r\nX-Injected: 1")]
    [InlineData("line1\nline2")]
    [InlineData("tab\there")]
    [InlineData("nul\0byte")]
    [InlineData("ansi\u001b[31mred")]
    [InlineData("del\u007fchar")]
    [InlineData("non-ascii-äöü")]
    [InlineData("quote\"break")]
    [InlineData("back\\slash")]
    [InlineData("space inside")]
    public void An_unsafe_value_is_discarded(string headerValue)
    {
        CorrelationIdMiddleware.ReadSanitized(ContextWith(headerValue)).Should().BeNull(
            "an unsafe value must never reach TraceIdentifier, a log line or a response header");
    }

    [Fact]
    public void An_overlong_value_is_discarded()
    {
        var overlong = new string('a', 65);

        CorrelationIdMiddleware.ReadSanitized(ContextWith(overlong)).Should().BeNull(
            "unbounded values are a log and header bloat vector");
    }

    [Fact]
    public void A_64_character_value_is_still_accepted()
    {
        var boundary = new string('a', 64);

        CorrelationIdMiddleware.ReadSanitized(ContextWith(boundary)).Should().Be(boundary);
    }
}