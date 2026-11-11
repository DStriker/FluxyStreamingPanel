namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// One event as a row of the admin MAG event table: when, which subscription, which device,
    /// which operation, and whether the device confirmed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MagMac"/> is resolved from the subscription when the page is built, never stored
    /// on the event row, so a corrected device address reads as it is now. It is null when the
    /// subscription has no MAG device.
    /// </para>
    /// <para>
    /// The identifiers travel as text, which is the form the wire contract names for this list.
    /// </para>
    /// </remarks>
    public sealed record AdminMagEventItemResponse
    {
        /// <summary>Identifier of the event, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the subscription it was sent under, as text.</summary>
        public required string SubscriptionId { get; init; }

        /// <summary>MAG address of that subscription, or null when it has none.</summary>
        public string? MagMac { get; init; }

        /// <summary>
        /// The operation, as one of <c>playChannel</c>, <c>closePortal</c>, <c>reloadPortal</c>,
        /// <c>rebootDevice</c>, <c>sendMessage</c>, <c>resetStbLock</c> - the spelling the filter
        /// accepts.
        /// </summary>
        public required string EventType { get; init; }

        /// <summary>Operation payload - a message text, a stream identifier, or null.</summary>
        public string? Message { get; init; }

        /// <summary>The confirmation state, as <c>confirmed</c> or <c>notConfirmed</c>.</summary>
        public required string Status { get; init; }

        /// <summary>When the event was recorded.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
