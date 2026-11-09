using Fluxy.Core.Users.Enums;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The wire vocabulary of <see cref="UserLogRecordType"/>: the snake_case name a client sends
    /// and reads, and the member each spelling names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The table lives here so it can be tested where it is used.</b> The unit test project
    /// references Core and Application only, never the API layer, so a mapping kept in the
    /// transport would be reachable only through a running host. The transport parses the query
    /// string and refuses what names nothing; the answer itself is a pure rule with no HTTP in it.
    /// </para>
    /// <para>
    /// <b>Comparison is case-insensitive and whitespace-tolerant</b>, the same as every other list
    /// filter in this API: <c>recordTypes=LOGIN</c> and <c>recordTypes=login</c> are one request.
    /// A value that names no member answers false, and the transport turns that into a refusal
    /// under <c>recordTypes</c> - never into "no filter", because a filter that quietly widened to
    /// everything would answer a request the caller did not make.
    /// </para>
    /// <para>
    /// <b>The names run in both directions.</b> <see cref="WireName"/> is what a response carries
    /// and what <see cref="TryParseWire"/> accepts, so a client never has to learn two vocabularies
    /// for one fact.
    /// </para>
    /// </remarks>
    public static class UserLogRecordTypes
    {
        /// <summary>
        /// Every record type, in declaration order - which is also the value the database stores.
        /// </summary>
        public static IReadOnlyList<UserLogRecordType> All { get; } =
        [
            UserLogRecordType.RegistrationSubmit,
            UserLogRecordType.RegistrationConfirm,
            UserLogRecordType.Login,
            UserLogRecordType.PasswordResetRequest,
            UserLogRecordType.PasswordResetConfirm,
            UserLogRecordType.UsernameChange,
            UserLogRecordType.EmailChange,
            UserLogRecordType.PasswordChange,
            UserLogRecordType.LoginGuardChange,
            UserLogRecordType.Logout
        ];

        /// <summary>
        /// The sentence a filter that names no record type is refused with. Built from
        /// <see cref="All"/> rather than written out, because a vocabulary that grows is a hint
        /// that would otherwise keep naming yesterday's members.
        /// </summary>
        public static string UnknownRecordTypeHint { get; } =
            "The record type must be one of: " +
            string.Join(", ", All.Select(WireName)) +
            ".";

        /// <summary>The snake_case name a client sends and reads.</summary>
        /// <param name="recordType">Member to name.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="recordType"/> names no member of the vocabulary.
        /// </exception>
        public static string WireName(UserLogRecordType recordType) => recordType switch
        {
            UserLogRecordType.RegistrationSubmit => "registration_submit",
            UserLogRecordType.RegistrationConfirm => "registration_confirm",
            UserLogRecordType.Login => "login",
            UserLogRecordType.PasswordResetRequest => "password_reset_request",
            UserLogRecordType.PasswordResetConfirm => "password_reset_confirm",
            UserLogRecordType.UsernameChange => "username_change",
            UserLogRecordType.EmailChange => "email_change",
            UserLogRecordType.PasswordChange => "password_change",
            UserLogRecordType.LoginGuardChange => "login_guard_change",
            UserLogRecordType.Logout => "logout",
            _ => throw new ArgumentOutOfRangeException(
                nameof(recordType),
                recordType,
                "No wire name is defined for that record type.")
        };

        /// <summary>Reads a wire name back into the member it names.</summary>
        /// <param name="value">Name as sent, possibly null.</param>
        /// <param name="recordType">The member the name stands for, when there is one.</param>
        /// <returns>True when the name names a member of the vocabulary.</returns>
        public static bool TryParseWire(string? value, out UserLogRecordType recordType)
        {
            recordType = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();

            foreach (var member in All)
            {
                if (string.Equals(WireName(member), text, StringComparison.OrdinalIgnoreCase))
                {
                    recordType = member;

                    return true;
                }
            }

            return false;
        }
    }
}
