namespace Fluxy.API.Contracts.Subscriptions
{
    /// <summary>
    /// What the "send event" form sends to ask a MAG device to perform an operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No data annotations on purpose: framework validation runs before the action, so a body that
    /// failed one would never reach the antiforgery check. Every field is checked by
    /// <c>MagEventRules</c> after the CSRF pair has been accepted.
    /// </para>
    /// <para>
    /// <b>The flags are nullable because "not sent" and "sent false" are different requests.</b>
    /// The service resolves every unset flag, so the defaults live in one place and the database
    /// invents nothing.
    /// </para>
    /// </remarks>
    public sealed record AdminMagEventSendRequest
    {
        /// <summary>Subscription whose device is addressed, as a uuid.</summary>
        public string? SubscriptionId { get; init; }

        /// <summary>
        /// The operation, as one of <c>playChannel</c>, <c>closePortal</c>, <c>reloadPortal</c>,
        /// <c>rebootDevice</c>, <c>sendMessage</c>, <c>resetStbLock</c>.
        /// </summary>
        public string? Event { get; init; }

        /// <summary>
        /// The operation's payload: the text a <c>sendMessage</c> shows, the stream identifier a
        /// <c>playChannel</c> opens, and nothing for the other four.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>Whether the device must confirm the operation.</summary>
        public bool? NeedConfirm { get; init; }

        /// <summary>Whether the device should reboot after a successful operation.</summary>
        public bool? RebootAfterOk { get; init; }

        /// <summary>Whether the message hides itself after a timeout.</summary>
        public bool? AutoHideTimeout { get; init; }

        /// <summary>Whether other services are switched on with the operation.</summary>
        public bool? AdditionalServicesOn { get; init; }

        /// <summary>ANEC flag.</summary>
        public bool? Anec { get; init; }

        /// <summary>VCLUB flag.</summary>
        public bool? Vclub { get; init; }
    }
}
