namespace Fluxy.API.Configuration
{
    /// <summary>
    /// How often one client may reach the endpoints that cost the server something. Bound from
    /// the <c>RateLimit</c> configuration section.
    /// </summary>
    /// <remarks>
    /// These are the defaults, and they are deliberately strict. They describe what the
    /// endpoints do on purpose rather than what protects a machine:
    ///
    /// A registration hashes a password, which costs more than everything else an unauthenticated
    /// request can make it do, so a single stored account per client per window is enough for
    /// real use and turns the endpoint into a poor way to burn someone's CPU. A rejection is
    /// free, so a mistyped form can be resubmitted as often as the visitor likes.
    ///
    /// A confirmation compares a code against a BCrypt hash, which costs as much as producing one
    /// would, and a six digit code is a small enough space to guess at. Ten attempts per window
    /// is generous for a person reading a number out of an email and still far below the number
    /// that would be needed to run through the code.
    /// </remarks>
    public sealed class RateLimitOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "RateLimit";

        /// <summary>Registrations a client may store in one window.</summary>
        public int RegisterLimit { get; set; } = 1;

        /// <summary>Length of the registration window.</summary>
        public TimeSpan RegisterWindow { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>Confirmation attempts a client may spend in one window.</summary>
        public int ConfirmLimit { get; set; } = 10;

        /// <summary>Length of the confirmation window.</summary>
        public TimeSpan ConfirmWindow { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>Sign-in attempts a client may spend in one window.</summary>
        /// <remarks>
        /// Five per window, and the window is keyed by role, address and name together - see
        /// <c>AuthenticationController</c> for why all three are in the key. The number is
        /// chosen by what a legitimate person does: a person who mistypes a password tries it
        /// again, so the limit has to leave room for two or three corrections without punishing
        /// someone who is simply bad at typing, while staying far below the count that would
        /// make an online guess worth running.
        ///
        /// A successful sign-in clears the window rather than leaving it to expire, because
        /// someone who has just proved they know the password is no longer guessing.
        /// </remarks>
        public int LoginLimit { get; set; } = 5;

        /// <summary>Length of the sign-in window.</summary>
        public TimeSpan LoginWindow { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>Token refreshes a client may spend in one window.</summary>
        /// <remarks>
        /// Much higher than the sign-in limit, and the reason is who makes the requests. A
        /// refresh is made by the application the user is already signed in to, roughly once
        /// every few minutes, with a token that is valid and spent only if it is accepted. A
        /// limit here is a guard against a stolen refresh token being cycled, not against a
        /// person doing anything, so it must not be low enough to interrupt ordinary use.
        /// </remarks>
        public int RefreshLimit { get; set; } = 30;

        /// <summary>Length of the refresh window.</summary>
        public TimeSpan RefreshWindow { get; set; } = TimeSpan.FromMinutes(15);
    }
}