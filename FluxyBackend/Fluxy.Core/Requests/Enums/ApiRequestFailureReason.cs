namespace Fluxy.Core.Requests.Enums
{
    /// <summary>
    /// Why a public-API request was refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The numeric values are schema and append-only.</b> They are stored as a
    /// <c>smallint</c> and are already written into rows, so a member is added at the end and
    /// never renumbered; renumbering one would reinterpret every stored refusal as a different
    /// reason without a single query being made.
    /// </para>
    /// <para>
    /// <b>The wire vocabulary is the snake_case name, not the member name.</b> A query string and
    /// a response carry <c>unknown_id</c>, while the database stores the member as a
    /// <c>smallint</c>; <see cref="Fluxy.Core.Requests.Utils.ApiRequestFailureReasons"/> is the one
    /// place the two are tied together.
    /// </para>
    /// </remarks>
    public enum ApiRequestFailureReason
    {
        /// <summary>The request named an identifier this installation does not know.</summary>
        UnknownId = 0,

        /// <summary>The request came from an address the owner's login guard does not allow.</summary>
        GeoIpViolation = 1,

        /// <summary>The request named no valid access parameter.</summary>
        AuthFailed = 2,

        /// <summary>The subscription already held as many connections as it allows.</summary>
        TooManyConnections = 3,

        /// <summary>The subscription behind the request had expired.</summary>
        SubscriptionExpired = 4,

        /// <summary>The account behind the request was blocked.</summary>
        UserBlocked = 5
    }
}
