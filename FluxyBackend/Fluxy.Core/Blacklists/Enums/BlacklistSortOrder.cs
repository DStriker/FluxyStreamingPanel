namespace Fluxy.Core.Blacklists.Enums
{
    /// <summary>Which way a blacklist table runs.</summary>
    public enum BlacklistSortOrder
    {
        /// <summary>Smallest first: A before Z, older before newer.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, newer before older.</summary>
        Descending
    }
}
