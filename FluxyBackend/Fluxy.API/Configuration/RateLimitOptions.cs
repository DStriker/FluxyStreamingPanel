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
    }
}