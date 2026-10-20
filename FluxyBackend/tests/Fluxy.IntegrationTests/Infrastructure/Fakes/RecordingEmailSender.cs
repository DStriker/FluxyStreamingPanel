using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Fluxy.Core.Email.Commands;
using Fluxy.Core.Email.Enums;
using Fluxy.Core.Email.Interfaces;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// Captures every message instead of sending it, and exposes the one-time code it carries.
    /// </summary>
    public sealed class RecordingEmailSender : IEmailSender
    {
        private static readonly Regex CodePattern = new(
            @"\b\d{6}\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly ConcurrentQueue<EmailMessage> _messages = new();

        public bool IsConfigured { get; init; } = true;

        public IReadOnlyList<EmailMessage> Messages => _messages.ToArray();

        public Task<EmailSendResult> SendAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(message);

            return Task.FromResult(IsConfigured
                ? EmailSendResult.Sent
                : EmailSendResult.NotConfigured);
        }

        public EmailMessage? LastFor(string email)
            => _messages.LastOrDefault(message =>
                string.Equals(message.To, email, StringComparison.OrdinalIgnoreCase));

        public string? CodeFor(string email)
        {
            var message = LastFor(email);

            if (message is null)
            {
                return null;
            }

            var match = CodePattern.Match(message.Body);

            return match.Success ? match.Value : null;
        }

        public void Clear()
        {
            while (_messages.TryDequeue(out _))
            {
            }
        }
    }
}
