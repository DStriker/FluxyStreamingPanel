using Fluxy.Core.Models.Users;

namespace Fluxy.API.Contracts
{
    /// <summary>
    /// The one place a bulk outcome becomes a body: one code for the run as a whole, one entry
    /// per row, and a per-row code taken from the very tables the single-row endpoints read.
    /// </summary>
    /// <remarks>
    /// Nothing here decides what a row's code means. Each entry is answered by
    /// <see cref="UserAdminResponses.Describe"/> or <see cref="UserGroupResponses.Describe"/> -
    /// the same call the one-row endpoint makes - so a bulk run and a single request spell the
    /// same fact the same way, and a rule that changes one of them changes both at once. The
    /// only new thing here is the shape around them: counts, and a sentence that says how much
    /// of the run landed.
    ///
    /// <c>Ok</c> is read off the status that mapping produced rather than off the outcome word,
    /// which keeps "what a client counts as success" in the one place the statuses are already
    /// decided. It is a number comparison rather than a list of members so that a refusal
    /// added tomorrow counts as a refusal without anybody remembering to add it here.
    /// </remarks>
    internal static class BulkResponses
    {
        /// <summary>Code reported once a run over accounts has been answered for.</summary>
        public const string UsersCompletedCode = "users_bulk_completed";

        /// <summary>Code reported once a run over groups has been answered for.</summary>
        public const string GroupsCompletedCode = "user_groups_bulk_completed";

        /// <summary>Body for a run over accounts.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkUserOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = UserAdminResponses.Describe(new AdminUserOutcome
                    {
                        Action = item.Action,
                        UserId = item.UserId,
                        Errors = item.Errors
                    });

                    return ToItem(item.UserId, status, body);
                })
                .ToList();

            return Answer(UsersCompletedCode, Summary(outcome.Action, items), items);
        }

        /// <summary>Body for a run over groups.</summary>
        /// <param name="outcome">What the service reported, one entry per identifier.</param>
        public static BulkOperationResponse Describe(BulkUserGroupOutcome outcome)
        {
            var items = outcome.Items
                .Select(item =>
                {
                    var (status, body) = UserGroupResponses.Describe(new UserGroupOutcome
                    {
                        Action = item.Action,
                        GroupId = item.GroupId,
                        Errors = item.Errors
                    });

                    return ToItem(item.GroupId, status, body);
                })
                .ToList();

            return Answer(GroupsCompletedCode, Summary(outcome.Action, items), items);
        }

        private static BulkItemResponse ToItem(
            Guid id,
            int status,
            MessageResponse body)
            => new()
            {
                Id = id.ToString(),
                Ok = status < StatusCodes.Status400BadRequest,
                Code = body.Code,
                Message = body.Message,
                Errors = body.Errors
            };

        private static BulkOperationResponse Answer(
            string code,
            string message,
            IReadOnlyList<BulkItemResponse> items)
            => new()
            {
                Code = code,
                Message = message,
                Succeeded = items.Count(item => item.Ok),
                Failed = items.Count(item => !item.Ok),
                Items = items
            };

        /// <summary>
        /// The sentence naming how much of a run over accounts landed.
        /// </summary>
        /// <remarks>
        /// Built from the operation rather than from the first row's code, because the codes
        /// inside one run are all different - a block that refused one account and completed
        /// nine has nine <c>user_blocked</c> entries and one <c>cannot_block_self</c>, and a
        /// sentence assembled from those would be a sentence about whichever one came first.
        /// The landed count beside it is what makes "9 of 10" read as the two halves it is.
        /// </remarks>
        private static string Summary(
            BulkUserAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkUserAction.Block => $"{Landed(items)} of {items.Count} accounts blocked.",
                BulkUserAction.Unblock => $"{Landed(items)} of {items.Count} accounts unblocked.",
                BulkUserAction.Delete => $"{Landed(items)} of {items.Count} accounts deleted.",

                BulkUserAction.ConfirmRegistration =>
                    $"{Landed(items)} of {items.Count} accounts registered.",

                BulkUserAction.AssignGroup =>
                    $"{Landed(items)} of {items.Count} accounts moved to that group.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} accounts changed."
            };

        /// <summary>The sentence naming how much of a run over groups landed.</summary>
        private static string Summary(
            BulkUserGroupAction action,
            IReadOnlyList<BulkItemResponse> items)
            => action switch
            {
                BulkUserGroupAction.Block => $"{Landed(items)} of {items.Count} groups blocked.",
                BulkUserGroupAction.Unblock => $"{Landed(items)} of {items.Count} groups unblocked.",
                BulkUserGroupAction.Delete => $"{Landed(items)} of {items.Count} groups deleted.",

                // Unreachable: the enum holds no other member.
                _ => $"{Landed(items)} of {items.Count} groups changed."
            };

        private static int Landed(IReadOnlyList<BulkItemResponse> items)
            => items.Count(item => item.Ok);
    }
}
