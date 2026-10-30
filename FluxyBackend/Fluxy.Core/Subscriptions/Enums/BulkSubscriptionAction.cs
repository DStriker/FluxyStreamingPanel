namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// The operations one bulk request may run over a selection of subscriptions.
    /// </summary>
    /// <remarks>
    /// The first three are the ones a selection is collectivised for - move it to another
    /// plan, hand it to another issuer, remove it - and the four after them are the per-field
    /// clears a table row would otherwise have to be opened for. Every one of them is the
    /// single-row operation applied in a loop, so the codes a row answers with are the same
    /// codes the addressed endpoint gives.
    /// </remarks>
    public enum BulkSubscriptionAction
    {
        /// <summary>Attach one tariff plan to every selected row, deriving its parameters.</summary>
        ChangeTariff,

        /// <summary>Reassign the issuer of every selected row to one administrator or reseller.</summary>
        ChangeCreator,

        /// <summary>Remove every selected row.</summary>
        Delete,

        /// <summary>Clear the playlist id of every selected row. Already empty succeeds.</summary>
        DeletePlaylist,

        /// <summary>Clear the MAG MAC of every selected row. Already empty succeeds.</summary>
        DeleteMag,

        /// <summary>Clear the Enigma2 MAC of every selected row. Already empty succeeds.</summary>
        DeleteEnigma2,

        /// <summary>
        /// Extend the period of every selected row from its end by its plan's duration. A row with
        /// no plan is refused, because there is no duration to add.
        /// </summary>
        Prolong,

        /// <summary>Block the owner of every selected row through the account block.</summary>
        BlockOwner
    }
}
