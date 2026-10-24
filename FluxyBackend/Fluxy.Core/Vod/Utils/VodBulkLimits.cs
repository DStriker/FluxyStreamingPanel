namespace Fluxy.Core.Vod.Utils
{
    /// <summary>
    /// The bounds of one bulk operation over on-demand items.
    /// </summary>
    /// <remarks>
    /// The ceiling follows the largest page a table can draw, because the selection a bulk
    /// operation acts on is the rows of the page the operator is looking at. A second number
    /// would let the table select more rows than it could ever show.
    /// </remarks>
    public static class VodBulkLimits
    {
        /// <summary>Most items one bulk request may name.</summary>
        public const int MaxItems = VodListLimits.MaxPageSize;
    }
}
