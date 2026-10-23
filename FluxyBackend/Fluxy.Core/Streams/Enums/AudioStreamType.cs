namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// The kind of an audio stream: the two ways a stored address list is played.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="OnDemand"/> is a stream that starts when a listener asks for it and stops
    /// when nobody is listening; <see cref="Redirect"/> hands the listener the address and lets
    /// the listener's player fetch it directly. Both relay from the same ordered source list, so
    /// the type decides <i>how</i> the stream runs, never <i>what</i> it carries.
    /// </para>
    /// <para>
    /// The values are stored as <c>smallint</c>, so they are part of the schema: a new member
    /// takes a number nobody has used, and the existing numbers are never renumbered.
    /// </para>
    /// </remarks>
    public enum AudioStreamType
    {
        /// <summary>Started on demand and stopped when the last listener leaves.</summary>
        OnDemand = 1,

        /// <summary>Handed to the listener's player as a redirect.</summary>
        Redirect = 2
    }
}
