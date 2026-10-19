using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Interfaces;
using Fluxy.Core.Users.Outcomes;

namespace Fluxy.Application.Users.Services
{
    /// <summary>
    /// Applies one operation to a set of accounts, one account at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bulk endpoint is not a second implementation of the single-account operations, it
    /// is those operations applied to each row in turn. The rules that protect the caller
    /// and the rules that protect the target therefore live in exactly one place, and a
    /// hundred accounts in a request are refused for exactly the reasons one account is.
    /// </para>
    /// <para>
    /// The service is a seam rather than a move: the accounts resource has a bulk shape that
    /// no single-row endpoint has, and a client asking for it must not have to know which
    /// single-row method it is built on.
    /// </para>
    /// </remarks>
    public sealed class UserBulkService : IUserBulkService
    {
        private readonly IUserAdminService _users;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserBulkService"/> class.
        /// </summary>
        /// <param name="users">Service holding the single-account operations.</param>
        public UserBulkService(IUserAdminService users)
        {
            _users = users;
        }

        /// <inheritdoc />
        public async Task<BulkUserOutcome> BulkAsync(
            BulkUserOperation operation,
            Guid actingUserId,
            IReadOnlySet<UserPermission> granted,
            CancellationToken cancellationToken = default)
        {
            // A move with no destination is not a move. The transport refuses it with a 400
            // before any row is reached; if one ever arrives here, every account is answered
            // with the field that was missing rather than each of them being left where it
            // was. A patch whose GroupId is absent means "leave the group alone", so the
            // alternative is an operation that reports success over a hundred accounts and
            // changes none of them - the single worst answer this service can give.
            if (operation.Action is BulkUserAction.AssignGroup && operation.GroupId is null)
            {
                return new BulkUserOutcome
                {
                    Action = operation.Action,
                    Items = operation.UserIds
                        .Select(userId => new BulkUserItemResult
                        {
                            UserId = userId,
                            Action = AdminUserAction.InvalidInput,
                            Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
                            {
                                [nameof(BulkUserOperation.GroupId)] =
                                    ["A bulk move needs the group to move the accounts to."]
                            }
                        })
                        .ToList()
                };
            }

            var items = new List<BulkUserItemResult>(operation.UserIds.Count);

            foreach (var userId in operation.UserIds)
            {
                // Every entry comes from the single-account method of the same name, so the
                // refusals and the side effects are the ones those methods already have rather
                // than a copy of them - the reason this is an orchestration and not a second
                // implementation is written at length on IUserBulkService.BulkAsync.
                var outcome = operation.Action switch
                {
                    BulkUserAction.Block => await _users.SetBlockedAsync(
                        userId, true, actingUserId, granted, cancellationToken),

                    BulkUserAction.Unblock => await _users.SetBlockedAsync(
                        userId, false, actingUserId, granted, cancellationToken),

                    BulkUserAction.Delete => await _users.DeleteAsync(
                        userId, actingUserId, granted, cancellationToken),

                    BulkUserAction.ConfirmRegistration => await _users.ConfirmRegistrationAsync(
                        userId, granted, cancellationToken),

                    BulkUserAction.AssignGroup => await _users.UpdateAsync(
                        userId,
                        new UserPatch { GroupId = operation.GroupId },
                        actingUserId,
                        granted,
                        cancellationToken),

                    // Unreachable: the enum holds no other member and the transport refuses a
                    // name that maps to none. Thrown rather than skipped, because an entry
                    // missing from the answer is an account the client believes it acted on
                    // and the server never mentioned.
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(operation),
                        operation.Action,
                        "Unknown bulk user action.")
                };

                items.Add(new BulkUserItemResult
                {
                    UserId = userId,
                    Action = outcome.Action,
                    Errors = outcome.Errors
                });
            }

            return new BulkUserOutcome { Action = operation.Action, Items = items };
        }
    }
}
