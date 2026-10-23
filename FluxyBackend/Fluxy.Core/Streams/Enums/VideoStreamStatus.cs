namespace Fluxy.Core.Streams.Enums
{
    /// <summary>Whether a stream is active and broadcasting, or disabled and kept.</summary>
    /// <remarks>
    /// <para>
    /// Two states of one stream, not two levels of anything, so nothing anywhere may compare
    /// two of them with <c>&gt;</c> or <c>&lt;</c> - a stream is either active or it is not,
    /// and "more disabled" is not a thing.
    /// </para>
    /// <para>
    /// The numeric values are fixed for the reason <see cref="VideoStreamType"/>'s are: they
    /// are what the <c>status</c> column stores. A new member is added at the end.
    /// </para>
    /// <para>
    /// The create and edit forms choose it, and the "start", "stop" and "restart" actions
    /// change it. <c>ActiveSource</c> is a separate fact - which source is being used right
    /// now - and it is read-only from the interface because only the process that actually
    /// relays the stream knows the answer.
    /// </para>
    /// </remarks>
    public enum VideoStreamStatus
    {
        /// <summary>The stream is disabled: kept, but not broadcasting.</summary>
        Stopped = 0,

        /// <summary>The stream is active and broadcasting.</summary>
        Running = 1
    }
}
