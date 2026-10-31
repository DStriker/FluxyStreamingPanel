using Fluxy.API.Contracts.Common;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Outcomes;
using Microsoft.AspNetCore.Http;

namespace Fluxy.API.Contracts.Blacklists
{
    /// <summary>
    /// The one place a <see cref="BlacklistEntryAction"/> becomes an HTTP status and a code.
    /// </summary>
    /// <remarks>
    /// The service knows nothing about HTTP and this class knows nothing about blacklists
    /// beyond which word maps to which answer. A duplicate answers 409 <c>blacklist_value_exists</c>
    /// rather than <c>already_exists</c> because the refusal is about a value, and the three
    /// lists are one resource - a client branching on this code knows it is looking at a
    /// blacklist without also having to know which one.
    /// </remarks>
    public static class BlacklistEntryResponses
    {
        /// <summary>Code reported once an entry has been created.</summary>
        public const string CreatedCode = "blacklist_entry_created";

        /// <summary>Code reported once an entry has been changed.</summary>
        public const string UpdatedCode = "blacklist_entry_updated";

        /// <summary>Code reported once an entry has been deleted.</summary>
        public const string DeletedCode = "blacklist_entry_deleted";

        /// <summary>Code reported once every entry of one list has been deleted.</summary>
        public const string ClearedCode = "blacklist_entries_cleared";

        /// <summary>Code reported when no entry answers to that identifier.</summary>
        public const string NotFoundCode = "blacklist_entry_not_found";

        /// <summary>Code reported when the list already holds that value.</summary>
        public const string ValueExistsCode = "blacklist_value_exists";

        /// <summary>Answer describing <paramref name="outcome"/>.</summary>
        /// <param name="outcome">What the service reported.</param>
        /// <returns>The status and the body, with the rejected fields when the input was the problem.</returns>
        public static (int Status, MessageResponse Body) Describe(BlacklistEntryOutcome outcome)
        {
            var (status, code, message) = outcome.Action switch
            {
                BlacklistEntryAction.Created =>
                    (StatusCodes.Status201Created, CreatedCode, "The entry has been created."),

                BlacklistEntryAction.Updated =>
                    (StatusCodes.Status200OK, UpdatedCode, "The entry has been updated."),

                BlacklistEntryAction.Removed =>
                    (StatusCodes.Status200OK, DeletedCode, "The entry has been deleted."),

                BlacklistEntryAction.Cleared =>
                    (StatusCodes.Status200OK, ClearedCode, "Every entry of the list has been deleted."),

                BlacklistEntryAction.NotFound =>
                    (StatusCodes.Status404NotFound,
                        NotFoundCode,
                        "There is no blacklist entry with that identifier."),

                BlacklistEntryAction.AlreadyExists =>
                    (StatusCodes.Status409Conflict,
                        ValueExistsCode,
                        "This list already holds that value."),

                BlacklistEntryAction.Invalid =>
                    (StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Some of the values you entered are not valid."),

                // Unreachable: the service reports nothing outside this enum. Thrown rather
                // than answered as a 200, because a code a client has never seen with a status
                // it was never told to expect is a silent failure somewhere above this line.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome.Action,
                    "Unknown blacklist outcome.")
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
