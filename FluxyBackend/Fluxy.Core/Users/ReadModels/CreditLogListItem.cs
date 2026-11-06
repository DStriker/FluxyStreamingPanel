namespace Fluxy.Core.Users.ReadModels
{
    /// <summary>
    /// One credit log entry as a row of the admin table: the reseller it belongs to, the
    /// administrator who issued the credit when there is one, and the change itself.
    /// </summary>
    /// <remarks>
    /// The two usernames are resolved from <c>users</c> rather than stored on the log row, so a
    /// renamed account reads as its current name. <see cref="IssuedByUsername"/> is null on a
    /// reseller-spent row - that write path does not exist yet, and a spend has no issuer - and
    /// also when the issuing administrator's account has since been deleted, which the foreign key
    /// turns into a null identifier.
    /// </remarks>
    public sealed record CreditLogListItem
    {
        /// <summary>Identifier of the ledger entry.</summary>
        public required Guid Id { get; init; }

        /// <summary>Reseller whose balance this row moved.</summary>
        public required Guid ResellerId { get; init; }

        /// <summary>Login name of that reseller as it reads today.</summary>
        public required string ResellerUsername { get; init; }

        /// <summary>
        /// Administrator who wrote the row in the admin panel - create or update, either sign - or
        /// null on a reseller-spent row.
        /// </summary>
        public Guid? IssuedById { get; init; }

        /// <summary>Login name of that administrator, or null when there was no issuer.</summary>
        public string? IssuedByUsername { get; init; }

        /// <summary>Signed change to the balance.</summary>
        public required int Amount { get; init; }

        /// <summary>English sentence describing the change.</summary>
        public required string Description { get; init; }

        /// <summary>When the entry was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
