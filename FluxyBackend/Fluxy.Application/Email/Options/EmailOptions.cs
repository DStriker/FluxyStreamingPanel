using Fluxy.Core.Users.Models;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Fluxy.Application.Email.Options
{
    /// <summary>
    /// Settings of the SMTP server the installation mails through. Bound from the
    /// <c>Email</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Every property is optional on purpose. A machine that has no mail server is a valid state
    /// - the rest of the application still works - so binding an empty section must not throw.
    /// The absence is reported through <see cref="IEmailSender.IsConfigured"/> instead.
    ///
    /// This section is deliberately absent from <c>appsettings.json</c>. The credentials come from
    /// the gitignored <c>.env</c> file as <c>SMTP_HOST</c>, <c>SMTP_PORT</c>, <c>SMTP_USER</c>,
    /// <c>SMTP_PASSWORD</c>, <c>SMTP_USE_TLS</c> and <c>SMTP_FROM</c>, and a placeholder key in
    /// appsettings would shadow the real value, because appsettings has the higher priority. The
    /// defaults below are what applies when the file says nothing.
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
}
