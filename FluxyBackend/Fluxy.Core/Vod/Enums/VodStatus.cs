namespace Fluxy.Core.Vod.Enums
{
    /// <summary>Whether an on-demand item is published, or disabled and kept.</summary>
    /// <remarks>
    /// <para>
    /// Two states of one item, not two levels of anything, so nothing anywhere may compare two
    /// of them with <c>&gt;</c> or <c>&lt;</c> - an item is either published or it is not, and
    /// "more disabled" is not a thing. The numeric values are fixed because they are what the
    /// <c>status</c> column stores.
    /// </para>
    /// <para>
    /// The status controls what a client is offered, not whether the operator may preview the
    /// item: the play endpoint answers for an inactive row as well, because an operator
    /// checking a file before publishing it is exactly who that endpoint serves.
    /// </para>
    /// </remarks>
    public enum VodStatus
    {
        /// <summary>The item is disabled: kept, but not offered to clients.</summary>
        Inactive = 0,

        /// <summary>The item is published.</summary>
        Active = 1
    }
}
