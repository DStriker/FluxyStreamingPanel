namespace Fluxy.Core.Content.Models
{
    /// <summary>
    /// Immutable business representation of one bouquet - a named package of content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bouquet is named and ordered, exactly as a <see cref="ContentCategory"/> is, and that
    /// is the whole of it: what a bouquet holds is a relation rather than a field, so it lives
    /// in four membership tables rather than on the row (see
    /// <c>Fluxy.Core.Content.ReadModels.BouquetContent</c> for the shape a screen reads).
    /// </para>
    /// <para>
    /// <see cref="SortOrder"/> is a position among the bouquets, not a rank with a meaning of
    /// its own. Gaps between the values are expected - deletions leave them behind, and nothing
    /// is renumbered to close them - so nothing may assume the values are contiguous.
    /// </para>
    /// <para>
    /// The name is unique for the installation <i>without regard to case</i>: "Кино" and "кино"
    /// are one bouquet, because an operator scanning the list cannot tell them apart and a
    /// package delivered to a subscriber under two spellings is the same package. That rule is
    /// enforced by a unique index over <c>lower(name)</c> as well as by the service, so a race
    /// between two operators cannot slip a second spelling in.
    /// </para>
    /// </remarks>
    public sealed record Bouquet
    {
        /// <summary>Identifier of the bouquet.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Display name, unique for the installation when compared without regard to case.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Position among the bouquets, lowest first. Assigned when the bouquet is created
        /// (after the current last one) and rewritten wholesale when an operator re-arranges
        /// the list.
        /// </summary>
        public required int SortOrder { get; init; }
    }
}
