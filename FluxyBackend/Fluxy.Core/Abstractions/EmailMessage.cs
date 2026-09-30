namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// A message to deliver to a single recipient. It is intentionally plain text and
    /// deliberately minimal: the registration code is the only content that matters today.
    /// </summary>
    public sealed record EmailMessage
    {
        /// <summary>Address the message is delivered to.</summary>
        public required string To { get; init; }

        /// <summary>Subject line of the message.</summary>
        public required string Subject { get; init; }

        /// <summary>Body of the message, in plain text.</summary>
        public required string Body { get; init; }
    }
}
