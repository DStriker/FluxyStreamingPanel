using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.Application.Subscriptions.Utils
{
    /// <summary>
    /// The rules every MAG event send is judged by, and the resolved values the row is stored
    /// with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The defaults live here and nowhere else.</b> The contract forbids <c>HasDefaultValue</c>
    /// on the table, so a column the application forgot is a mistake the database surfaces rather
    /// than masks. Every flag therefore arrives as a nullable on the request and leaves this type
    /// resolved: all false except <c>additionalServicesOn</c>, which is true.
    /// </para>
    /// <para>
    /// <b>The message's meaning belongs to the operation.</b> A message is the text the device
    /// shows for <see cref="MagEventType.SendMessage"/>, a stream identifier for
    /// <see cref="MagEventType.PlayChannel"/>, and nothing at all for the other four - where a
    /// payload sent anyway is dropped rather than stored. A <c>playChannel</c> identifier is
    /// checked for shape only: whether a video stream carries it is a fact about another table and
    /// deliberately not a condition of sending the operation.
    /// </para>
    /// <para>
    /// <b>The MAG address check is separate.</b> Whether the subscription has a device is a fact
    /// about the row, not about the body, so it is answered by <see cref="IsMagDeviceMissing"/>
    /// and refused by the service with its own code rather than through the validation map.
    /// </para>
    /// <para>
    /// The keys of <c>errors</c> are the C# names of the fields, the convention every other service
    /// here follows; the transport turns them into the camelCase names a form reads.
    /// </para>
    /// </remarks>
    public static class MagEventRules
    {
        /// <summary>The operations a request may name, in their wire order.</summary>
        private static readonly MagEventType[] KnownEvents =
        [
            MagEventType.PlayChannel,
            MagEventType.ClosePortal,
            MagEventType.ReloadPortal,
            MagEventType.RebootDevice,
            MagEventType.SendMessage,
            MagEventType.ResetStbLock
        ];

        /// <summary>The sentence a message the operation requires but does not carry gets.</summary>
        public static readonly string[] MessageRequiredRule =
        [
            "This operation requires a message."
        ];

        /// <summary>The sentence a message longer than its column gets.</summary>
        public static readonly string[] MessageTooLongRule =
        [
            "The message must be at most " + MagEventListLimits.MessageMaxLength + " characters."
        ];

        /// <summary>The sentence a <c>playChannel</c> message that is not a uuid gets.</summary>
        public static readonly string[] ChannelRule =
        [
            "A playChannel event requires the identifier of the channel as a uuid."
        ];

        /// <summary>The sentence a send that names no subscription gets.</summary>
        public static readonly string[] SubscriptionRule =
        [
            "The subscription must be the identifier of a subscription, as a uuid."
        ];

        /// <summary>The sentence an operation that names no member gets.</summary>
        public static readonly string[] EventRule =
        [
            "The event must be one of: playChannel, closePortal, reloadPortal, rebootDevice, " +
                "sendMessage, resetStbLock."
        ];

        /// <summary>The sentence a subscription with no MAG device gets.</summary>
        public static readonly string[] MagDeviceMissingRule =
        [
            "The subscription has no MAG device, so there is nothing to send the event to."
        ];

        /// <summary>
        /// Whether a subscription's stored MAG address means there is no device to send to.
        /// </summary>
        /// <param name="magMac">Stored address, or null when the subscription has none.</param>
        public static bool IsMagDeviceMissing(string? magMac) => string.IsNullOrWhiteSpace(magMac);

        /// <summary>
        /// Applies rules 1 to 4 to one send and answers the row values it resolves to, or the
        /// fields it refused.
        /// </summary>
        /// <param name="request">What the caller asked to send.</param>
        /// <param name="errors">Rejected fields, keyed by the C# property name.</param>
        /// <returns>The resolved row values, or null when any field was refused.</returns>
        public static MagEventWrite? TryResolve(
            NewMagEvent request,
            out Dictionary<string, string[]> errors)
        {
            errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (request.SubscriptionId == Guid.Empty)
            {
                errors["SubscriptionId"] = SubscriptionRule;
            }

            if (!KnownEvents.Contains(request.EventType))
            {
                errors["Event"] = EventRule;
            }

            var message = ResolveMessage(request, errors);

            if (errors.Count > 0)
            {
                return null;
            }

            // Rule 1: a message the device shows must be confirmed, whatever the body said.
            // Everything else keeps the flag the caller named, or false when it named none.
            var needConfirm = request.EventType is MagEventType.SendMessage
                || request.NeedConfirm is true;

            return new MagEventWrite
            {
                SubscriptionId = request.SubscriptionId,
                EventType = request.EventType,
                Message = message,
                NeedConfirm = needConfirm,
                RebootAfterOk = request.RebootAfterOk ?? false,
                AutoHideTimeout = request.AutoHideTimeout ?? false,

                // The one flag whose default is true, and the reason every flag is resolved in
                // the service rather than by a database default.
                AdditionalServicesOn = request.AdditionalServicesOn ?? true,
                Anec = request.Anec ?? false,
                Vclub = request.Vclub ?? false
            };
        }

        /// <summary>
        /// Applies the message rules of one operation: required for two kinds, dropped for four.
        /// </summary>
        private static string? ResolveMessage(
            NewMagEvent request,
            Dictionary<string, string[]> errors)
        {
            var trimmed = request.Message?.Trim();

            if (request.EventType is MagEventType.ClosePortal
                or MagEventType.ReloadPortal
                or MagEventType.RebootDevice
                or MagEventType.ResetStbLock)
            {
                // Dropped rather than refused: these operations carry no payload, and a value sent
                // anyway is not a request the row should honour.
                return null;
            }

            if (string.IsNullOrEmpty(trimmed))
            {
                errors["Message"] = MessageRequiredRule;

                return null;
            }

            if (trimmed.Length > MagEventListLimits.MessageMaxLength)
            {
                errors["Message"] = MessageTooLongRule;

                return null;
            }

            if (request.EventType is MagEventType.PlayChannel
                && !Guid.TryParse(trimmed, out _))
            {
                errors["Message"] = ChannelRule;

                return null;
            }

            return trimmed;
        }
    }
}
