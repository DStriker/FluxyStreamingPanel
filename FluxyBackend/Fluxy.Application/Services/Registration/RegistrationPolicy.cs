using Fluxy.Core.Models.Users;
using System.Text.RegularExpressions;

namespace Fluxy.Application.Services.Registration
{
    /// <summary>
    /// The rules a set of registration values has to satisfy that are more than a length.
    /// </summary>
    /// <remarks>
    /// The lengths themselves live in <see cref="RegistrationLimits"/>, in Core, because the API
    /// contracts need them before any service is reached; what stays here is the shape of an
    /// address and the complexity of a password, which only the services apply. Both halves are
    /// the authority behind what a form advertises: a client is free to ignore whatever the
    /// server says, so a service checks the values again rather than trusting a body that arrived
    /// with every attribute satisfied.
    /// </remarks>
    public static partial class RegistrationPolicy
    {
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
            => email.Length <= RegistrationLimits.EmailMaxLength && EmailShape().IsMatch(email);

        /// <summary>
        /// Whether <paramref name="password"/> meets the complexity rules: at least one lower
        /// case letter, one upper case letter and one digit.
        /// </summary>
        /// <param name="password">Password to check, in clear text.</param>
        public static bool IsPasswordAcceptable(string password)
        {
            if (password.Length is < RegistrationLimits.PasswordMinLength
                or > RegistrationLimits.PasswordMaxLength)
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
