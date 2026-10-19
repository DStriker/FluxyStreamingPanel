namespace Fluxy.Core.Authentication.Enums
{
    /// <summary>
    /// How an attempt to end somebody else's session ended, as the service sees it.
    /// </summary>
    /// <remarks>
    /// The transport turns these into three different answers, and the split is the whole point
    /// of naming them rather than returning a boolean. <see cref="NotFound"/> is deliberately one
    /// word for four situations - no such session, one that was already ended, one that quietly
    /// expired, and one belonging to an account this operator may not read - because telling
    /// them apart would make the endpoint a probe for other accounts' rows, and the id is a
    /// guessable value.
    /// </remarks>
    public enum AdminSessionRevokeOutcome
    {
        /// <summary>
        /// A live session of an account this operator may read was ended.
        /// </summary>
        Revoked = 1,

        /// <summary>
        /// Nothing was ended, and there is nothing to say about why - see the type's remarks.
        /// </summary>
        NotFound = 2,

        /// <summary>
        /// The caller named the very session the request is authenticated with. That is a fact
        /// about the request rather than about a row, so it is settled before any lookup and
        /// gets a sentence of its own.
        /// </summary>
        CannotRevokeCurrent = 3
    }
}
