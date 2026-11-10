using Fluxy.Core.Content.Enums;
using Fluxy.Core.Requests.Enums;

namespace Fluxy.Core.Requests.ReadModels
{
    /// <summary>
    /// One refused API request as a row of the admin table: the subscription data it named, the
    /// content snapshot, the reason, the network it came from and when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The content name is the stored snapshot, never a resolved one.</b> A row that names
    /// content which has since been deleted still reads with the name captured at write time -
    /// no read path joins a content table, and this type has no field a join could fill.
    /// </para>
    /// <para>
    /// <see cref="Description"/> is the stored sentence and stays legible on its own, which is
    /// what makes a refusal worth reading after the subscription it named is gone.
    /// </para>
    /// </remarks>
    public sealed record ApiRequestFailureListItem
    {
        /// <summary>Identifier of the failure record.</summary>
        public required Guid Id { get; init; }

        /// <summary>Free-form access parameter the refused request named, or null.</summary>
        public string? SubscriptionData { get; init; }

        /// <summary>Kind of the content the request named, or null.</summary>
        public ContentType? ContentKind { get; init; }

        /// <summary>Opaque identifier of that content, or null.</summary>
        public Guid? ContentId { get; init; }

        /// <summary>Name of that content as it read at write time, or null.</summary>
        public string? ContentName { get; init; }

        /// <summary>Why the request was refused.</summary>
        public required ApiRequestFailureReason Reason { get; init; }

        /// <summary>Address the request came from.</summary>
        public required string Ip { get; init; }

        /// <summary>Provider the address belonged to, or null.</summary>
        public string? Isp { get; init; }

        /// <summary>Upper-case ISO 3166-1 alpha-2 country of the address, or null.</summary>
        public string? CountryCode { get; init; }

        /// <summary>English sentence describing the refusal.</summary>
        public required string Description { get; init; }

        /// <summary>When the record was written.</summary>
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
