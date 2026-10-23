namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// Whether an audio stream is active or disabled.
    /// </summary>
    /// <remarks>
    /// <b>This enum is not ordered.</b> The two members are states of one stream, not levels of
    /// access; a check is for the exact member and never a comparison. The numeric values are
    /// stored as <c>smallint</c> and are therefore part of the schema.
    /// </remarks>
    public enum AudioStreamStatus
    {
        /// <summary>Disabled: the row is kept, but nothing is relayed.</summary>
        Stopped = 0,

        /// <summary>Active and relaying.</summary>
        Running = 1
    }
}
