using Fluxy.Core.Models.Users;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// Turns what an admin operation reported into what a client is told: an HTTP status, a
    /// machine readable code and an English fallback.
    /// </summary>
    /// <remarks>
    /// The whole table lives in the transport layer for the reason
    /// <see cref="ProfileResponses"/> keeps one there: the service knows only
    /// <see cref="AdminUserAction"/>, so which of those is a 201 and which is a 409 can change
    /// without touching a rule - and a rule can change without anybody having to decide what
    /// status code it should start returning.
    ///
    /// Two codes are deliberately *not* of this controller's making. <c>registration_confirmed</c>
    /// is the same code a visitor's own confirmation gets, and <c>user_already_exists</c> is the
    /// same one a registration conflict gets, because they are the same facts - a client
    /// branching on either behaves the same way no matter which door the answer came through.
    ///
    /// The codes are snake_case, matching the naming the protocol already uses, and they are
    /// the part of the contract a client branches on. Renaming one breaks every client;
    /// rewriting the wording beside it breaks nothing.
    /// </remarks>
    internal static class UserAdminResponses
    {
        /// <summary>Code reported once an account has been created.</summary>
        public const string CreatedCode = "user_created";

        /// <summary>Code reported once a change has been written to the row.</summary>
        public const string UpdatedCode = "user_updated";

        /// <summary>Code reported once an account has been deleted.</summary>
        public const string DeletedCode = "user_deleted";

        /// <summary>Code reported once an account has been put into Blocked.</summary>
        public const string BlockedCode = "user_blocked";

        /// <summary>Code reported once an account has been taken out of Blocked.</summary>
        public const string UnblockedCode = "user_unblocked";

        /// <summary>Code reported when no account answers to that identifier.</summary>
        public const string NotFoundCode = "user_not_found";

        /// <summary>
        /// Code reported when the action does not apply to the state the account is in.
        /// </summary>
        public const string InvalidStatusCode = "invalid_status";

        /// <summary>Code reported for an attempt to delete the account making the request.</summary>
        public const string CannotDeleteSelfCode = "cannot_delete_self";

        /// <summary>Code reported for an attempt to block the account making the request.</summary>
        public const string CannotBlockSelfCode = "cannot_block_self";

        /// <summary>Code reported for an attempt to take one's own access level down.</summary>
        public const string CannotDemoteSelfCode = "cannot_demote_self";

        /// <summary>
        /// Code reported for an unblock of an account that is blocked by its group rather than
        /// by its own row, so there is nothing on the row for an unblock to clear.
        /// </summary>
        public const string BlockedByGroupCode = "user_blocked_by_group";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(AdminUserOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                AdminUserAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The account has been created."),

                AdminUserAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The account has been updated."),

                AdminUserAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The account has been deleted."),

                AdminUserAction.RegistrationConfirmed =>
                    (StatusCodes.Status200OK,
                        RegistrationResponses.ConfirmedCode,
                        "The account has been registered."),

                AdminUserAction.Blocked =>
                    (StatusCodes.Status200OK, BlockedCode, "The account has been blocked and its sessions ended."),

                AdminUserAction.Unblocked =>
                    (StatusCodes.Status200OK, UnblockedCode, "The account has been unblocked."),

                AdminUserAction.NotFound =>
                    (StatusCodes.Status404NotFound, NotFoundCode, "There is no account with that identifier."),

                AdminUserAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        "user_already_exists",
                        "That username or email address is already taken."),

                AdminUserAction.InvalidInput =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                AdminUserAction.InvalidStatus =>
                    (StatusCodes.Status409Conflict,
                        InvalidStatusCode,
                        "That action does not apply to the account's current state. Reload the list."),

                AdminUserAction.CannotDeleteSelf =>
                    (StatusCodes.Status400BadRequest,
                        CannotDeleteSelfCode,
                        "You cannot delete your own account."),

                AdminUserAction.CannotBlockSelf =>
                    (StatusCodes.Status400BadRequest,
                        CannotBlockSelfCode,
                        "You cannot block your own account. Ask another administrator."),

                AdminUserAction.CannotDemoteSelf =>
                    (StatusCodes.Status400BadRequest,
                        CannotDemoteSelfCode,
                        "You cannot lower your own access level. Ask another administrator."),

                AdminUserAction.BlockedByGroup =>
                    (StatusCodes.Status409Conflict,
                        BlockedByGroupCode,
                        "That account is blocked by the group it belongs to. " +
                        "Unblock the group instead."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown admin user outcome.")
            };

            return (status, new MessageResponse
            {
                Code = code,
                Message = message,
                Errors = FieldErrorKeys.FromPropertyNames(outcome.Errors)
            });
        }
    }
}
