namespace Fluxy.Core.Users.Commands
{
    /// <summary>
    /// Values one credit log row is staged with, before it has an identifier.
    /// </summary>
    /// <remarks>
    /// The identifier and the audit timestamps are absent for the reason every other
    /// <c>New*</c> command omits them: the row generates the first and the context stamps the
    /// second. The description arrives already built, because the wording is a rule of the
    /// service that knows the before and after values.
    /// </remarks>
    public sealed record NewCreditLog
    {
        /// <summary>Reseller whose balance this row moves.</summary>
        public required Guid ResellerId { get; init; }

        /// <summary>
        /// Administrator who wrote this row in the admin panel, for a create or an update of
        /// either sign; null on a reseller-spent row and after the issuing administrator is
        /// deleted.
        /// </summary>
        public Guid? IssuedById { get; init; }

        /// <summary>Signed change to the balance.</summary>
        public required int Amount { get; init; }

        /// <summary>English sentence describing the change.</summary>
        public required string Description { get; init; }
    }
}
