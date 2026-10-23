using Fluxy.Core.Streams.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of a stream field into the member it names, for every endpoint that reads
    /// one.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with an ordered value, because neither enum is
    /// ordered: six kinds of stream are not six levels of anything, and two states of one stream
    /// are not two levels either. Comparison happens after lowering, so <c>Permanent</c> is the
    /// same request as <c>permanent</c>. Enum binding is deliberately not used, for the reason
    /// the accounts controller gives: it takes a member name spelled in full and would refuse
    /// the conventional lower-case one.
    /// </remarks>
    internal static class VideoStreamTextParser
    {
        /// <summary>Turns text into the kind of stream it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string, a JSON body or a form field.</param>
        public static VideoStreamType? ParseTypeOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "permanent" => VideoStreamType.Permanent,
                "ondemand" => VideoStreamType.OnDemand,
                "redirect" => VideoStreamType.Redirect,
                "onlineseries" => VideoStreamType.OnlineSeries,
                "vodplaylist" => VideoStreamType.VodPlaylist,
                "fileplaylist" => VideoStreamType.FilePlaylist,
                _ => null
            };

        /// <summary>Turns text into the state it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string, a JSON body or a form field.</param>
        public static VideoStreamStatus? ParseStatusOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "stopped" => VideoStreamStatus.Stopped,
                "running" => VideoStreamStatus.Running,
                _ => null
            };
    }
}
