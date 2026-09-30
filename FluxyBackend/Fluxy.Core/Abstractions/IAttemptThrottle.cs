namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Counts how often a caller has tried something, so an expensive or abusable operation
    /// can be refused before it does any work.
    /// </summary>
    /// <remarks>
    /// The two calls are deliberately separate. An operation whose work is only paid for on
    /// success - a registration that is stored, a confirmation that verifies a code - peeks
    /// first and records afterwards, so a rejected or mistyped request costs nothing. An
    /// operation that must count failures as well, such as a code check that burns a
    /// deliberately expensive comparison, records up front in a single call.
    /// </remarks>
    public interface IAttemptThrottle
    {
        /// <summary>
        /// Number of attempts already recorded for <paramref name="key"/> inside the current
        /// window, without changing anything. Callers compare it against
        /// <see cref="AttemptPolicy.Limit"/> to decide whether to refuse the request.
        /// </summary>
        /// <param name="key">
        /// Identifier of the counted party, normally the operation name and the client address.
        /// Callers must build it so that one client cannot influence another one.
        /// </param>
        /// <param name="policy">Limit and window to reason with.</param>
        /// <returns>The current count, or zero when nothing has been recorded yet.</returns>
        int GetAttempts(string key, AttemptPolicy policy);

        /// <summary>
        /// Records one more attempt for <paramref name="key"/> and returns the resulting count.
        /// The first attempt of a window starts it; later ones only increment.
        /// </summary>
        /// <param name="key">Identifier of the counted party.</param>
        /// <param name="policy">Limit and window the count is kept in.</param>
        /// <returns>The count including the attempt just recorded.</returns>
        int RecordAttempt(string key, AttemptPolicy policy);
    }
}
