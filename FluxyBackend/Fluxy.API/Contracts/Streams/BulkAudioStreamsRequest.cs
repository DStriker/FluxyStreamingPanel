namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// What a bulk operation over audio streams carries: which operation, over which streams,
    /// and the one value that operation writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose, for the reason <c>BulkUsersRequest</c> gives: framework
    /// validation runs before the action, so a body that failed one would never reach the
    /// antiforgery check. Every field is parsed in the action, after the CSRF pair has been
    /// accepted and before a row is read.
    /// </para>
    /// <para>
    /// The action, identifiers, category and type arrive as text so their parsing happens where
    /// the refusal can name the field a client sent. The four restream fields keep their native
    /// types because there is no enum or identifier to parse - a blank string is judged as a
    /// rule of the operation itself.
    /// </para>
    /// </remarks>
    public sealed class BulkAudioStreamsRequest
    {
        /// <summary>
        /// The operation to run, as text: <c>start</c>, <c>stop</c>, <c>restart</c>,
        /// <c>delete</c>, <c>move-category</c>, <c>change-type</c> or <c>change-restream</c>.
        /// </summary>
        public string? Action { get; init; }

        /// <summary>
        /// The streams to act on, as uuids. At most one page of them - see
        /// <c>AudioStreamBulkLimits</c>.
        /// </summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>Destination of a <c>move-category</c>, as the uuid of a category.</summary>
        public string? CategoryId { get; init; }

        /// <summary>Destination of a <c>change-type</c>, as the name of the enum member.</summary>
        public string? Type { get; init; }

        /// <summary>User agent a <c>change-restream</c> writes; blank clears it.</summary>
        public string? RestreamUserAgent { get; init; }

        /// <summary>Proxy a <c>change-restream</c> writes; blank clears it.</summary>
        public string? RestreamProxy { get; init; }

        /// <summary>Cookies a <c>change-restream</c> writes; blank clears them.</summary>
        public string? RestreamCookies { get; init; }

        /// <summary>Extra headers a <c>change-restream</c> writes; blank clears them.</summary>
        public string? RestreamHeaders { get; init; }
    }
}
