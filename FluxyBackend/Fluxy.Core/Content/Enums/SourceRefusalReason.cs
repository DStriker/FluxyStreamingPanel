namespace Fluxy.Core.Content.Enums
{
    /// <summary>
    /// Why one entity of a source replacement was left exactly as it was.
    /// </summary>
    /// <remarks>
    /// A refusal is per entity and all-or-nothing: one value the replacement would break
    /// refuses the whole entity, because half a failover chain pointing at the new host and
    /// half at the old one is worse than either. The members are checked in this order, so a
    /// replacement that produces a blank address reports <see cref="Blank"/> even when another
    /// value of the same entity also grew too long.
    /// </remarks>
    public enum SourceRefusalReason
    {
        /// <summary>The replacement would leave an address empty or whitespace only.</summary>
        Blank = 0,

        /// <summary>The replacement would make an address longer than its column allows.</summary>
        TooLong = 1
    }
}
