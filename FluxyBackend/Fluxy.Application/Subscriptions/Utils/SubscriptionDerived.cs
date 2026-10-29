using Fluxy.Core.Subscriptions.Enums;

namespace Fluxy.Application.Subscriptions.Utils
{
    /// <summary>
    /// Everything an attached tariff plan decides for one subscription, as one value.
    /// </summary>
    /// <remarks>
    /// A helper type rather than six out-parameters, and it exists for exactly one reason: the
    /// create, the change and the bulk plan change all have to derive the same row from the same
    /// plan, and a tuple spelled three times is three places for one of them to fall out of step.
    /// It never leaves this layer.
    /// </remarks>
    /// <param name="MaxConnections">Connection ceiling the plan allows.</param>
    /// <param name="IsRestreamer">Whether the plan allows restreaming.</param>
    /// <param name="OutputFormats">The formats the plan delivers.</param>
    /// <param name="IsStbLock">Whether the plan's MAG identifiers are checked strictly.</param>
    /// <param name="IsCountryLock">Whether the plan pins the subscription to one country.</param>
    /// <param name="IsTrial">Whether the plan is a trial.</param>
    /// <param name="SupportsPlaylist">Whether the plan delivers a playlist.</param>
    /// <param name="SupportsMag">Whether the plan delivers to a MAG device.</param>
    /// <param name="SupportsEnigma2">Whether the plan delivers to an Enigma2 device.</param>
    /// <param name="ExpireAt">The expiry the plan computes for this subscription.</param>
    public sealed record SubscriptionDerived(
        int MaxConnections,
        bool IsRestreamer,
        SubscriptionOutputFormat OutputFormats,
        bool IsStbLock,
        bool IsCountryLock,
        bool IsTrial,
        bool SupportsPlaylist,
        bool SupportsMag,
        bool SupportsEnigma2,
        DateTimeOffset ExpireAt);
}
