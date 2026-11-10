using Fluxy.Core.Content.Enums;
using Fluxy.Core.Requests.Enums;

namespace Fluxy.Core.Requests.Models
{
    /// <summary>
    /// Immutable business representation of one refused public-API request: which subscription
    /// data asked for what content, why the request was refused, and from where.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="SubscriptionData"/> is opaque text, never a foreign key.</b> The public API
    /// receives it as a free-form access parameter, and a request that names an identifier this
    /// installation has no subscription for is exactly the failure worth recording. Pointing the
    /// column at <c>subscriptions</c> would refuse the row the log exists to keep.
    /// </para>
    /// <para>
    /// <b>The content snapshot is a group of three nullable columns.</b> A writer sets
    /// <see cref="ContentKind"/>, <see cref="ContentId"/> and <see cref="ContentName"/> together
    /// or leaves all three empty. The name is captured at write time and never resolved on read,
    /// so the row stays legible after the content it named is deleted; no read path joins a
    /// content table. None of the three is a foreign key, for the same reason.
    /// </para>
    /// <para>
    /// <b>The network facts are a snapshot taken at write time.</b> Country and provider are
    /// stored so the operator can filter and count on them on the server, exactly as the
    /// connection log stores the same three facts.
    /// </para>
    /// <para>
    /// <b>There is no production write path.</b> The record is read and wiped; the only insert is
    /// the Development seeder's. See <c>ApiRequestFailureEntity.ToModel</c>/<c>FromModel</c> for
    /// the reserved mapping the future capture path will use.
    /// </para>
    /// </remarks>
    public sealed record ApiRequestFailure
    {
        /// <summary>Identifier of the failure record.</summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Free-form access parameter the refused request named, at most 512 characters, or null
        /// when it named none. Deliberately not a subscription foreign key.
        /// </summary>
        public string? SubscriptionData { get; init; }

        /// <summary>Kind of the content the request named, or null when it named none.</summary>
        public ContentType? ContentKind { get; init; }

        /// <summary>
        /// Opaque identifier of that content, or null. No foreign key points at any content
        /// table: the row may name content this installation never recorded.
        /// </summary>
        public Guid? ContentId { get; init; }

        /// <summary>
        /// Name of that content as it read at write time, at most 255 characters, or null.
        /// Never resolved or recomputed on read.
        /// </summary>
        public string? ContentName { get; init; }

        /// <summary>Why the request was refused.</summary>
        public required ApiRequestFailureReason Reason { get; init; }

        /// <summary>Address the request came from, up to 45 characters (the longest IPv6 form).</summary>
        public required string Ip { get; init; }

        /// <summary>Internet provider the address belonged to, up to 255 characters, or null.</summary>
        public string? Isp { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null when unknown.</summary>
        public string? CountryCode { get; init; }

        /// <summary>English sentence describing the refusal, at most 1000 characters.</summary>
        public required string Description { get; init; }

        /// <summary>Creation timestamp of the row.</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>Last modification timestamp of the row.</summary>
        public required DateTimeOffset UpdatedAt { get; init; }
    }
}
