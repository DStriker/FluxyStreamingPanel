using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Commands
{
    /// <summary>
    /// One collective operation over a set of subscriptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifiers are the set a table assembled on screen. Duplicates have already collapsed
    /// and the ceiling has already been enforced by the transport, which is why the service
    /// treats this as a set rather than as a request that could still be malformed.
    /// </para>
    /// <para>
    /// Each action reads the one value it needs and ignores the rest: <see cref="TariffPlanId"/>
    /// for <see cref="BulkSubscriptionAction.ChangeTariff"/>,
    /// <see cref="ResellerId"/> for <see cref="BulkSubscriptionAction.ChangeCreator"/>. The
    /// remaining actions take nothing.
    /// </para>
    /// </remarks>
    public sealed record BulkSubscriptionOperation
    {
        /// <summary>Which operation to run.</summary>
        public required BulkSubscriptionAction Action { get; init; }

        /// <summary>The subscriptions to act on.</summary>
        public required IReadOnlyList<Guid> SubscriptionIds { get; init; }

        /// <summary>Destination plan of a <c>change-tariff</c>.</summary>
        public Guid? TariffPlanId { get; init; }

        /// <summary>Destination issuer of a <c>change-creator</c>.</summary>
        public Guid? ResellerId { get; init; }
    }
}
