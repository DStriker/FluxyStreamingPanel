using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a connection log query into the choices it names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rules live in Core and this is the transport's name for them.</b>
    /// <see cref="ConnectionLogQuery"/> holds the mapping and the refusals, because the unit test
    /// project references Core and Application only - it cannot see this assembly - and every rule
    /// about a parameter that names nothing has to be pinnable without a running host. This class
    /// exists so the controller reads as a controller: it names the parse it wants and the
    /// response spelling it draws, and it does not re-state either.
    /// </para>
    /// <para>
    /// Matched member by member rather than with enum binding: binding takes a member name spelled
    /// in full and would refuse the conventional lower-case one, before the action could run at
    /// all. Comparison happens after lowering, so <c>kind=AUDIO</c> is the same request as
    /// <c>kind=audio</c>.
    /// </para>
    /// </remarks>
    internal static class ConnectionLogTextParser
    {
        /// <summary>
        /// Resolves every filter and order of the query, or reports each parameter that named
        /// nothing.
        /// </summary>
        /// <param name="kind">Medium as typed; <c>video</c> when absent.</param>
        /// <param name="state">State as typed; <c>all</c> when absent.</param>
        /// <param name="subscriptionId">Subscription to keep, as typed, or null.</param>
        /// <param name="ownerId">Owner to keep, as typed, or null.</param>
        /// <param name="streamId">Stream to keep, as typed, or null.</param>
        /// <param name="ip">Address to keep exactly, or null.</param>
        /// <param name="country">Country as typed, any case, or null.</param>
        /// <param name="isp">Provider to keep exactly, or null.</param>
        /// <param name="userAgent">User agent to keep exactly, or null.</param>
        /// <param name="startedFrom">Earliest start instant, inclusive, as typed, or null.</param>
        /// <param name="startedTo">Latest start instant, inclusive, as typed, or null.</param>
        /// <param name="sortBy">Column as typed; <c>startedAt</c> when absent.</param>
        /// <param name="sortOrder">Direction as typed; <c>desc</c> when absent.</param>
        /// <param name="errors">Rejected parameters, keyed by the query-string name.</param>
        public static ConnectionLogQuery? ParseQuery(
            string? kind,
            string? state,
            string? subscriptionId,
            string? ownerId,
            string? streamId,
            string? ip,
            string? country,
            string? isp,
            string? userAgent,
            string? startedFrom,
            string? startedTo,
            string? sortBy,
            string? sortOrder,
            out Dictionary<string, string[]> errors)
            => ConnectionLogQuery.TryParse(
                kind,
                state,
                subscriptionId,
                ownerId,
                streamId,
                ip,
                country,
                isp,
                userAgent,
                startedFrom,
                startedTo,
                sortBy,
                sortOrder,
                out errors);

        /// <summary>The name a client reads a medium by: the spelling the filter accepts.</summary>
        /// <param name="kind">Medium to name.</param>
        public static string NameOf(ConnectionStreamKind kind)
            => ConnectionLogQueryText.NameOf(kind);
    }
}
