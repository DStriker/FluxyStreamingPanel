using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The rules a MAG event send is judged by: which operation carries a message, what a message
    /// must be, which flag the server forces, and the value every unset flag takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One test per rule per kind, because the rules disagree with each other on purpose: a
    /// message is required for two operations, dropped for four, and a <c>playChannel</c> message
    /// must parse as a uuid without the rule ever asking whether a video stream carries it.
    /// </para>
    /// <para>
    /// The flag defaults are pinned here rather than trusted to the data access layer: the
    /// contract forbids <c>HasDefaultValue</c>, so the only place an unset flag becomes
    /// <c>false</c> - or, for <c>additionalServicesOn</c>, <c>true</c> - is this rule.
    /// </para>
    /// </remarks>
    public sealed class MagEventRulesTests
    {
        private static readonly Guid Subscription = Guid.NewGuid();

        /// <summary>Resolves a send, asserting that nothing was refused.</summary>
        private static MagEventWrite Resolve(NewMagEvent request)
        {
            var write = MagEventRules.TryResolve(request, out var errors);

            Assert.Empty(errors);
            Assert.NotNull(write);

            return write!;
        }

        /// <summary>A request of one kind, with no flags named.</summary>
        private static NewMagEvent Request(MagEventType type, string? message = null)
            => new()
            {
                SubscriptionId = Subscription,
                EventType = type,
                Message = message
            };

        [Fact]
        public void ASendMessageForcesNeedConfirmWhateverTheBodySaid()
        {
            // The contract's rule 1: a message must be confirmed, so the body cannot switch it
            // off. Both spellings - absent and explicitly false - are overridden.
            var absent = Resolve(Request(MagEventType.SendMessage, "Hello"));
            Assert.True(absent.NeedConfirm);

            var refused = Resolve(new NewMagEvent
            {
                SubscriptionId = Subscription,
                EventType = MagEventType.SendMessage,
                Message = "Hello",
                NeedConfirm = false
            });
            Assert.True(refused.NeedConfirm);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ASendMessageWithoutAMessageIsRefused(string? message)
        {
            var write = MagEventRules.TryResolve(
                Request(MagEventType.SendMessage, message),
                out var errors);

            Assert.Null(write);
            Assert.True(errors.ContainsKey("Message"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void ASendMessageIsTrimmedAndStoredAtItsEdges()
        {
            Assert.Equal(
                "Hello there",
                Resolve(Request(MagEventType.SendMessage, "  Hello there  ")).Message);
        }

        [Fact]
        public void ASendMessageAtTheColumnWidthIsAcceptedAndOneLongerIsRefused()
        {
            var atWidth = new string('m', MagEventListLimits.MessageMaxLength);
            Assert.Equal(atWidth, Resolve(Request(MagEventType.SendMessage, atWidth)).Message);

            var tooLong = MagEventRules.TryResolve(
                Request(MagEventType.SendMessage, new string('m', MagEventListLimits.MessageMaxLength + 1)),
                out var errors);

            Assert.Null(tooLong);
            Assert.True(errors.ContainsKey("Message"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void APlayChannelMessageMustParseAsAUuid()
        {
            var stream = Guid.NewGuid();

            var write = Resolve(Request(MagEventType.PlayChannel, $" {stream} "));

            // Format only: the rule stores the identifier as sent and never asks the stream table
            // whether that video stream exists - the contract's rule 2.
            Assert.Equal(stream.ToString(), write.Message);
        }

        [Theory]
        [InlineData("not-a-channel")]
        [InlineData("42")]
        [InlineData("")]
        public void APlayChannelMessageThatIsNotAUuidIsRefusedUnderMessage(string message)
        {
            var write = MagEventRules.TryResolve(
                Request(MagEventType.PlayChannel, message),
                out var errors);

            Assert.Null(write);
            Assert.True(errors.ContainsKey("Message"), string.Join(", ", errors.Keys));
        }

        [Theory]
        [InlineData(MagEventType.ClosePortal)]
        [InlineData(MagEventType.ReloadPortal)]
        [InlineData(MagEventType.RebootDevice)]
        [InlineData(MagEventType.ResetStbLock)]
        public void EveryOtherKindDropsTheMessageToNull(MagEventType type)
        {
            // Whatever was sent - text, a uuid, or whitespace - these four operations carry no
            // payload, and storing one would be a value the request did not mean.
            Assert.Null(Resolve(Request(type, "ignored payload")).Message);
            Assert.Null(Resolve(Request(type, Guid.NewGuid().ToString())).Message);
            Assert.Null(Resolve(Request(type, "   ")).Message);
        }

        [Theory]
        [InlineData(MagEventType.ClosePortal)]
        [InlineData(MagEventType.ReloadPortal)]
        [InlineData(MagEventType.RebootDevice)]
        [InlineData(MagEventType.ResetStbLock)]
        public void EveryOtherKindNeedsNoMessageAtAll(MagEventType type)
        {
            Assert.Null(Resolve(Request(type)).Message);
        }

        [Fact]
        public void EveryUnsetFlagTakesItsContractDefault()
        {
            // All false except additionalServicesOn, which is true. The database has no default
            // for any of them; this is where the values come from.
            var write = Resolve(Request(MagEventType.RebootDevice));

            Assert.False(write.NeedConfirm);
            Assert.False(write.RebootAfterOk);
            Assert.False(write.AutoHideTimeout);
            Assert.True(write.AdditionalServicesOn);
            Assert.False(write.Anec);
            Assert.False(write.Vclub);
        }

        [Fact]
        public void EveryFlagTheBodyNamesIsKept()
        {
            var write = Resolve(new NewMagEvent
            {
                SubscriptionId = Subscription,
                EventType = MagEventType.RebootDevice,
                NeedConfirm = true,
                RebootAfterOk = true,
                AutoHideTimeout = true,
                AdditionalServicesOn = false,
                Anec = true,
                Vclub = true
            });

            Assert.True(write.NeedConfirm);
            Assert.True(write.RebootAfterOk);
            Assert.True(write.AutoHideTimeout);
            Assert.False(write.AdditionalServicesOn);
            Assert.True(write.Anec);
            Assert.True(write.Vclub);
        }

        [Fact]
        public void ASendWithoutASubscriptionIsRefusedUnderSubscriptionId()
        {
            var write = MagEventRules.TryResolve(
                new NewMagEvent { SubscriptionId = Guid.Empty, EventType = MagEventType.RebootDevice },
                out var errors);

            Assert.Null(write);
            Assert.True(errors.ContainsKey("SubscriptionId"), string.Join(", ", errors.Keys));
        }

        [Fact]
        public void AnEventThatIsNotAMemberIsRefusedUnderEvent()
        {
            var write = MagEventRules.TryResolve(
                new NewMagEvent { SubscriptionId = Subscription, EventType = (MagEventType)99 },
                out var errors);

            Assert.Null(write);
            Assert.True(errors.ContainsKey("Event"), string.Join(", ", errors.Keys));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void ASubscriptionWithoutAMagDeviceIsReportedAsMissing(string? magMac)
        {
            // Rule 5: the subscription must carry a MAG address. This answers the question; the
            // service owns the refusal's code because it is a fact about the row, not the body.
            Assert.True(MagEventRules.IsMagDeviceMissing(magMac));
            Assert.False(MagEventRules.IsMagDeviceMissing("AA:BB:CC:DD:EE:FF"));
        }
    }
}
