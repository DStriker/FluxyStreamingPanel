namespace Fluxy.API.Contracts
{
    /// <summary>
    /// What the create form sends to make a group in one request.
    /// </summary>
    /// <remarks>
    /// No data annotations on purpose, and the reason is the order of the checks: an attribute
    /// is validated by the framework *before* the action runs, so a body that failed one would
    /// be answered without ever reaching the antiforgery check. Every field is checked in the
    /// action and then in the service, in the order the rest of this API checks things - CSRF
    /// first, rules second, database last - and the field names land in <c>errors</c> under the
    /// same camelCase spelling the body used.
    ///
    /// Each of the three required values travels as the name it is read by rather than as a
    /// number in a column. A role of <c>3</c> would be a number only this code could translate,
    /// and an <c>admin</c> that the client spelled with a typo would be refused either way -
    /// so the two spellings a client could pick from are worth agreeing on now.
    /// </remarks>
    public sealed class CreateUserGroupRequest
    {
        /// <summary>Display name the group gets. Unique, compared case sensitively.</summary>
        public string? Name { get; init; }

        /// <summary>Access level every member will inherit, as the name of the enum member.</summary>
        public string? Role { get; init; }

        /// <summary>State the group starts in, as the name of the enum member.</summary>
        public string? Status { get; init; }

        /// <summary>
        /// Permissions the group grants, each spelled as the catalog spells it. A grant the
        /// role does not own is dropped rather than refused, so this can be an empty list.
        /// </summary>
        public IReadOnlyList<string>? Permissions { get; init; }
    }
}
