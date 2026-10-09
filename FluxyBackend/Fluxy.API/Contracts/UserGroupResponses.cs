using Fluxy.Core.Models.Users;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The one place a <see cref="UserGroupAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about groups beyond
    /// which word maps to which answer, which is the whole reason both exist: a rule can change
    /// without anybody having to decide what status code it should start returning, and a
    /// status can move without touching a rule.
    ///
    /// The codes follow the ones the accounts already use - <c>user_already_exists</c>,
    /// <c>user_not_found</c> - one level deeper, so a client that knows the account set knows
    /// the shape of this one without reading a second table of exceptions.
    /// </remarks>
    public static class UserGroupResponses
    {
        /// <summary>Code reported once a group has been created.</summary>
        public const string CreatedCode = "user_group_created";

        /// <summary>Code reported once a group has been changed.</summary>
        public const string UpdatedCode = "user_group_updated";

        /// <summary>Code reported once a group has been deleted, along with its permissions.</summary>
        public const string DeletedCode = "user_group_deleted";

        /// <summary>Code reported when no group answers to that identifier.</summary>
        public const string NotFoundCode = "user_group_not_found";

        /// <summary>Code reported when another group already holds that name.</summary>
        public const string AlreadyExistsCode = "user_group_already_exists";

        /// <summary>
        /// Code reported when the request changed a base group in some way other than renaming
        /// it. A distinct code rather than a validation error, because it is not a value that
        /// was wrong - it is a row this installation refuses to let anybody reshape.
        /// </summary>
        public const string ImmutableBaseCode = "user_group_immutable";

        /// <summary>
        /// Code reported when a group still has members, so deleting it would strand them.
        /// </summary>
        public const string InUseCode = "user_group_in_use";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status, the body, and the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(UserGroupOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                UserGroupAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The group has been created."),

                UserGroupAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The group has been updated."),

                UserGroupAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The group has been deleted."),

                UserGroupAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no group with that identifier."),

                UserGroupAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        AlreadyExistsCode,
                        "That group name is already taken."),

                UserGroupAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                UserGroupAction.ImmutableBase =>
                    (StatusCodes.Status409Conflict,
                        ImmutableBaseCode,
                        "The three groups this installation is built from may only be renamed. " +
                        "Create a group of your own for anything else."),

                UserGroupAction.InUse =>
                    (StatusCodes.Status409Conflict,
                        InUseCode,
                        "Accounts still belong to this group. Move them somewhere else first."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown user group outcome.")
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
