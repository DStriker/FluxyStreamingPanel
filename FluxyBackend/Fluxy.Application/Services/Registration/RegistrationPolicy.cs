using System.Text.RegularExpressions;

namespace Fluxy.Application.Services.Registration
{
    /// <summary>
    /// The rules a set of registration values has to satisfy, in one place.
    /// </summary>
    /// <remarks>
    /// Both the API contracts and <c>RegistrationService</c> read these constants. The contracts
    /// turn them into cheap length checks that reject a malformed request before it reaches a
    /// service, and the service applies the same numbers as the authority, because a client is
    /// free to ignore whatever the server advertises. Keeping one definition stops the two from
    /// drifting into different limits.
    /// </remarks>
    public static partial class RegistrationPolicy
    {
        /// <summary>Shortest accepted login name. The column allows 20 characters, the form wants fewer.</summary>
        public const int UsernameMinLength = 5;

        /// <summary>Longest accepted login name, matching the column width.</summary>
        public const int UsernameMaxLength = 20;

        /// <summary>Shortest accepted password.</summary>
        public const int PasswordMinLength = 8;

        /// <summary>Longest accepted password, well above the column width, which stores only a hash.</summary>
        public const int PasswordMaxLength = 100;

        /// <summary>Length of the longest address SMTP accepts, matching the column width.</summary>
        public const int EmailMaxLength = 254;

        /// <summary>
        /// Deliberately narrower than the RFC grammar. It accepts the shapes a real address has
        /// and rejects the ones that would only pass a strict parser to fail later at the mail
        /// server, where the visitor finds out nothing at all.
        /// </summary>
        [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
        private static partial Regex EmailShape();

        /// <summary>
        /// Whether <paramref name="email"/> has the shape of a deliverable address. It does not
        /// prove that the address exists - only a mail server can do that.
        /// </summary>
        /// <param name="email">Address to check, already normalized.</param>
        public static bool IsEmailShapeValid(string email)
            => email.Length <= EmailMaxLength && EmailShape().IsMatch(email);

        /// <summary>
        /// Whether <paramref name="password"/> meets the complexity rules: at least one lower
        /// case letter, one upper case letter and one digit.
        /// </summary>
        /// <param name="password">Password to check, in clear text.</param>
        public static bool IsPasswordAcceptable(string password)
        {
            if (password.Length is < PasswordMinLength or > PasswordMaxLength)
            {
                return false;
            }

            var hasLower = false;
            var hasUpper = false;
            var hasDigit = false;

            foreach (var character in password)
            {
                if (char.IsLower(character))
                {
                    hasLower = true;
                }
                else if (char.IsUpper(character))
                {
                    hasUpper = true;
                }
                else if (char.IsDigit(character))
                {
                    hasDigit = true;
                }

                if (hasLower && hasUpper && hasDigit)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
