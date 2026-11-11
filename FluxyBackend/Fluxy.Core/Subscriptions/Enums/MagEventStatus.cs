namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// Whether the device confirmed the operation it was sent.
    /// </summary>
    /// <remarks>
    /// The numbers are schema, written as <c>smallint</c>. A sent event starts
    /// <see cref="NotConfirmed"/>; the confirmation path that will flip it is the future
    /// streaming subsystem's, not this phase's.
    /// </remarks>
    public enum MagEventStatus
    {
        /// <summary>Sent and not yet acknowledged by the device.</summary>
        NotConfirmed = 0,

        /// <summary>The device acknowledged the operation.</summary>
        Confirmed = 1
    }
}
