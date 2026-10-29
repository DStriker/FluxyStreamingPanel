namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What a bulk operation over subscriptions carries: which operation, over which rows, and
    /// the one value it writes.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose, for the reason <c>BulkVideoStreamsRequest</c> gives:
    /// framework validation runs before the action, so a body that failed one would never reach
    /// the antiforgery check. The action and both identifiers arrive as text so their parsing
    /// happens where the refusal can name the field a client sent.
    /// </remarks>
    public sealed class BulkSubscriptionsRequest
    {
        /// <summary>
        /// The operation to run, as text: <c>change-tariff</c>, <c>change-creator</c>,
        /// <c>delete</c>, <c>delete-playlist</c>, <c>delete-mag</c>, <c>delete-enigma2</c> or
        /// <c>block-owner</c>.
        /// </summary>
        public string? Action { get; init; }

        /// <summary>
        /// The subscriptions to act on, as uuids. At most one page of them - see
        /// <c>SubscriptionBulkLimits</c>.
        /// </summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>Destination plan of a <c>change-tariff</c>, as the uuid of a plan.</summary>
        public string? TariffPlanId { get; init; }

        /// <summary>Destination issuer of a <c>change-creator</c>, as the uuid of an account.</summary>
        public string? ResellerId { get; init; }
    }
}
