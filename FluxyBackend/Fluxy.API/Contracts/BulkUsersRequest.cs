namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What a bulk operation over accounts carries: which operation, over which accounts, and
    /// where to move them.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose, and the reason is the order of the checks the create
    /// body gives at length: an attribute is validated by the framework *before* the action
    /// runs, so a body that failed one would be answered without ever reaching the antiforgery
    /// check. Every field here is parsed in the action, after the CSRF pair has been accepted
    /// and before a single row is read.
    ///
    /// All three arrive as text rather than as bound values. A uuid bound by the framework
    /// would answer a typo with a model-state error the action never sees, and an action
    /// bound to an enum would accept only a member name spelled in full - so the
    /// conventional <c>"block"</c> would be refused while <c>"Block"</c> worked, which is
    /// exactly the lesson <c>sortOrder</c> taught this API. Both are parsed below.
    /// </remarks>
    public sealed class BulkUsersRequest
    {
        /// <summary>
        /// The operation to run, as text: <c>block</c>, <c>unblock</c>, <c>delete</c>,
        /// <c>confirm-registration</c> or <c>assign-group</c>.
        /// </summary>
        public string? Action { get; init; }

        /// <summary>
        /// The accounts to act on, as uuids. At most one page of them - see
        /// <c>BulkOperationLimits</c>.
        /// </summary>
        public IReadOnlyList<string>? Ids { get; init; }

        /// <summary>
        /// Destination of a <c>assign-group</c>, as a uuid. Required for that operation and
        /// ignored for the other four.
        /// </summary>
        public string? GroupId { get; init; }
    }
}
