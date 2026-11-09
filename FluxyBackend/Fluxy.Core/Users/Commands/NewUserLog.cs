using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Commands
{
    /// <summary>
    /// Values one user log row is staged with, before it has an identifier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The identifier and the audit timestamps are absent for the reason every other <c>New*</c>
    /// command omits them: the row generates the first and the context stamps the second.
    /// </para>
    /// <para>
    /// <b>The raw network values arrive here, not a resolved snapshot.</b> The service that stages
    /// the row is the one that owns the GeoIP lookup and the column truncation, so a caller states
    /// what the request carried - the address, the agent - and the service decides what a row can
    /// store. <see cref="Isp"/> and <see cref="CountryCode"/> are the two columns the service fills
    /// from that lookup; a caller leaves them null. The type is one shape because the repository
    /// stages one shape, and the record's <c>with</c> expression is what lets the service turn the
    /// caller's statement into the resolved row without a second command type.
    /// </para>
    /// <para>
    /// The description arrives already built, because its wording is a rule of the caller that knows
    /// the before and after values.
    /// </para>
    /// </remarks>
    public sealed record NewUserLog
    {
        /// <summary>
        /// Account the entry is about, or null when the attempt named no existing account.
        /// </summary>
        public Guid? UserId { get; init; }

        /// <summary>Which kind of event the entry records.</summary>
        public required UserLogRecordType RecordType { get; init; }

        /// <summary>Whether the attempt ended successfully.</summary>
        public required bool IsSuccess { get; init; }

        /// <summary>English sentence describing the event, already built.</summary>
        public required string Description { get; init; }

        /// <summary>Address the event came from, as the request reported it, or null.</summary>
        public string? Ip { get; init; }

        /// <summary>
        /// Provider the address belonged to. A caller leaves it null: it is the service's GeoIP
        /// snapshot, and it is carried here because the repository stages exactly this shape.
        /// </summary>
        public string? Isp { get; init; }

        /// <summary>
        /// Upper-case ISO 3166-1 alpha-2 country of the address. A caller leaves it null: it is the
        /// service's GeoIP snapshot, carried here for the same reason as <see cref="Isp"/>.
        /// </summary>
        public string? CountryCode { get; init; }

        /// <summary>User agent the client presented, as the request reported it, or null.</summary>
        public string? UserAgent { get; init; }
    }
}
