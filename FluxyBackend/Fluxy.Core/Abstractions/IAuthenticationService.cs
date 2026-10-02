using Fluxy.Core.Models.Authentication;

namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// Turns a username and a password into a signed-in account and a pair of tokens.
    /// </summary>
    /// <remarks>
    /// The service owns the rules - which account the name belongs to, whether it may sign in,
    /// whether it is the audience the form is for - and knows nothing about HTTP. A caller gets
    /// one of two answers and cannot tell from it why it was refused, which is what keeps the
    /// sign-in form from being usable to find out which accounts exist.
    /// </remarks>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Verifies submitted credentials and issues tokens when they are good.
        /// </summary>
        /// <param name="credentials">
        /// What was typed, together with the role the endpoint accepts. The values arrive
        /// unnormalized, exactly as the service expects to receive them.
        /// </param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// The account and its tokens, or a single refusal covering every reason a sign-in can
        /// fail.
        /// </returns>
        Task<AuthenticationOutcome> AuthenticateAsync(
            NewCredentials credentials,
            CancellationToken cancellationToken = default);
    }
}
