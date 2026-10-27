using Fluxy.Core.Content.Enums;

namespace Fluxy.Core.Content.Commands
{
    /// <summary>
    /// The whole content of one kind that a bouquet should hold, in the order it should hold it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One kind at a time, because the four kinds are ordered apart: a video stream's position
    /// says nothing about an audio stream's, and a request that named all four would have to
    /// decide what "position 3" meant for each of them.
    /// </para>
    /// <para>
    /// The list is the new membership **and** the new order, in one statement. That is what the
    /// content manager produces - the operator moves items between two lists and arranges what
    /// is inside - and a request that named only the additions would leave the browser to
    /// describe positions the server already has. The order the list carries is written as
    /// 1..N in that order, so what the screen shows after saving is exactly what was dragged.
    /// </para>
    /// <para>
    /// Every identifier must name an item of the stated kind, exactly once. An unknown one is a
    /// refusal rather than a silent skip: a request that changed less than it said while
    /// answering that it had worked is the shape of bug this API refuses elsewhere too.
    /// </para>
    /// </remarks>
    public sealed record SetBouquetContent
    {
        /// <summary>Which of the four content kinds the list is about.</summary>
        public required ContentType ContentType { get; init; }

        /// <summary>
        /// Every item of that kind the bouquet should hold, in the order it should hold them.
        /// An empty list empties the bouquet of that kind.
        /// </summary>
        public required IReadOnlyList<Guid> ContentIds { get; init; }
    }
}
