namespace Fluxy.Core.Servers.Enums
{
    /// <summary>Which way the server table runs.</summary>
    public enum ServerSortOrder
    {
        /// <summary>Smallest first: A before Z, disabled before active.</summary>
        Ascending,

        /// <summary>Largest first: Z before A, active before disabled.</summary>
        Descending
    }
}
