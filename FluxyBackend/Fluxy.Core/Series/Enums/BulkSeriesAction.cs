namespace Fluxy.Core.Series.Enums
{
    /// <summary>
    /// The operations one bulk request may apply to a set of series.
    /// </summary>
    public enum BulkSeriesAction
    {
        /// <summary>Publish or disable every named series.</summary>
        ChangeStatus,

        /// <summary>Move every named series to another category.</summary>
        MoveCategory,

        /// <summary>Delete every named series and its episodes.</summary>
        Delete
    }
}
