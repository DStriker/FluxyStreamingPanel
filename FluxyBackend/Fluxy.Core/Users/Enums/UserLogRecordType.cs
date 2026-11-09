namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// What one user log row records, as the kind of event it describes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This enum says what happened, not whether it worked.</b> Whether the attempt succeeded
    /// is a stored boolean on the row (<c>is_success</c>), because the same kind of event can end
    /// either way: a sign-in that was refused is still a sign-in, and a record type derived from
    /// a success flag would have to double the vocabulary every time a refusal appeared.
    /// </para>
    /// <para>
    /// <b>The wire vocabulary is the snake_case name, not the member name.</b> A query string and
    /// a response carry <c>registration_submit</c>, while the database stores the member as a
    /// <c>smallint</c>; <see cref="Fluxy.Core.Users.Utils.UserLogRecordTypes"/> is the one place
    /// the two are tied together. The numeric values are declaration order and are schema: they
    /// are already written into rows and must not be renumbered.
    /// </para>
    /// </remarks>
    public enum UserLogRecordType
    {
        /// <summary>A registration form was submitted and an account was staged for it.</summary>
        RegistrationSubmit = 0,

        /// <summary>A mailed registration code was accepted and the account became usable.</summary>
        RegistrationConfirm = 1,

        /// <summary>A sign-in was attempted, successfully or not.</summary>
        Login = 2,

        /// <summary>A password reset was requested from the public form.</summary>
        PasswordResetRequest = 3,

        /// <summary>A mailed password reset code was accepted and the password replaced.</summary>
        PasswordResetConfirm = 4,

        /// <summary>The account asked to change its login name.</summary>
        UsernameChange = 5,

        /// <summary>The account asked to change its email address.</summary>
        EmailChange = 6,

        /// <summary>The account changed its password from the profile screen.</summary>
        PasswordChange = 7,

        /// <summary>The account changed its login guard settings.</summary>
        LoginGuardChange = 8,

        /// <summary>The account ended its session.</summary>
        Logout = 9
    }
}
