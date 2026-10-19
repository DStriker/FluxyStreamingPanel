using Fluxy.Core.Models.Streams;

namespace Fluxy.Core.Models.Content
{
    /// <summary>
    /// The four kinds of content this installation manages, each with its own set of
    /// categories.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The numeric values are fixed and must never be renumbered: they are what the
    /// <c>content_type</c> column stores, so a value already in the database is a fact about
    /// the past as much as about the code. A new member is added at the end.
    /// </para>
    /// <para>
    /// Like <see cref="Fluxy.Core.Users.Enums.UserStatus"/>, this is not an ordered scale - the members are
    /// kinds of content, not levels of anything - so nothing anywhere may compare two of
    /// them with <c>></c> or <c><</c>. A category belongs to exactly one kind, and
    /// moving content between kinds is not a thing this domain has.
    /// </para>
    /// </remarks>
    public enum ContentType
    {
        /// <summary>A live video broadcast.</summary>
        VideoStream = 1,

        /// <summary>A live audio broadcast.</summary>
        AudioStream = 2,

        /// <summary>A video available on demand.</summary>
        VideoOnDemand = 3,

        /// <summary>A series - ordered episodes rather than a single video.</summary>
        Series = 4,
    }
}
