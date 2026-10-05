namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Which allow list a login guard rule belongs to.
    /// </summary>
    /// <remarks>
    /// Stored as a <c>smallint</c>, like every other enum of this project, so the numeric values
    /// are part of the schema: add a member at the end, never renumber one.
    ///
    /// The value starts at one rather than zero so that <c>default(LoginGuardRuleKind)</c> is not
    /// a valid kind - a row that forgot to state its purpose should be refused rather than
    /// interpreted as the first member of the list.
    /// </remarks>
    public enum LoginGuardRuleKind
    {
        /// <summary>An allowed address: exact or CIDR, IPv4 or IPv6.</summary>
        IpAddress = 1,

        /// <summary>An allowed country, as an ISO 3166-1 alpha-2 code.</summary>
        Country = 2,

        /// <summary>An allowed provider, matched by autonomous system number.</summary>
        AutonomousSystem = 3
    }
}
