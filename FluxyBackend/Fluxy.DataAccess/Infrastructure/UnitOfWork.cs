using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Context;

namespace Fluxy.DataAccess.Infrastructure
{
    /// <summary>
    /// Commits the staged changes of one request through the context that holds them.
    /// </summary>
    /// <remarks>
    /// The save goes through <c>SaveChangesCheckedAsync</c>, so a unique refusal reaches the
    /// caller as <see cref="UniqueViolationException"/> carrying the index name, and the change
    /// tracker is cleared before the exception leaves. A service that catches the refusal can
    /// therefore continue with a clean tracker instead of retrying the rows that were refused.
    /// </remarks>
    internal sealed class UnitOfWork : IUnitOfWork
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
        /// </summary>
        /// <param name="context">Context whose staged changes are written.</param>
        public UnitOfWork(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesCheckedAsync(cancellationToken);
    }
}
