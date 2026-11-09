using System.Text;

namespace Fluxy.Core.Users.Utils
{
    /// <summary>
    /// The English sentences a user log entry carries, built and read back in one place, because
    /// the wording is what the table draws, what the search box matches, and what an auditor reads
    /// after the account it names is gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>English, not the reader's language.</b> The description is stored once and read by every
    /// screen; a row written in one operator's language would read as noise to the next. The
    /// frontend renders its own translation from the record type and the outcome where it wants
    /// one, and treats this sentence as the auditable record.
    /// </para>
    /// <para>
    /// <b>One shape, one inverse.</b> Every sentence is
    /// <c>{FixedPhrase}: {subject}</c> or <c>{FixedPhrase}: {subject} | {detail}</c>, and
    /// <see cref="TryGetSubject"/> reads the middle part back. The fixed phrase never contains the
    /// <c>": "</c> separator and never interpolates user text before it, so the first separator is
    /// always the one the builder wrote - which is what makes the inverse exact rather than
    /// heuristic.
    /// </para>
    /// <para>
    /// <b>Nothing secret is ever a subject or a detail.</b> No password, one-time code or token
    /// reaches this type: a permanent audit trail that held a credential would be a credential
    /// nobody can rotate, and the call sites are written to name the account instead.
    /// </para>
    /// </remarks>
    public static class UserLogDescriptions
    {
        /// <summary>Longest description a row may store.</summary>
        public const int DescriptionMaxLength = 500;

        /// <summary>Longest subject the description keeps, after sanitization.</summary>
        public const int SubjectMaxLength = 64;

        /// <summary>The subject a sentence carries when the caller had nothing to name.</summary>
        public const string EmptySubject = "(empty)";

        /// <summary>Separator between the fixed phrase and the subject.</summary>
        public const string SubjectSeparator = ": ";

        /// <summary>Separator between the subject and the detail, when there is a detail.</summary>
        public const string DetailSeparator = " | ";

        // The closed set of fixed phrases a sentence may begin with. The phrase is the searchable,
        // auditable half of the sentence and the part a translation can be built from, so it is
        // fixed text rather than something a flow writes inline. None of these carries the
        // `": "` separator and none interpolates user text: user text is always the subject, and
        // the first separator in a sentence is therefore always the one Describe wrote.

        /// <summary>A registration form was accepted and the account staged.</summary>
        public const string RegistrationSubmitted = "Registration submitted";

        /// <summary>A registration was refused because the identifier is taken.</summary>
        public const string RegistrationRefusedAlreadyExists = "Registration refused (already exists)";

        /// <summary>A registration was refused because a supplied value broke a rule.</summary>
        public const string RegistrationRefusedInvalidInput = "Registration refused (invalid input)";

        /// <summary>A registration was refused because this installation cannot send mail.</summary>
        public const string RegistrationRefusedNotConfigured =
            "Registration refused (mail not configured)";

        /// <summary>A registration was stored but its code could not be mailed.</summary>
        public const string RegistrationFailedEmailDelivery = "Registration failed (email delivery)";

        /// <summary>A mailed registration code was accepted.</summary>
        public const string RegistrationConfirmed = "Registration confirmed";

        /// <summary>A registration confirmation was refused because the code does not match.</summary>
        public const string RegistrationConfirmFailedInvalidCode =
            "Registration confirmation failed (invalid code)";

        /// <summary>A registration confirmation was refused because the code window closed.</summary>
        public const string RegistrationConfirmFailedExpiredCode =
            "Registration confirmation failed (code expired)";

        /// <summary>A sign-in succeeded.</summary>
        public const string LoginSucceeded = "Login succeeded";

        /// <summary>A sign-in was refused because no such account exists.</summary>
        public const string LoginFailedNoSuchAccount = "Login failed (no such account)";

        /// <summary>A sign-in was refused because the password does not match.</summary>
        public const string LoginFailedWrongPassword = "Login failed (wrong password)";

        /// <summary>A sign-in was refused because the account is blocked.</summary>
        public const string LoginFailedBlocked = "Login failed (account blocked)";

        /// <summary>A sign-in was refused because the account never confirmed itself.</summary>
        public const string LoginFailedNotRegistered = "Login failed (account not registered)";

        /// <summary>A sign-in was refused because the account belongs to another entrance.</summary>
        public const string LoginFailedWrongRole = "Login failed (wrong role)";

