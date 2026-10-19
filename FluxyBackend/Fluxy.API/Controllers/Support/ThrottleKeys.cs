using Fluxy.Core.Users.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// The keys the attempt windows are counted under, one method per window.
    /// </summary>
    /// <remarks>
    /// The strings are the contract with Redis: a key that changes spelling silently starts a
    /// new window, so an attacker's spent permits would come back the moment somebody tidied a
    /// name. They live here so that the counter is named in one place per window and the
    /// controller that spends it only says which window it means. The store prefixes every key
    /// with its own namespace, so what is written here is the address of the window inside it.
    /// </remarks>
    internal static class ThrottleKeys
    {
        /// <summary>
        /// Sign-ins, keyed by role and address. No username is part of the key: including one
        /// would let a caller lock an account out instead of only slowing itself down.
        /// </summary>
        public static string Login(UserRole role, string clientAddress)
            => $"login:{role}:{clientAddress}";

        /// <summary>Refreshes, keyed by address.</summary>
        public static string Refresh(string clientAddress)
            => $"refresh:{clientAddress}";

        /// <summary>Registrations, keyed by address.</summary>
        public static string Register(string clientAddress)
            => $"register:{clientAddress}";

        /// <summary>Registration confirmations, keyed by address.</summary>
        public static string Confirm(string clientAddress)
            => $"confirm:{clientAddress}";

        /// <summary>Password reset requests, keyed by address.</summary>
        public static string PasswordResetRequest(string clientAddress)
            => $"password_reset:{clientAddress}";

        /// <summary>Password reset confirmations, keyed by address.</summary>
        public static string PasswordResetConfirm(string clientAddress)
            => $"password_reset_confirm:{clientAddress}";

        /// <summary>
        /// Profile changes, keyed by address. The four changes that touch what the account is
        /// share this window; the time zone has one of its own below.
        /// </summary>
        public static string ProfileChange(string clientAddress)
            => $"login:profile:{clientAddress}";

        /// <summary>
        /// Time zone saves, keyed by address. A window of its own: the setting saves on
        /// selection and would otherwise drain the permits a password change counts on.
        /// </summary>
        public static string ProfileTimeZone(string clientAddress)
            => $"profile_timezone:{clientAddress}";

        /// <summary>Profile change confirmations, keyed by address.</summary>
        public static string ProfileConfirm(string clientAddress)
            => $"profile_confirm:{clientAddress}";
    }
}
