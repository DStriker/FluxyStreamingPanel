namespace Fluxy.Core.Series.Enums
{
    /// <summary>Which way the series table runs.</summary>
    public enum SeriesSortOrder
    {
        /// <summary>Smallest first: A before Z, inactive before active.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, active before inactive.</summary>
        Descending
    }
}