        /// <summary>A sign-in was refused because the network is not on the allow lists.</summary>
        public const string LoginFailedGuardRefused = "Login failed (network not allowed)";

        /// <summary>A password reset was requested from the public form.</summary>
        public const string PasswordResetRequested = "Password reset requested";

        /// <summary>A password reset request was refused because this installation cannot send mail.</summary>
        public const string PasswordResetRefusedNotConfigured =
            "Password reset refused (mail not configured)";

        /// <summary>A password reset request was refused because a supplied value broke a rule.</summary>
        public const string PasswordResetRefusedInvalidInput = "Password reset refused (invalid input)";

        /// <summary>A password reset request named a username and address that do not belong together.</summary>
        public const string PasswordResetRefusedUnknownPair = "Password reset refused (unknown pair)";

        /// <summary>A password reset was stored but its code could not be mailed.</summary>
        public const string PasswordResetFailedEmailDelivery =
            "Password reset failed (email delivery)";

        /// <summary>A mailed password reset code was accepted.</summary>
        public const string PasswordResetConfirmed = "Password reset confirmed";

        /// <summary>A password reset confirmation was refused because the code does not match.</summary>
        public const string PasswordResetConfirmFailedInvalidCode =
            "Password reset confirmation failed (invalid code)";

        /// <summary>A password reset confirmation was refused because the code window closed.</summary>
        public const string PasswordResetConfirmFailedExpiredCode =
            "Password reset confirmation failed (code expired)";

        /// <summary>The account asked to change its login name.</summary>
        public const string UsernameChangeRequested = "Username change requested";

        /// <summary>The account asked to change its email address.</summary>
        public const string EmailChangeRequested = "Email change requested";

        /// <summary>The account asked to change its password from the profile screen.</summary>
        public const string PasswordChangeRequested = "Password change requested";

        /// <summary>The account changed its login name.</summary>
        public const string UsernameChanged = "Username changed";

        /// <summary>The account changed its email address.</summary>
        public const string EmailChanged = "Email changed";

        /// <summary>The account changed its password from the profile screen.</summary>
        public const string PasswordChanged = "Password changed";

        /// <summary>The account confirmed a new login name.</summary>
        public const string UsernameChangeConfirmed = "Username change confirmed";

        /// <summary>The account confirmed a new email address.</summary>
        public const string EmailChangeConfirmed = "Email change confirmed";

        /// <summary>The account confirmed a new password.</summary>
        public const string PasswordChangeConfirmed = "Password change confirmed";

        /// <summary>A username change was refused because a supplied value broke a rule.</summary>
        public const string UsernameChangeRefusedInvalidInput =
            "Username change refused (invalid input)";

        /// <summary>An email change was refused because a supplied value broke a rule.</summary>
        public const string EmailChangeRefusedInvalidInput = "Email change refused (invalid input)";

        /// <summary>A password change was refused because the new password broke a rule.</summary>
        public const string PasswordChangeRefusedInvalidInput =
            "Password change refused (invalid input)";

        /// <summary>A username change was refused because the current password does not match.</summary>
        public const string UsernameChangeRefusedWrongPassword =
            "Username change refused (wrong password)";

        /// <summary>An email change was refused because the current password does not match.</summary>
        public const string EmailChangeRefusedWrongPassword =
            "Email change refused (wrong password)";

        /// <summary>A password change was refused because the current password does not match.</summary>
        public const string PasswordChangeRefusedWrongPassword =
            "Password change refused (wrong password)";

        /// <summary>A username change was refused because another account holds the name.</summary>
        public const string UsernameChangeRefusedAlreadyExists =
            "Username change refused (already exists)";

        /// <summary>An email change was refused because another account holds the address.</summary>
        public const string EmailChangeRefusedAlreadyExists =
            "Email change refused (already exists)";

        /// <summary>A username change was refused because the account is not active.</summary>
        public const string UsernameChangeRefusedAccountNotActive =
            "Username change refused (account not active)";

        /// <summary>An email change was refused because the account is not active.</summary>
        public const string EmailChangeRefusedAccountNotActive =
            "Email change refused (account not active)";

        /// <summary>A password change was refused because the account is not active.</summary>
        public const string PasswordChangeRefusedAccountNotActive =
            "Password change refused (account not active)";

        /// <summary>A username change was stored but its code could not be mailed.</summary>
        public const string UsernameChangeFailedEmailDelivery =
            "Username change failed (email delivery)";

