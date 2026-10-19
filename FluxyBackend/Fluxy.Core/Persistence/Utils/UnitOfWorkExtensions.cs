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
    /// These methods make the shape uniform without deciding what a refusal means: they hand the
    /// violation back and let the caller ask <see cref="UniqueViolationException.IsConstraint"/>
    /// about the index it knows. A violation from an index the caller does not recognise is
    /// <b>not</b> a name conflict, and the caller rethrows it - a bug in a list writer must not
    /// be announced as "that name is already taken". The overload that takes the known indexes
    /// is where that rule lives, so that it is written once instead of once per service.
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

        /// <summary>
        /// Commits everything that was staged, and reports the unique violation that refused it
        /// only when it came from an index the caller knows.
        /// </summary>
        /// <param name="unitOfWork">The unit of work holding the staged changes.</param>
        /// <param name="knownIndexes">
        /// The unique indexes whose refusal this caller knows how to answer.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The violation that stopped the write, or <see langword="null"/> when the write went
        /// through. Any other failure still propagates.
        /// </returns>
        /// <exception cref="UniqueViolationException">
        /// A unique index outside <paramref name="knownIndexes"/> refused the write. It is
        /// rethrown rather than reported, because a refusal nobody recognises is a bug and not
        /// an outcome.
        /// </exception>
        public static async Task<UniqueViolationException?> TrySaveAsync(
            this IUnitOfWork unitOfWork,
            IReadOnlyCollection<string> knownIndexes,
            CancellationToken cancellationToken = default)
        {
            var violation = await unitOfWork.TrySaveAsync(cancellationToken);

            if (violation is null)
            {
                return null;
            }

            foreach (var index in knownIndexes)
            {
                if (violation.IsConstraint(index))
                {
                    return violation;
                }
            }

            throw violation;
        }
    }
}
