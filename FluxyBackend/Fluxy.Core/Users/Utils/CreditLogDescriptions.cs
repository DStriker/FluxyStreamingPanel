using System.Globalization;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The English sentences a credit log entry carries, built in one place, because the wording
    /// is what the table draws and what the search box matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>English, not the reader's language.</b> The description is stored once and read by
    /// every screen; a row written in one operator's language would read as noise to the next.
    /// The frontend renders its own translations from the amount and the reseller where it
    /// wants them, and treats this sentence as the auditable record.
    /// </para>
    /// <para>
    /// <b>One factory per shape of change.</b> Creation has a balance, not a before and after -
    /// there was no account to change - so it reads as the account being created with its
    /// initial credit. Every later change names both sides, which is what makes the delta
    /// legible without arithmetic.
    /// </para>
    /// </remarks>
    public static class CreditLogDescriptions
    {
        /// <summary>
        /// The sentence of the one row a reseller is created with: the balance it started at.
        /// </summary>
        /// <param name="credits">Initial balance, which may be zero.</param>
        public static string ResellerCreated(int credits)
            => string.Create(
                CultureInfo.InvariantCulture,
                $"Reseller created with initial credits: {credits}");

        /// <summary>
        /// The sentence of a change to an existing balance: the value before and the value after,
        /// separated by a rightwards arrow (U+2192).
        /// </summary>
        /// <param name="before">Balance before the change.</param>
        /// <param name="after">Balance after the change.</param>
        public static string CreditsChanged(int before, int after)
            => string.Create(
                CultureInfo.InvariantCulture,
                $"Credits changed: {before} \u2192 {after}");
    }
}
