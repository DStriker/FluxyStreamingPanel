namespace Fluxy.API.Contracts.Users
{
    /// <summary>
    /// One row of the admin user audit log: who the event was about, which event it was,
    /// whether it worked, and where it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="AccountId"/> is the stored account, and it is nullable.</b> An attempt that
    /// named no existing account - a sign-in for a login name nobody holds - has no account to
    /// point at, and an account that was deleted turns the stored key into null (<c>ON DELETE SET
    /// NULL</c>) because the evidence outlives the account.
    /// </para>
    /// <para>
    /// <b><see cref="Username"/> is resolved in three steps, and the order is the contract.</b>
    /// When the row still names an account, it is that account's login name as it reads today -
    /// a renamed account therefore shows its current name. When it does not, the identifier the
    /// writer typed is recovered from <see cref="Description"/> through
    /// <c>UserLogDescriptions.TryGetSubject</c>, which is the exact inverse of the sentence
    /// builder. Only a row whose description carries no subject answers null. The typed
    /// identifier is what makes an attempt against an unknown account still readable as "someone
    /// tried to sign in as <c>ghost</c>"; it is the value as typed at write time, not a claim
    /// that the account exists.
    /// </para>
    /// <para>
    /// <see cref="RecordType"/> is the snake_case wire name
    /// (<c>registration_submit</c>, <c>login</c>, ...), the same vocabulary the filter accepts.
    /// </para>
    /// </remarks>
    public sealed record AdminUserLogItemResponse
    {
        /// <summary>Identifier of the log entry, as text.</summary>
        public required string Id { get; init; }

        /// <summary>
        /// Account the entry is about, as text, or null when the attempt named no existing
        /// account or that account has since been deleted.
        /// </summary>
        public string? AccountId { get; init; }

        /// <summary>
        /// Login name of that account as it reads today; otherwise the identifier recovered from
        /// the stored description; otherwise null.
        /// </summary>
        public string? Username { get; init; }

        /// <summary>Which kind of event the entry records, as its snake_case wire name.</summary>
        public required string RecordType { get; init; }

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

        /// <summary>When the entry was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
