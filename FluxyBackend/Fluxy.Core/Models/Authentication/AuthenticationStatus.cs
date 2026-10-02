namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// How a sign-in attempt ended.
    /// </summary>
    /// <remarks>
    /// The set has exactly two members, and that is a security decision rather than a
    /// simplification. Everything that can go wrong with a sign-in - no such account, wrong
    /// password, an account that never confirmed its address, a blocked account, and an account
    /// that exists but belongs to a different role than the form accepts - is reported as
    /// <see cref="InvalidCredentials"/>. A wider set would tell a caller which of those it hit,
    /// and the form would become a way of finding out who has an account here.
    ///
    /// The text a client shows, the status code it gets and where it is sent next are all
    /// decided in the API layer, so nothing here leaks that distinction either.
    /// </remarks>
    public enum AuthenticationStatus
    {
        /// <summary>The credentials matched and a token pair has been issued.</summary>
        Authenticated = 0,

        /// <summary>
        /// The request was refused. Every reason a sign-in can fail produces this one value.
        /// </summary>
        InvalidCredentials = 1
    }
}
