namespace Fluxy.Core.Vod.Enums
{
    /// <summary>Which way the on-demand table runs.</summary>
    public enum VodSortOrder
    {
        /// <summary>Smallest first: A before Z, inactive before active.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, active before inactive.</summary>
        Descending
    }
}
