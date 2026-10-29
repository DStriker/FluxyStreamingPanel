namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// A thing a successful bulk row has to tell the operator but did not refuse over.
    /// </summary>
    /// <remarks>
    /// A plan change clears the services the new plan does not support, and that is the
    /// consequence of the choice rather than a refusal - refusing would make a whole
    /// selection fail because one row happened to hold a device the destination plan does not
    /// carry. Each clearance is reported so the operator learns it happened, which is exactly
    /// what a warning is for: the row <b>was</b> written.
    /// </remarks>
    public enum SubscriptionBulkWarning
    {
        /// <summary>The plan change cleared a playlist id the new plan cannot deliver.</summary>
        PlaylistCleared,

        /// <summary>The plan change cleared a MAG MAC the new plan cannot deliver.</summary>
        MagCleared,

        /// <summary>The plan change cleared an Enigma2 MAC the new plan cannot deliver.</summary>
        Enigma2Cleared
    }
}
