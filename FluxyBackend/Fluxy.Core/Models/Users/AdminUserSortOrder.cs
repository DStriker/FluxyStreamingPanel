namespace Fluxy.Core.Models.Users
{
    /// <summary>Which way the admin user list runs.</summary>
    public enum AdminUserSortOrder
    {
        /// <summary>Smallest first: A before Z, oldest before newest.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, newest before oldest.</summary>
        Descending
    }
}
