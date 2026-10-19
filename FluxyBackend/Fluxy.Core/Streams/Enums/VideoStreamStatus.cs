namespace Fluxy.Core.Streams.Enums
{
    /// <summary>Whether a stream is currently running or has been stopped.</summary>
    /// <remarks>
    /// <para>
    /// Two states of one stream, not two levels of anything, so nothing anywhere may compare
    /// two of them with <c>&gt;</c> or <c>&lt;</c> - a stream is either running or it is not,
    /// and "more stopped" is not a thing.
    /// </para>
    /// <para>
    /// The numeric values are fixed for the reason <see cref="VideoStreamType"/>'s are: they
    /// are what the <c>status</c> column stores. A new member is added at the end.
    /// </para>
    /// <para>
    /// The status is what the "start" and "stop" actions on a stream change, and nothing else
    /// writes it. <c>ActiveSource</c> is a separate fact - which source is being used right now
    /// - and it is read-only from the interface because only the process that actually relays
    /// the stream knows the answer.
    /// </para>
    /// </remarks>
    public enum VideoStreamStatus
    {
        /// <summary>The stream is not broadcasting.</summary>
        Stopped = 0,

        /// <summary>The stream is broadcasting.</summary>
        Running = 1
    }
}
