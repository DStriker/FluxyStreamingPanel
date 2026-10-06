namespace Fluxy.Core.Models.Authentication
{
    /// <summary>Which way a page of the visit history runs.</summary>
    /// <remarks>
    /// The default is <see cref="Descending"/> because the newest visit is what somebody opening
    /// this page is looking for, and making them click a header to get there would be making them
    /// undo the sort that was chosen for them. A client spells it <c>desc</c> or <c>asc</c>;
    /// <c>ProfileController</c> turns the text into a member.
    /// </remarks>
    public enum SessionSortOrder
    {
        /// <summary>Smallest first: the earliest visit, or the lowest address.</summary>
        Ascending = 1,

        /// <summary>Largest first. The default.</summary>
        Descending = 2
    }
}
