using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.ReadModels
{
    /// <summary>
    /// One MAG event as a row of the admin table: when, which subscription, which device, which
    /// operation, and whether it was confirmed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The MAG address is resolved, not stored.</b> <see cref="MagMac"/> is read from the
    /// subscription the event was sent under, so a device whose MAC was corrected reads as the
    /// current value rather than as a snapshot. It is null when the subscription has no MAG
    /// device - though a send refuses that case, a row planted earlier may still carry one.
    /// </para>
    /// <para>
    /// The identifiers travel as <see cref="Guid"/> here; the transport renders them as text on the
    /// wire, which is the form the contract names.
    /// </para>
    /// </remarks>
    public sealed record MagEventListItem
    {
        /// <summary>Identifier of the event.</summary>
        public required Guid Id { get; init; }

        /// <summary>Identifier of the subscription it was sent under.</summary>
        public required Guid SubscriptionId { get; init; }

        /// <summary>MAG address of that subscription, or null when it has none.</summary>
        public string? MagMac { get; init; }

        /// <summary>Which operation it asked for.</summary>
        public required MagEventType EventType { get; init; }

        /// <summary>Operation payload - a message text, a stream identifier, or null.</summary>
        public string? Message { get; init; }

        /// <summary>Whether the device has confirmed the operation.</summary>
        public required MagEventStatus Status { get; init; }

        /// <summary>When the event was recorded.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
