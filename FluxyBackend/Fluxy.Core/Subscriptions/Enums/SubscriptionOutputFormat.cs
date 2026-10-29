namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The formats a subscription may be delivered in, as a flag so "is HLS delivered" is one
    /// bitwise test over the value the row carries.
    /// </summary>
    /// <remarks>
    /// Two members and no more: MPEG-TS and HLS are the two shapes the relay speaks, and a
    /// subscription with neither is refused rather than stored - a package that cannot be
    /// played is not a package. The numeric values are part of the schema (<c>smallint</c> in
    /// <c>subscriptions.output_formats</c>), so a member added later takes a value nobody has
    /// used yet rather than renumbering these.
    /// </remarks>
    [Flags]
    public enum SubscriptionOutputFormat
    {
        /// <summary>Nothing selected. Refused by the service, and the empty set a body may name.</summary>
        None = 0,

        /// <summary>MPEG-TS, the transport stream a set-top box and a desktop player both read.</summary>
        MpegTs = 1,

        /// <summary>HLS, the segmented stream a browser plays.</summary>
        Hls = 2
    }
}
