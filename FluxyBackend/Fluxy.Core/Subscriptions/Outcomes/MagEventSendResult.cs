namespace Fluxy.Core.Subscriptions.Outcomes
{
    /// <summary>
    /// The outcome of one MAG event send: the row it stored, or the fields that made the request
    /// unacceptable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Errors"/> is keyed by the C# name of the field the service rejected, the
    /// convention every other service here follows; the transport turns those into the camelCase
    /// names a form reads.
    /// </para>
    /// <para>
    /// <b><see cref="MagDeviceMissing"/> is separate from <see cref="Errors"/> on purpose.</b> A
    /// subscription with no MAG device is a fact about the row rather than a malformed field, and
    /// the contract answers it with its own code (<c>mag_device_missing</c>) instead of a
    /// validation map.
    /// </para>
    /// </remarks>
    public sealed record MagEventSendResult
    {
        /// <summary>Identifier of the stored row, or null when the send was refused.</summary>
        public Guid? EventId { get; init; }

        /// <summary>True when the subscription carries no MAG device and nothing was written.</summary>
        public bool MagDeviceMissing { get; init; }

        /// <summary>Rejected fields, when the request was the problem.</summary>
        public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    }
}
