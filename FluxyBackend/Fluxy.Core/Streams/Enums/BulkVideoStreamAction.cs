namespace Fluxy.Core.Streams.Enums
{
    /// <summary>
    /// The operations one bulk request may apply to a set of streams.
    /// </summary>
    /// <remarks>
    /// The members name operations rather than fields. A bulk edit is a different thing from a
    /// patch of twenty rows: it changes the one value the operator chose and leaves every other
    /// value of every row alone, so the shape of the request stays one value and the rules the
    /// value has to satisfy are the rules the single-row service already enforces.
    /// </remarks>
    public enum BulkVideoStreamAction
    {
        /// <summary>Mark every named stream as broadcasting.</summary>
        Start,

        /// <summary>Mark every named stream as no longer broadcasting.</summary>
        Stop,

        /// <summary>Mark every named stream as broadcasting again.</summary>
        Restart,

        /// <summary>Delete every named stream together with its sources and files.</summary>
        Delete,

        /// <summary>Move every named stream to another category.</summary>
        MoveCategory,

        /// <summary>Change the kind of every named stream, within the kinds this operation allows.</summary>
        ChangeType,

        /// <summary>Write one delay to every named permanent stream.</summary>
        ChangeDelay,

        /// <summary>Write one archive length to every named permanent stream.</summary>
        ChangeArchive,

        /// <summary>Replace all four restream settings of every named permanent or on-demand stream.</summary>
        ChangeRestream
    }
}
