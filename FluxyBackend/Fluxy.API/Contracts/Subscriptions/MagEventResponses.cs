namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// The stable codes the MAG event endpoints answer with.
    /// </summary>
    /// <remarks>
    /// The spelling follows the other journals (<c>stream_problems_cleared</c>), so a client that
    /// knows one knows this one. The send has a code of its own because a 201 is a distinct
    /// outcome a client branches on, not a generic success.
    /// </remarks>
    public static class MagEventResponses
    {
        /// <summary>Code reported once an event has been recorded and sent.</summary>
        public const string SentCode = "mag_event_sent";

        /// <summary>Code reported once every row of the journal has been deleted.</summary>
        public const string ClearedCode = "mag_events_cleared";

        /// <summary>Code reported when the subscription has no MAG device to send to.</summary>
        public const string MagDeviceMissingCode = "mag_device_missing";
    }
}
