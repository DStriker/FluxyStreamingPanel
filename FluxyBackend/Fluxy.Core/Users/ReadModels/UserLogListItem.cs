using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.ReadModels
{
    /// <summary>
    /// One user log entry as a row of the admin table: the account it is about when there is
    /// one, the event, its outcome, and the network it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The login name is resolved from <c>users</c> rather than stored on the log row</b>, so a
    /// renamed account reads as its current name. It is null when the row has no account - an
    /// attempt against an identifier nobody held - and when that account has since been deleted,
    /// which the foreign key turns into a null identifier.
    /// </para>
    /// <para>
    /// <see cref="Description"/> is the stored sentence and stays legible on its own, which is
    /// what makes a row about a deleted account still worth reading.
    /// </para>
    /// </remarks>
    public sealed record UserLogListItem
    {
        /// <summary>Identifier of the log entry.</summary>
        public required Guid Id { get; init; }

        /// <summary>Account the entry is about, or null when there was none or it was deleted.</summary>
        public Guid? UserId { get; init; }

        /// <summary>Login name of that account as it reads today, or null.</summary>
        public string? Username { get; init; }

        /// <summary>Which kind of event the entry records.</summary>
        public required UserLogRecordType RecordType { get; init; }

        /// <summary>Whether the attempt ended successfully.</summary>
        public required bool IsSuccess { get; init; }

        /// <summary>English sentence describing the event.</summary>
        public required string Description { get; init; }

        /// <summary>Address the event came from, or null.</summary>
        public string? Ip { get; init; }

        /// <summary>Provider the address belonged to, or null.</summary>
        public string? Isp { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>User agent the client presented, or null.</summary>
        public string? UserAgent { get; init; }

        /// <summary>When the entry was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
