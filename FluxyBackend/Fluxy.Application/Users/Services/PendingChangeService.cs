using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Models;
using Fluxy.Core.Users.Repositories;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// The machinery of a change that waits for a one-time code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two flows stage a change and wait for a code to confirm it: a profile change - a
    /// username, an address, a password, a login guard - and the public password reset. Each
    /// of them wrote its own copy of the same three steps: stage the row with the hash of the
    /// code, check a code against the row that holds it, and remove the row once it is spent.
    /// The steps are the same because the proof is the same; what differs is the wording of
    /// the message, where it is sent, and what happens after the code is accepted.
    /// </para>
    /// <para>
    /// So the machinery lives here and the flow does not. A flow says which change it is
    /// staging and what it does with the code; this says how a change is stored, how a code is
    /// checked and when it stops being accepted.
    /// </para>
    /// </remarks>
    public sealed class PendingChangeService
    {
        private const string PendingChangeIndexName = "IX_pending_changes_user_id";

        private readonly IPendingChangeRepository _pendingChanges;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PendingChangeService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PendingChangeService"/> class.
        /// </summary>
        /// <param name="pendingChanges">Repository holding the staged rows.</param>
        /// <param name="unitOfWork">The unit of work the rows are committed through.</param>
        /// <param name="timeProvider">The clock that decides whether a code has expired.</param>
        /// <param name="logger">Where the stage of a change is recorded.</param>
        public PendingChangeService(
            IPendingChangeRepository pendingChanges,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<PendingChangeService> logger)
        {
            _pendingChanges = pendingChanges;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Stores a change and the hash of the code that will confirm it.
        /// </summary>
        /// <param name="userId">Account the change belongs to.</param>
        /// <param name="kind">Which operation the change is.</param>
        /// <param name="code">The one-time code, in clear text. Only its hash is stored.</param>
        /// <param name="expiresAt">Moment the code stops being accepted.</param>
        /// <param name="targetValue">The username or address the change will write, if any.</param>
        /// <param name="newPassword">The new password the change will write, if any.</param>
        /// <param name="payload">
        /// Staged content that does not fit the columns, such as a login guard. Null for the
        /// changes the columns already carry.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// True when the change was stored. False when a concurrent request for the same
        /// account won the unique index: the winner stored a row of its own and mailed a code
        /// of its own, so this one has nothing to send.
        /// </returns>
        public async Task<bool> StageAsync(
            Guid userId,
            PendingChangeKind kind,
            string code,
            DateTimeOffset expiresAt,
            string? targetValue = null,
            string? newPassword = null,
            string? payload = null,
            CancellationToken cancellationToken = default)
        {
            await _pendingChanges.StageAsync(
                new PendingChange
                {
                    Id = Guid.Empty,
                    UserId = userId,
                    Kind = kind,
                    TargetValue = targetValue,
                    // The password is hashed here, once, and copied onto the account only after
                    // the code matches. No clear text and no reversible form of it is ever
                    // stored.
                    NewPasswordHash = newPassword is null
                        ? null
                        : BCrypt.Net.BCrypt.HashPassword(newPassword),
                    CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                    Payload = payload,
                    ExpiresAt = expiresAt
                },
                cancellationToken);

            if (await _unitOfWork.TrySaveAsync([PendingChangeIndexName], cancellationToken) is not null)
            {
                _logger.LogInformation(
                    "A concurrent request for account {UserId} staged its {Kind} first, so this " +
                    "one has no change of its own to send a code for.",
                    userId,
                    kind);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks a code against the change that is waiting for it.
        /// </summary>
        /// <param name="pending">The staged change, as the repository reads it.</param>
        /// <param name="code">The code the visitor typed.</param>
        /// <returns>
        /// What the code turned out to be. A wrong code is reported before an expired one,
        /// because a code that does not match was never a code at all.
        /// </returns>
        /// <remarks>
        /// BCrypt recomputes the hash from the salt inside the stored value, so verifying a
        /// code costs as much as producing one. That is why the endpoints are throttled before
        /// they get here rather than after.
        /// </remarks>
        public PendingCodeStatus Verify(PendingChange pending, string? code)
        {
            var trimmed = code?.Trim() ?? string.Empty;

            if (trimmed.Length == 0
                || !BCrypt.Net.BCrypt.Verify(trimmed, pending.CodeHash))
            {
                return PendingCodeStatus.Invalid;
            }

            return _timeProvider.GetUtcNow() > pending.ExpiresAt
                ? PendingCodeStatus.Expired
                : PendingCodeStatus.Valid;
        }

        /// <summary>
        /// Removes a change that has been confirmed or given up on.
        /// </summary>
        /// <param name="pendingId">Identifier of the row to remove.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public Task RemoveAsync(Guid pendingId, CancellationToken cancellationToken = default)
            => _pendingChanges.RemoveAsync(pendingId, cancellationToken);
    }
}