        /// <summary>An email change was stored but its code could not be mailed.</summary>
        public const string EmailChangeFailedEmailDelivery = "Email change failed (email delivery)";

        /// <summary>A password change was stored but its code could not be mailed.</summary>
        public const string PasswordChangeFailedEmailDelivery =
            "Password change failed (email delivery)";

        /// <summary>A username change confirmation was refused for a wrong code.</summary>
        public const string UsernameChangeConfirmFailedInvalidCode =
            "Username change confirmation failed (invalid code)";

        /// <summary>An email change confirmation was refused for a wrong code.</summary>
        public const string EmailChangeConfirmFailedInvalidCode =
            "Email change confirmation failed (invalid code)";

        /// <summary>A password change confirmation was refused for a wrong code.</summary>
        public const string PasswordChangeConfirmFailedInvalidCode =
            "Password change confirmation failed (invalid code)";

        /// <summary>A username change confirmation was refused because the code window closed.</summary>
        public const string UsernameChangeConfirmFailedExpiredCode =
            "Username change confirmation failed (code expired)";

        /// <summary>An email change confirmation was refused because the code window closed.</summary>
        public const string EmailChangeConfirmFailedExpiredCode =
            "Email change confirmation failed (code expired)";

        /// <summary>A password change confirmation was refused because the code window closed.</summary>
        public const string PasswordChangeConfirmFailedExpiredCode =
            "Password change confirmation failed (code expired)";

        /// <summary>The account asked to change its login guard settings.</summary>
        public const string LoginGuardChangeRequested = "Login guard change requested";

        /// <summary>The account changed its login guard settings.</summary>
        public const string LoginGuardChanged = "Login guard changed";

        /// <summary>The account confirmed a staged change of its login guard settings.</summary>
        public const string LoginGuardChangeConfirmed = "Login guard change confirmed";

        /// <summary>A login guard change was refused because a supplied value broke a rule.</summary>
        public const string LoginGuardChangeRefusedInvalidInput =
            "Login guard change refused (invalid input)";

        /// <summary>A login guard change was refused because the current password does not match.</summary>
        public const string LoginGuardChangeRefusedWrongPassword =
            "Login guard change refused (wrong password)";

        /// <summary>A login guard change was refused because the account is not active.</summary>
        public const string LoginGuardChangeRefusedAccountNotActive =
            "Login guard change refused (account not active)";

        /// <summary>A login guard change was refused because it would lock the account out.</summary>
        public const string LoginGuardChangeRefusedWouldLockOut =
            "Login guard change refused (would lock out)";

        /// <summary>A login guard change was stored but its code could not be mailed.</summary>
        public const string LoginGuardChangeFailedEmailDelivery =
            "Login guard change failed (email delivery)";

        /// <summary>A login guard change confirmation was refused for a wrong code.</summary>
        public const string LoginGuardChangeConfirmFailedInvalidCode =
            "Login guard confirmation failed (invalid code)";

        /// <summary>A login guard change confirmation was refused because the code window closed.</summary>
        public const string LoginGuardChangeConfirmFailedExpiredCode =
            "Login guard confirmation failed (code expired)";

        /// <summary>The account ended its session.</summary>
        public const string LoggedOut = "Logged out";

        /// <summary>
        /// Builds a sentence naming the subject, and optionally a detail after it.
        /// </summary>
        /// <param name="phrase">
        /// Fixed phrase of the event, for example <c>Login failed (no such account)</c>. Must not
        /// be blank and must not contain <see cref="SubjectSeparator"/>.
        /// </param>
        /// <param name="subject">
        /// Identifier at write time - an account's login name, or the identifier an unknown
        /// account typed. Sanitized, capped and never empty.
        /// </param>
        /// <param name="detail">
        /// Optional detail, for example the two sides of a rename. Sanitized and shortened first
        /// when the sentence would not fit.
        /// </param>
        /// <returns>The sentence, at most <see cref="DescriptionMaxLength"/> characters.</returns>
        /// <exception cref="ArgumentException">The phrase is blank or contains the separator.</exception>
        public static string Describe(string phrase, string? subject, string? detail = null)
        {
            var fixedPhrase = ValidatePhrase(phrase);
            var prefix = fixedPhrase + SubjectSeparator;

            var cleanSubject = Sanitize(subject);
            var cleanDetail = Clean(detail);

            // A sentence with no detail is the phrase, the separator and the subject. A phrase so
            // long that it leaves the subject no room still has to produce a value the column
            // accepts, so it is shortened rather than refused - the caller named a phrase, not a
            // column width.
            var available = DescriptionMaxLength - prefix.Length;

            if (available <= 0)
            {
                return Truncate(prefix, DescriptionMaxLength);
            }

            if (cleanDetail.Length == 0)
            {
                return prefix + Truncate(cleanSubject, available);
            }

            // The detail is shortened first, which here means it is given only what the subject
            // does not need: the subject is the part the search box and the round trip rely on,
            // while the detail is an optional explanation. Only a sentence that cannot hold the
            // subject at all loses part of it, and then the detail is dropped entirely rather than
            // left as a dangling separator.
            var subjectRoom = available - DetailSeparator.Length;

            if (subjectRoom <= 0)
            {
                return prefix + Truncate(cleanSubject, available);
            }

            var keptSubject = Truncate(cleanSubject, subjectRoom);
            var detailRoom = available - keptSubject.Length - DetailSeparator.Length;

            if (detailRoom <= 0)
            {
                return prefix + Truncate(cleanSubject, available);
            }

            return prefix
                + keptSubject
                + DetailSeparator
                + Truncate(cleanDetail, detailRoom);
        }

