using Fluxy.Core.Persistence.Exceptions;

namespace Fluxy.Core.Persistence.Interfaces
{
    /// <summary>
    /// Commits everything the repositories of one request have staged.
    /// </summary>
    /// <remarks>
    /// The unit of work exists so that a business operation is one save rather than a save per
    /// repository call. A caller that stages an account and its rules, or a refresh token and
    /// its replacement, decides where the transaction boundary is; a repository that saved on
    /// its own would decide it instead, and the second half of the operation could fail after
    /// the first half had already been committed.
    ///
    /// Revokes and cleanups that use <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> are exempt by
    /// design: they run immediately and cannot be rolled back by this method.
    /// </remarks>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Writes every staged change as one transaction.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <exception cref="UniqueViolationException">
        /// A unique index refused the write. The exception names the index so the caller can
        /// decide whether the refusal is an outcome it understands.
        /// </exception>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
