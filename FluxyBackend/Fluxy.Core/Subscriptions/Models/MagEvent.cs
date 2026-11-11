using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Models
{
    /// <summary>
    /// Immutable business representation of one event an operator sent to a MAG device: which
    /// subscription it was sent under, which operation it asked for, and whether the device has
    /// confirmed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The subscription is a real foreign key.</b> An event without the subscription it was
    /// sent under is a row nobody can be told about, so deleting the subscription removes its
    /// events through <c>ON DELETE CASCADE</c>.
    /// </para>
    /// <para>
    /// <b>The message carries whatever the operation needed.</b> For <see cref="MagEventType.SendMessage"/>
    /// it is the text the device shows; for <see cref="MagEventType.PlayChannel"/> it is the
    /// stream identifier; every other kind stores null. Those rules are the application's
    /// (<c>MagEventRules</c>), not this record's.
    /// </para>
    /// </remarks>
    public sealed record MagEvent
    {
        /// <summary>Identifier of the event.</summary>
        public required Guid Id { get; init; }

        /// <summary>The subscription the event was sent under.</summary>
        public required Guid SubscriptionId { get; init; }

        /// <summary>Which operation it asked the device to perform.</summary>
        public required MagEventType EventType { get; init; }

        /// <summary>Operation payload - a message text, a stream identifier, or null.</summary>
        public string? Message { get; init; }

        /// <summary>Whether the device has confirmed the operation.</summary>
        public required MagEventStatus Status { get; init; }

        /// <summary>Whether the device must confirm the operation.</summary>
        public required bool NeedConfirm { get; init; }

        /// <summary>Whether the device should reboot after a successful operation.</summary>
        public required bool RebootAfterOk { get; init; }

        /// <summary>Whether the message hides itself after a timeout.</summary>
        public required bool AutoHideTimeout { get; init; }

        /// <summary>Whether other services are switched on with the operation.</summary>
        public required bool AdditionalServicesOn { get; init; }

        /// <summary>ANEC flag the device is sent with the operation.</summary>
        public required bool Anec { get; init; }

        /// <summary>VCLUB flag the device is sent with the operation.</summary>
        public required bool Vclub { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
