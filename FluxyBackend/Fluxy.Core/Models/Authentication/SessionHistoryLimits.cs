namespace Fluxy.Core.Models.Authentication
{
    /// <summary>
    /// The numbers the visit history endpoint accepts.
    /// </summary>
    /// <remarks>
    /// Living in Core rather than in the controller means the transport and the service name one
    /// pair of values instead of carrying two copies that will eventually stop agreeing - the
    /// same reason <c>RefreshTokenConfigurationLimits</c> exists, except that here both halves
    /// are layers this one already depends on rather than one either of them must not see.
    ///
    /// The bounds are a rule about a read, not about a secret: a page larger than
    /// <see cref="MaxPageSize"/> is refused rather than clamped silently, because a caller who
    /// asked for 500 rows deserves to be told it will not get them instead of quietly receiving
    /// 100 and reporting a total that does not match the items it was handed.
    /// </remarks>
    public static class SessionHistoryLimits
    {
        /// <summary>Visits one page holds when the caller names no size.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Most visits one page may hold.</summary>
        public const int MaxPageSize = 100;
    }
}
