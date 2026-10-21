namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// A setting a bulk type change cleared because the destination kind cannot carry it.
    /// </summary>
    /// <remarks>
    /// A warning is not a refusal: the change landed, and the cleared value is the only thing
    /// the operator has to know about afterwards. The transport turns each member into a code
    /// and an English sentence, so the rule that decides what is cleared and the words a person
    /// reads stay two separate things.
    /// </remarks>
    public enum VideoStreamBulkWarning
    {
        /// <summary>The stream's delay was cleared because the destination kind is not permanent.</summary>
        DelayCleared,

        /// <summary>The stream's archive length was cleared because the destination kind is not permanent.</summary>
        ArchiveCleared,

        /// <summary>The four restream settings were cleared because the destination kind does not restream.</summary>
        RestreamCleared
    }
}
