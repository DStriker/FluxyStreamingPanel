namespace Fluxy.Core.Epg.Enums
{
    /// <summary>Which way the channels-using-a-source table runs.</summary>
    public enum EpgSourceChannelSortOrder
    {
        /// <summary>Smallest first: A before Z.</summary>
        Ascending,

        /// <summary>Largest first: Z before A.</summary>
        Descending
    }
}
