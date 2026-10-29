using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Core.Subscriptions.Outcomes
{
    /// <summary>
    /// How one row inside a bulk run over subscriptions ended.
    /// </summary>
    /// <remarks>
    /// <see cref="Warnings"/> is separate from <see cref="Errors"/> because a warning belongs to
    /// a row that <b>was</b> written: a plan change that clears a service the destination does
    /// not support succeeded and lost a setting, which is not the same statement as a refusal
    /// and must not be read as one.
    /// </remarks>
    public sealed record BulkSubscriptionItemResult
    {
        /// <summary>The subscription this entry is about.</summary>
        public required Guid SubscriptionId { get; init; }

        /// <summary>How that row ended.</summary>
        public required SubscriptionAction Action { get; init; }

        /// <summary>Rejected fields, when the input was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

        /// <summary>Settings the operation cleared, or null when nothing was cleared.</summary>
        public IReadOnlyList<SubscriptionBulkWarning>? Warnings { get; init; }
    }
}
