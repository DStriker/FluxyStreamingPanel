using Fluxy.Core.Abstractions;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one link in a sign-in session's refresh chain.
    /// </summary>
    /// <remarks>
    /// A row is a spent or live refresh token, and only ever a hash of it. Storing the token
    /// itself would put a usable credential in every database backup and in the table of anyone
    /// who can read it, which is the whole reason the column is called what it is.
    ///
    /// Rows are kept after they are revoked rather than deleted. A refresh token presented twice
    /// is only recognisable as such if the row it belonged to is still there to be compared
    /// against, and that comparison is what turns a stolen token into a detected one. They are
    /// cheap, they expire on their own, and a periodic cleanup can remove anything older than
    /// the longest session without affecting any live one.
    /// </remarks>
    public sealed class RefreshTokenEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenEntity"/> class for a token
        /// that is about to be issued. The identifier is generated.
        /// </summary>
        public RefreshTokenEntity()
        {
        }

        /// <summary>Account the token signs in.</summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Lower case hexadecimal SHA-256 of the token. Unique, and the only thing about the
        /// token that is kept.
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Session this token belongs to. Every token ever issued from one sign-in carries the
        /// same value, which is what lets a sign-out end the whole chain at once.
        /// </summary>
        public Guid SessionId { get; set; }

        /// <summary>Moment the token stops being accepted, whether or not it was revoked.</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// Moment the token was withdrawn, or null while it is still live. Set when it is spent
        /// by a refresh as well as when the session is ended, because the two mean different
        /// things: a spent token is a normal event, a revoked session is not.
        /// </summary>
        public DateTimeOffset? RevokedAt { get; set; }

        /// <summary>
        /// Token issued in exchange for this one, or null while this one is live. It makes the
        /// chain walkable, which is what a stolen-and-reused token is detected along.
        /// </summary>
        public Guid? ReplacedById { get; set; }

        /// <summary>Address the sign-in or the refresh came from, for the session's own record.</summary>
        public string? ClientAddress { get; set; }

        /// <summary>User agent the sign-in or the refresh came with, for the same reason.</summary>
        public string? UserAgent { get; set; }
    }
}
