using Fluxy.Core.Vod.Enums;

namespace Fluxy.API.Controllers.Support
{
    /// <summary>
    /// Turns the text of an on-demand field into the member it names, for every endpoint that
    /// reads one.
    /// </summary>
    /// <remarks>
    /// Matched member by member rather than with enum binding: binding takes a member name
    /// spelled in full and would refuse the conventional lower-case one, before the
    /// antiforgery check could even run. Comparison happens after lowering, so <c>Relay</c> is
    /// the same request as <c>relay</c>.
    /// </remarks>
    internal static class VodTextParser
    {
        /// <summary>Turns text into the delivery kind it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string, a JSON body or a form field.</param>
        public static VodType? ParseTypeOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "relay" => VodType.Relay,
                "redirect" => VodType.Redirect,
                _ => null
            };

        /// <summary>Turns text into the state it names, or null when it names none.</summary>
        /// <param name="value">Value from a query string, a JSON body or a form field.</param>
        public static VodStatus? ParseStatusOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "active" => VodStatus.Active,
                "inactive" => VodStatus.Inactive,
                _ => null
            };

        /// <summary>Turns text into the container it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static VodTranscodeFormat? ParseFormatOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "mp4" => VodTranscodeFormat.Mp4,
                "avi" => VodTranscodeFormat.Avi,
                "mkv" => VodTranscodeFormat.Mkv,
                "mpg" => VodTranscodeFormat.Mpg,
                "flv" => VodTranscodeFormat.Flv,
                _ => null
            };

        /// <summary>Turns text into the bulk operation it names, or null when it names none.</summary>
        /// <param name="value">Value from a JSON body.</param>
        public static BulkVodAction? ParseBulkActionOrNull(string? value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "change-status" => BulkVodAction.ChangeStatus,
                "move-category" => BulkVodAction.MoveCategory,
                "change-type" => BulkVodAction.ChangeType,
                "delete" => BulkVodAction.Delete,
                _ => null
            };
    }
}
