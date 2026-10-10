namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a bulk operation over groups carries: which operation, over which groups.
    /// </summary>
    /// <remarks>
    /// Parsed in the action rather than by data annotations and by bound values rather than
    /// strings, for the reasons the accounts body gives: the antiforgery check has to run
    /// before any refusal a body could cause, and an enum bound from the framework would
    /// accept only a member name spelled in full while refusing the conventional lowercase one.
    /// </remarks>
    public sealed class BulkUserGroupsRequest
    {
        /// <summary>
        /// The operation to run, as text: <c>block</c>, <c>unblock</c> or <c>delete</c>.
        /// </summary>
        public string? Action { get; init; }

        /// <summary>
        /// The groups to act on, as uuids. At most one page of them - see
        /// <c>BulkOperationLimits</c>.
        /// </summary>
        public IReadOnlyList<string>? Ids { get; init; }
    }
}
