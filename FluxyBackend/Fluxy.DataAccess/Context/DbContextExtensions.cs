using Fluxy.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fluxy.DataAccess.Context
{
    /// <summary>
    /// Saving extensions for <see cref="FluxyDbContext"/>: the one entry point through which
    /// every write in this solution goes.
    /// </summary>
    public static class DbContextExtensions
    {
        /// <summary>
        /// Saves every staged change, turning the one database refusal the application
        /// interprets - a unique index saying a value is taken - into
        /// <see cref="UniqueViolationException"/>, which carries the index's name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every write in this solution goes through this method rather than through
        /// <c>SaveChangesAsync</c>, so no caller has to read the provider's message text to
        /// find out what was refused - SQLSTATE <c>23505</c> used to be searched for in a
        /// string, in eight copies. A failure that is not a unique violation is left exactly as
        /// the provider threw it: a constraint this service does not know about is a bug, and
        /// dressing it up as a familiar outcome is how such a bug hides.
        /// </para>
        /// <para>
        /// The change tracker is cleared before the exception leaves, for the reason the
        /// services that used to do it themselves gave: the rows it was holding did not reach
        /// the database, and leaving them attached would make the next save try the same write.
        /// For a unique violation the rows are exactly the ones not to retry.
        /// </para>
        /// </remarks>
        /// <param name="context">Context whose changes are written.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of rows written.</returns>
        /// <exception cref="UniqueViolationException">A unique index refused the write.</exception>
        public static async Task<int> SaveChangesCheckedAsync(
            this FluxyDbContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (UniqueViolation(exception) is { } violation)
            {
                context.ChangeTracker.Clear();

                throw new UniqueViolationException(violation.ConstraintName, exception);
            }
        }

        /// <summary>
        /// The unique violation inside a failed save, or null when the failure was something
        /// else.
        /// </summary>
        /// <remarks>
        /// Read from the provider's own type rather than from its message: the message is
        /// localized and its shape is not a contract, while <see cref="PostgresException"/>
        /// with this <c>SqlState</c> and a <c>ConstraintName</c> is.
        /// </remarks>
        private static PostgresException? UniqueViolation(DbUpdateException exception)
            => exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                } violation
                ? violation
                : null;
    }
}
