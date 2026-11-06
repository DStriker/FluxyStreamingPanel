namespace Fluxy.API.Contracts.Users
{
    /// <summary>
    /// One row of the admin credit log: the reseller whose balance moved, the administrator who
    /// issued the credit when there was one, and the change itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The amount is a signed delta.</b> Positive is credit issued, negative is credit spent,
    /// and zero is the entry an account created with no initial credit carries. The row's
    /// <see cref="Description"/> is the sentence the write path generated, kept in English because
    /// it is the auditable record rather than a rendering of the reader's language.
    /// </para>
    /// <para>
    /// <see cref="IssuedById"/> and <see cref="IssuedByUsername"/> are null on a reseller-spent
    /// row - that write path does not exist yet, and a spend has no issuer - and also when the
    /// issuing administrator's account has since been deleted, which the foreign key turns into a
    /// null identifier.
    /// </para>
    /// </remarks>
    public sealed record AdminCreditLogItemResponse
    {
        /// <summary>Identifier of the ledger entry, as text.</summary>
        public required string Id { get; init; }

        /// <summary>Identifier of the reseller whose balance moved, as text.</summary>
        public required string ResellerId { get; init; }

        /// <summary>Login name of that reseller as it reads today.</summary>
        public required string ResellerUsername { get; init; }

        /// <summary>
        /// Administrator who wrote the row in the admin panel - create or update, either sign - or
        /// null on a reseller-spent row.
        /// </summary>
        public string? IssuedById { get; init; }

        /// <summary>Login name of that administrator, or null when there was no issuer.</summary>
        public string? IssuedByUsername { get; init; }

        /// <summary>Signed change to the balance: positive issued, negative spent.</summary>
        public required int Amount { get; init; }

        /// <summary>English sentence describing the change.</summary>
        public required string Description { get; init; }

        /// <summary>When the entry was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
