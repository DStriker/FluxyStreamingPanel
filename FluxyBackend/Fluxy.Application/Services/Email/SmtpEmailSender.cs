using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Fluxy.Core.Abstractions;

namespace Fluxy.Application.Services.Email
{
    /// <summary>
    /// Settings of the SMTP server the installation mails through. Bound from the
    /// <c>Email</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Every property is optional on purpose. A machine that has no mail server is a valid state
    /// - the rest of the application still works - so binding an empty section must not throw.
    /// The absence is reported through <see cref="IEmailSender.IsConfigured"/> instead.
    /// </remarks>
    public sealed class EmailOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "Email";

        /// <summary>Host name of the SMTP server. Absent means mailing is not configured.</summary>
        public string? Host { get; set; }

        /// <summary>Port of the SMTP server. Submission on 465 is implicit TLS, otherwise it is plaintext.</summary>
        public int Port { get; set; } = 587;

        /// <summary>User name for SMTP authentication.</summary>
        public string? User { get; set; }

        /// <summary>Password for SMTP authentication.</summary>
        public string? Password { get; set; }

        /// <summary>Whether to start TLS on the connection. A local relay needs neither this nor 465.</summary>
        public bool UseTls { get; set; } = true;

        /// <summary>Address the installation sends from. Any confirmation a mail server asks for goes here.</summary>
        public string? From { get; set; }

        /// <summary>Display name shown next to <see cref="From"/>.</summary>
        public string? FromName { get; set; }
    }

    /// <summary>
    /// Delivers mail through an SMTP server with MailKit.
    /// </summary>
    /// <remarks>
    /// The class never throws. Mail is the one part of a registration that can be impossible for
    /// reasons outside the request - no server configured, server down, credentials refused - and
    /// an exception there would turn a configuration problem into a 500. Every failure is logged
    /// and returned as an <see cref="EmailSendResult"/>.
    /// </remarks>
    public sealed class SmtpEmailSender : IEmailSender
    {
        /// <summary>Port on which TLS is negotiated before the SMTP greeting rather than after it.</summary>
        private const int ImplicitTlsPort = 465;

        private readonly IOptionsMonitor<EmailOptions> _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmtpEmailSender"/> class.
        /// </summary>
        /// <param name="options">Live settings, so a configuration reload applies without a restart.</param>
        /// <param name="logger">Logger delivery problems are reported to.</param>
        public SmtpEmailSender(
            IOptionsMonitor<EmailOptions> options,
            ILogger<SmtpEmailSender> logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public bool IsConfigured => TryResolve(out _);

        /// <inheritdoc />
        public async Task<EmailSendResult> SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            if (!TryResolve(out var settings))
            {
                _logger.LogWarning(
                    "Mail is not configured, so a message for {Recipient} was not sent. Missing: {Missing}.",
                    message.To,
                    string.Join(", ", DescribeMissingSettings(_options.CurrentValue)));

                return EmailSendResult.NotConfigured;
            }

            try
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    settings.Host,
                    settings.Port,
                    settings.UseTls,
                    cancellationToken);

                // Tested for being filled in rather than merely present. A shipped
                // appsettings.json carries "User": "", which binds to an empty string and is
                // not null, so testing for null authenticates as a user with no name - and a
                // relay that requires no authentication refuses the connection over it, turning
                // a working configuration into a delivery failure.
                if (settings.Credentials is { } credentials)
                {
                    await client.AuthenticateAsync(
                        credentials.User,
                        credentials.Password,
                        cancellationToken);
                }

                var mail = new MimeMessage
                {
                    Subject = message.Subject,
                    Body = new TextPart("plain") { Text = message.Body }
                };

                mail.From.Add(new MailboxAddress(settings.FromName, settings.From));
                mail.To.Add(MailboxAddress.Parse(message.To));

                await client.SendAsync(mail, cancellationToken);
                await client.DisconnectAsync(quit: true, cancellationToken);

                _logger.LogInformation("Sent a message to {Recipient}.", message.To);

                return EmailSendResult.Sent;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away. Not a delivery failure, and not worth a log entry.
                throw;
            }
            catch (Exception exception)
            {
                // Whatever the mail server or the network does lands here. The row behind this
                // message is already stored, so the failure is reported to the caller and kept
                // out of the response body: the address of an internal host is not something a
                // client needs, and the detail is already in the log.
                _logger.LogError(
                    exception,
                    "Could not send a message to {Recipient} through {Host}:{Port}. The account is " +
                    "stored and its code is pending, so a new registration request has to replace it.",
                    message.To,
                    settings.Host,
                    settings.Port);

                return EmailSendResult.TransportFailed;
            }
        }

        /// <summary>
        /// Checks the current settings and hands back a version with the required values already
        /// known to be present, so the mailer never has to reason about a missing host.
        /// </summary>
        private bool TryResolve(out ResolvedSettings settings)
        {
            var current = _options.CurrentValue;

            if (DescribeMissingSettings(current).Length > 0)
            {
                settings = null!;
                return false;
            }

            settings = new ResolvedSettings(
                Host: current.Host!,
                Port: current.Port,
                // Port 465 negotiates TLS before the greeting; every other port that wants it
                // starts it with STARTTLS afterwards. A relay that wants neither sets UseTls false.
                UseTls: current.UseTls && current.Port != ImplicitTlsPort,
                Credentials: IsSet(current.User) && IsSet(current.Password)
                    ? new Credentials(current.User!, current.Password!)
                    : null,
                From: current.From!,
                FromName: current.FromName);

            return true;
        }

        /// <summary>
        /// Names of the settings that keep the installation from mailing, or an empty array when
        /// it can.
        /// </summary>
        /// <remarks>
        /// A half filled credential pair counts as missing. A user name without a password
        /// authenticates as nobody, and a password without a user name is never read, so both
        /// mean the section was only partly filled in - and the first of them fails against a
        /// server that requires authentication, with an error that points at the wrong setting.
        /// </remarks>
        private static string[] DescribeMissingSettings(EmailOptions options)
        {
            var missing = new List<string>();

            if (!IsSet(options.Host))
            {
                missing.Add($"{EmailOptions.SectionName}:Host");
            }

            if (!IsSet(options.From))
            {
                missing.Add($"{EmailOptions.SectionName}:From");
            }

            if (IsSet(options.User) != IsSet(options.Password))
            {
                missing.Add($"{EmailOptions.SectionName}:User and {EmailOptions.SectionName}:Password together");
            }

            return [.. missing];
        }

        private static bool IsSet(string? value)
            => !string.IsNullOrWhiteSpace(value);

        /// <summary>
        /// The settings a delivery actually needs, with the required values already checked.
        /// Keeping them in one type is what lets <see cref="SendAsync"/> read the host without a
        /// null check that the compiler cannot verify.
        /// </summary>
        private sealed record ResolvedSettings(
            string Host,
            int Port,
            bool UseTls,
            Credentials? Credentials,
            string From,
            string? FromName);

        /// <summary>
        /// The pair the mail server authenticates with, or nothing at all when it authenticates
        /// nobody. Carrying the two together is what makes it impossible to authenticate with a
        /// user name and no password.
        /// </summary>
        private sealed record Credentials(string User, string Password);
    }
}
