namespace Fluxy.Core.Servers.Enums
{
    /// <summary>
    /// How an admin operation on a server ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404, which a 409 and
    /// which code a client branches on is the transport's answer, stated beside the other
    /// response tables in the API layer - so a status can change without touching a rule, and a
    /// rule can change without anybody having to decide what status code it should start
    /// returning.
    /// </remarks>
    public enum ServerAction
    {
        /// <summary>The server was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The server was deleted.</summary>
        Removed,

        /// <summary>No server with that identifier.</summary>
        NotFound,

        /// <summary>Another server already holds that name.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid,

        /// <summary>
        /// The request addressed the main server, which this installation may change but never
        /// remove. Refused here rather than by a hidden control, so that an endpoint cannot be
        /// asked twice in two shapes.
        /// </summary>
        ImmutableMain
    }
}
