namespace Fluxy.Core.Epg.Enums
{
    /// <summary>Which way the guide source table runs.</summary>
    public enum EpgSourceSortOrder
    {
        /// <summary>Smallest first: A before Z, fewer days before more.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, more days before fewer.</summary>
        Descending
    }
}
