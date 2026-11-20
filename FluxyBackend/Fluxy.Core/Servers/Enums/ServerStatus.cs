namespace Fluxy.Core.Servers.Enums
{
    /// <summary>
    /// Whether a server is active or disabled.
    /// </summary>
    /// <remarks>
    /// <b>This enum is not ordered.</b> The two members are states of one server, not levels of
    /// service; a check is for the exact member and never a comparison. The numeric values are
    /// stored as <c>smallint</c> and are therefore part of the schema.
    /// </remarks>
    public enum ServerStatus
    {
        /// <summary>Disabled: the row is kept, but nothing is relayed through it.</summary>
        Disabled = 0,

        /// <summary>Active and serving.</summary>
        Active = 1
    }
}
