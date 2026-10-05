namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// Which operation a row in <c>pending_changes</c> is waiting to finish.
    /// </summary>
    /// <remarks>
    /// Stored as a <c>smallint</c>, like every other enum of this project, so the numeric values
    /// are part of the schema: add a member at the end, never renumber one.
    ///
    /// The value starts at one rather than zero so that <c>default(PendingChangeKind)</c> is not
    /// a valid kind - a row that forgot to state its purpose should be refused rather than
    /// interpreted as the first member of the list.
    /// </remarks>
    public enum PendingChangeKind
    {
        /// <summary>
        /// A password reset requested from the public form, by somebody who was not signed in.
        /// </summary>
        PasswordReset = 1,

        /// <summary>A new login name, waiting to be confirmed from the profile page.</summary>
        ChangeUsername = 2,

        /// <summary>A new email address, waiting to be confirmed from the profile page.</summary>
        ChangeEmail = 3,

        /// <summary>A new password, waiting to be confirmed from the profile page.</summary>
        ChangePassword = 4,

        /// <summary>
        /// A new login guard (the two switches and the three allow lists), waiting to be
        /// confirmed from the profile page. Its content travels in the pending row's payload,
        /// because a guard of five networks does not fit the staged value column.
        /// </summary>
        ChangeLoginGuard = 5
    }
}
