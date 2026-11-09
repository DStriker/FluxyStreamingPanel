using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Models
{
    /// <summary>
    /// Immutable business representation of one entry of a user's audit log: what happened, to
    /// which account when there is one, from where, and whether it worked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="IsSuccess"/> is a stored fact, not a property of the record type.</b> The
    /// same kind of event ends either way - a sign-in that was refused is still a sign-in - and
    /// deriving the flag from <see cref="RecordType"/> would make one attempt unrepresentable.
    /// </para>
    /// <para>
    /// <b>The account is a nullable foreign key and never cascades.</b> A refused sign-in against
    /// an address that names no account has no account to point at, and deleting an account must
    /// not erase the history of what was attempted against it. The identifier is therefore nulled
    /// by the database (<c>ON DELETE SET NULL</c>) and <see cref="Description"/> already carries
    /// the identifier as it read at write time, so the row stays legible after the account is gone.
    /// </para>
    /// <para>
    /// <b>The network facts are a snapshot taken at write time.</b> Country and provider are
    /// captured from the GeoIP lookup that one request made, so a filter on them describes the
    /// network as it was when the event happened, not as the database would resolve it today.
    /// </para>
    /// </remarks>
    public sealed record UserLog
    {
        /// <summary>Identifier of the log entry.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Account the entry is about, or null when the attempt named no existing account and
        /// when the account was deleted afterwards.
        /// </summary>
        public Guid? UserId { get; init; }

        /// <summary>Which kind of event the entry records.</summary>
        public required UserLogRecordType RecordType { get; init; }

        /// <summary>Whether the attempt ended successfully.</summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// English sentence describing the event, built by
        /// <see cref="Utils.UserLogDescriptions"/>. Never carries a password, a code or a token.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>Address the event came from, up to 45 characters, or null when unknown.</summary>
        public string? Ip { get; init; }

        /// <summary>Provider the address belonged to, up to 255 characters, or null.</summary>
        public string? Isp { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>User agent the client presented, up to 512 characters, or null.</summary>
        public string? UserAgent { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