        /// <summary>
        /// Builds a sentence for a rename: the subject, then the value before and the value after,
        /// separated by a rightwards arrow (U+2192).
        /// </summary>
        /// <param name="phrase">Fixed phrase of the event.</param>
        /// <param name="subject">Identifier at write time.</param>
        /// <param name="before">Value before the change.</param>
        /// <param name="after">Value after the change.</param>
        public static string Renamed(string phrase, string? subject, string? before, string? after)
            => Describe(phrase, subject, $"{Clean(before)} \u2192 {Clean(after)}");

        /// <summary>
        /// The identifier a sentence names: the text after the first <c>": "</c> up to the first
        /// <c>" | "</c>, or to the end.
        /// </summary>
        /// <param name="description">Sentence built by <see cref="Describe"/>.</param>
        /// <param name="subject">The subject, or null when the text carries none.</param>
        /// <returns>True when the text has the separator and therefore a subject.</returns>
        public static bool TryGetSubject(string? description, out string? subject)
        {
            subject = null;

            if (string.IsNullOrEmpty(description))
            {
                return false;
            }

            var start = description.IndexOf(SubjectSeparator, StringComparison.Ordinal);

            if (start < 0)
            {
                return false;
            }

            start += SubjectSeparator.Length;

            var end = description.IndexOf(DetailSeparator, start, StringComparison.Ordinal);

            subject = end < 0
                ? description[start..]
                : description[start..end];

            return true;
        }

        /// <summary>
        /// Makes a subject safe to store and to read back: control characters and the detail
        /// separator are removed, the text is trimmed and capped, and an empty result is named
        /// rather than left blank.
        /// </summary>
        /// <param name="value">Subject as the caller knows it, possibly null.</param>
        public static string Sanitize(string? value)
        {
            var clean = Clean(value);

            return clean.Length == 0
                ? EmptySubject
                : Truncate(clean, SubjectMaxLength);
        }

        /// <summary>
        /// Strips control characters and the separators a sentence could not be read back through,
        /// then trims.
        /// </summary>
        /// <remarks>
        /// A pipe or a control character inside user text would let that text forge the shape of
        /// the sentence: a typed login name of <c>alice | admin</c> must not read as a subject plus
        /// a detail. Removing both is the only way the inverse stays exact.
        /// </remarks>
        private static string Clean(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                if (char.IsControl(character) || character == '|')
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }

        /// <summary>Cuts a value to the width the sentence still has for it.</summary>
        private static string Truncate(string value, int maxLength)
        {
            if (maxLength <= 0)
            {
                return string.Empty;
            }

            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }

        /// <summary>
        /// Checks the one rule a fixed phrase has: it is not blank and does not contain the
        /// separator, so the sentence's first separator is always the subject's.
        /// </summary>
        private static string ValidatePhrase(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
            {
                throw new ArgumentException(
                    "A description needs a fixed phrase to name the event.",
                    nameof(phrase));
            }

            if (phrase.Contains(SubjectSeparator, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A fixed phrase must not contain the ': ' separator: the sentence's first " +
                    "separator belongs to the subject, and TryGetSubject depends on it.",
                    nameof(phrase));
            }

            return phrase.Trim();
        }
    }
}
