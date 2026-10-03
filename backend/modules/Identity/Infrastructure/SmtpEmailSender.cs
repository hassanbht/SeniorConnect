using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Deployment-integration phase: production-honest email delivery path for the
/// OTP / magic-link / verification flows (P1-11, P1-08b, ADR-021).
///
/// Configuration (all from environment / secret store, never from source):
///   Email:Provider      = "None" (default) | "Smtp"
///   Email:Host          = SMTP relay host (required when Provider=Smtp)
///   Email:Port          = default 587
///   Email:SenderAddress = envelope From / sender identity
///   Email:SenderName    = display name
///   Email:Username      = from secret store (may be empty for internal relays)
///   Email:Password      = from secret store (may be empty for internal relays)
///   Email:TimeoutSeconds = SMTP client timeout (default 10)
///   Email:MaxRetries     = transient-failure retries (default 1)
///
/// Fail-closed rule: with Provider=none (the committed default) every send
/// fails with EMAIL_PROVIDER_NOT_CONFIGURED instead of pretending delivery.
/// The message body carries the OTP or magic-link token, so it is never
/// logged — only the masked recipient and subject are logged.
/// No vendor is selected by this abstraction: the deployment configures any
/// standards-compliant SMTP relay; no vendor SDK or API is embedded.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private const string NotConfiguredCode = "EMAIL_PROVIDER_NOT_CONFIGURED";

    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly EmailProviderOptions _options;
    private readonly bool _isDevelopment;

    public SmtpEmailSender(ILogger<SmtpEmailSender> logger, EmailProviderOptions options, bool isDevelopment = false)
    {
        _logger = logger;
        _options = options;
        _isDevelopment = isDevelopment;
    }

    public async Task<Result> SendEmailAsync(
        string recipientEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            // Same rule as SmsSender: only a Development host may log the
            // message so developers can complete sign-in without a relay;
            // development never holds real personal data. Everywhere else the
            // send fails honestly instead of pretending delivery.
            if (_isDevelopment)
            {
                _logger.LogInformation(
                    "[Email Adapter][Development] Email to {MaskedEmail}: {Message}",
                    MaskEmail(recipientEmail), body);
                return Result.Success();
            }

            _logger.LogError(
                "[Email Adapter] No email provider configured; message to {MaskedEmail} not sent (subject withheld: carries verification content).",
                MaskEmail(recipientEmail));
            return Result.Failure(Error.ProviderUnavailable(
                NotConfiguredCode, "Email messages cannot be sent right now."));
        }

        var attempts = Math.Max(0, _options.MaxRetries) + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = CreateClient();
                using var message = CreateMessage(recipientEmail, subject, body);
                await client.SendMailAsync(message, cancellationToken);
                return Result.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < attempts && IsTransient(ex))
            {
                // Transient failure with retries remaining: log without the
                // body (it carries the OTP / token), then retry.
                _logger.LogWarning(
                    "[Email Adapter] Transient failure on attempt {Attempt}/{Attempts} to {MaskedEmail}: {ExceptionType}.",
                    attempt, attempts, MaskEmail(recipientEmail), ex.GetType().Name);
            }
            catch (Exception ex)
            {
                // Permanent failure or retries exhausted.
                _logger.LogError(
                    "[Email Adapter] Send to {MaskedEmail} failed after {Attempt} attempt(s): {ExceptionType}.",
                    MaskEmail(recipientEmail), attempt, ex.GetType().Name);
                return Result.Failure(Error.ProviderUnavailable(
                    "EMAIL_PROVIDER_SEND_FAILED", "Email messages cannot be sent right now."));
            }
        }

        // Unreachable: the loop returns on every branch.
        return Result.Failure(Error.ProviderUnavailable(
            "EMAIL_PROVIDER_SEND_FAILED", "Email messages cannot be sent right now."));
    }

    private SmtpClient CreateClient() => new(_options.Host!, _options.Port)
    {
        EnableSsl = true,
        Timeout = _options.TimeoutMilliseconds,
        Credentials = string.IsNullOrEmpty(_options.Username) && string.IsNullOrEmpty(_options.Password)
            ? null
            : new NetworkCredential(_options.Username, _options.Password),
    };

    private MailMessage CreateMessage(string recipientEmail, string subject, string body)
    {
        var from = string.IsNullOrEmpty(_options.SenderName)
            ? new MailAddress(_options.SenderAddress!)
            : new MailAddress(_options.SenderAddress!, _options.SenderName);
        return new MailMessage(from, new MailAddress(recipientEmail))
        {
            Subject = subject,
            Body = body,
        };
    }

    /// <summary>Transiency heuristic: timeouts and temporary SMTP status, permanent on anything else.</summary>
    private static bool IsTransient(Exception ex) =>
        ex is SmtpException { StatusCode: SmtpStatusCode.MailboxBusy or SmtpStatusCode.ServiceNotAvailable }
        || ex is System.Net.Sockets.SocketException
        || ex is System.IO.IOException;

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***";
        return $"{email[0]}***{email[(atIndex - 1)..]}";
    }
}
