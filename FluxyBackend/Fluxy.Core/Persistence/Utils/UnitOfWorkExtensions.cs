using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;

namespace Fluxy.Core.Persistence.Utils
{
    /// <summary>
    /// The one way to commit, for callers that can name the index a unique violation came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six services commit through the unit of work, and each of them wrote its own
    /// <c>try</c>/<c>catch</c> around the same call, in four different shapes: two swallowed the
    /// violation and answered a boolean, two handed it back to the caller, one turned it into a
    /// domain exception and one returned it. The shape was never the point - what each service
    /// wanted to say about a refused write was.
    /// </para>
    /// <para>
    /// This method makes the shape uniform without deciding what a refusal means: it hands the
    /// violation back and lets the caller ask <see cref="UniqueViolationException.IsConstraint"/>
    /// about the index it knows. A violation from an index the caller does not recognise is
    /// <b>not</b> a name conflict, and the caller rethrows it - a bug in a list writer must not
    /// be announced as "that name is already taken".
    /// </para>
    /// </remarks>
    public static class UnitOfWorkExtensions
    {
        /// <summary>
        /// Commits everything that was staged, and reports the unique violation that refused it.
        /// </summary>
        /// <param name="unitOfWork">The unit of work holding the staged changes.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The violation that stopped the write, or <see langword="null"/> when the write went
        /// through. Any other failure still propagates.
        /// </returns>
        public static async Task<UniqueViolationException?> TrySaveAsync(
            this IUnitOfWork unitOfWork,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (UniqueViolationException exception)
            {
                return exception;
            }
        }
    }
}
