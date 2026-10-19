namespace Fluxy.Application.Services
{
    /// <summary>
    /// Folds an email address into the single form the database compares against.
    /// </summary>
    /// <remarks>
    /// Addresses are case insensitive in practice, and the column is not: a check that folded
    /// nothing would let a second free account be taken on a mailbox that already has one, by
    /// writing the same address with a different case. Trimming matters for the same reason -
    /// a trailing space from a paste would otherwise make a second account, or stage a change
    /// whose code is mailed to the first one's spelling.
    /// </remarks>
    public static class EmailNormalizer
    {
        /// <summary>Trims <paramref name="email"/> and folds it to lower case.</summary>
        public static string Normalize(string? email)
            => email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
