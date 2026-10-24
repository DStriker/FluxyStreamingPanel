namespace Fluxy.Core.Vod.Enums
{
    /// <summary>
    /// The operations one bulk request may apply to a set of on-demand items.
    /// </summary>
    /// <remarks>
    /// The members name operations rather than fields. A bulk edit changes the one value the
    /// operator chose and leaves every other value of every row alone, so the rules the value
    /// has to satisfy are the rules the single-row service already enforces.
    /// </remarks>
    public enum BulkVodAction
    {
        /// <summary>Publish or disable every named item.</summary>
        ChangeStatus,

        /// <summary>Move every named item to another category.</summary>
        MoveCategory,

        /// <summary>Change how every named item is delivered.</summary>
        ChangeType,

        /// <summary>Delete every named item.</summary>
        Delete
    }
}
