using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one subscription - the row an operator issues to a user. It is
    /// mutable, has no behaviour beyond mapping to <see cref="Subscription"/>, and knows nothing
    /// about HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three services are columns rather than child rows, because each kind is at most one
    /// per subscription: <see cref="PlaylistId"/>, <see cref="MagMac"/> and
    /// <see cref="Enigma2Mac"/>. Global uniqueness is guarded by three partial unique indexes
    /// (<c>... WHERE ... IS NOT NULL</c>), which is what lets the column be nullable and still
    /// unique.
    /// </para>
    /// <para>
    /// What a subscription carries of its own content is the <see cref="SubscriptionBouquetEntity"/>
    /// junction, used only while no plan is attached. With a plan the plan's own junction is the
    /// source of truth and these rows are kept but dormant.
    /// </para>
    /// <para>
    /// The two whitelists are <c>text[]</c> columns for the reason the on-demand catalogue's
    /// <c>cast</c> and <c>genres</c> are: they are read with the row and never queried by
    /// position, so a child table would buy a join and the "unloaded collection reads as empty"
    /// bug class for an order the array already keeps.
    /// </para>
    /// </remarks>
    public sealed class SubscriptionEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionEntity"/> class for a
        /// subscription that is about to be inserted. The identifier and both audit timestamps
        /// are generated.
        /// </summary>
        public SubscriptionEntity()
        {
        }

        /// <summary>Owner of the subscription. Removed with the account.</summary>
        public Guid UserId { get; set; }

        /// <summary>The playlist service's identifier, unique across subscriptions, or null.</summary>
        public string? PlaylistId { get; set; }

        /// <summary>Moment the subscription lapses, or null for "never expires".</summary>
        public DateTimeOffset? ExpireAt { get; set; }

        /// <summary>
        /// The tariff plan it is sold under, or null. The foreign key is <c>RESTRICT</c>: a plan
        /// a subscription still derives its parameters from may not be deleted out from under it.
        /// </summary>
        public Guid? TariffPlanId { get; set; }

        /// <summary>That plan.</summary>
        public TariffPlanEntity? TariffPlan { get; set; }

        /// <summary>Connection ceiling, or null for unlimited.</summary>
        public int? MaxConnections { get; set; }

        /// <summary>Whether the subscriber may restream.</summary>
        public bool IsRestreamer { get; set; }

        /// <summary>Addresses it may be watched from, exact or CIDR. Empty means no restriction.</summary>
        public string[] IpWhitelist { get; set; } = [];

        /// <summary>User agents it may be watched with. Empty means no restriction.</summary>
        public string[] UserAgentWhitelist { get; set; } = [];

        /// <summary>Whether it is pinned to one provider.</summary>
        public bool IsIspLock { get; set; }

        /// <summary>Whether it is pinned to one country.</summary>
        public bool IsCountryLock { get; set; }

        /// <summary>Whether it is a trial.</summary>
        public bool IsTrial { get; set; }

        /// <summary>
        /// Account that issued it. Set null when that account is deleted - provenance is not
        /// identity, and a departed issuer must not block the account's own deletion.
        /// </summary>
        public Guid? ResellerId { get; set; }

        /// <summary>The formats it is delivered in, as a <c>smallint</c> bit set.</summary>
        public SubscriptionOutputFormat OutputFormats { get; set; } = SubscriptionOutputFormat.MpegTs;

        /// <summary>MAG device's MAC, canonical <c>XX:XX:XX:XX:XX:XX</c>, or null.</summary>
        public string? MagMac { get; set; }

        /// <summary>Whether MAG identifiers are checked strictly.</summary>
        public bool IsStbLock { get; set; }

        /// <summary>Enigma2 device's MAC, canonical <c>XX:XX:XX:XX:XX:XX</c>, or null.</summary>
        public string? Enigma2Mac { get; set; }

        /// <summary>Reserved for the streaming subsystem. Nothing in this pass writes it.</summary>
        public string? PlayToken { get; set; }

        /// <summary>Bouquets it carries while no plan is attached.</summary>
        public List<SubscriptionBouquetEntity> Bouquets { get; set; } = [];

        /// <summary>
        /// Builds the business model out of this row and the bouquet links it was loaded with.
        /// </summary>
        /// <remarks>
        /// An unloaded collection reads as empty, which is why every load that ends here either
        /// includes the links or is a projection that fills the list below. A subscription whose
        /// bouquets nobody read is not the same fact as one that carries none.
        /// </remarks>
        public Subscription ToModel() => new()
        {
            Id = Id,
            UserId = UserId,
            PlaylistId = PlaylistId,
            ExpireAt = ExpireAt,
            TariffPlanId = TariffPlanId,
            MaxConnections = MaxConnections,
            IsRestreamer = IsRestreamer,
            IpWhitelist = IpWhitelist,
            UserAgentWhitelist = UserAgentWhitelist,
            IsIspLock = IsIspLock,
            IsCountryLock = IsCountryLock,
            IsTrial = IsTrial,
            ResellerId = ResellerId,
            OutputFormats = OutputFormats,
            MagMac = MagMac,
            IsStbLock = IsStbLock,
            Enigma2Mac = Enigma2Mac,
            PlayToken = PlayToken,
            BouquetIds = Bouquets.Select(link => link.BouquetId).ToList(),
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };

        /// <summary>
        /// Copies the changeable fields of the business model onto this row.
        /// </summary>
        /// <remarks>
        /// The owner and the play token are deliberately absent. The owner never changes after a
        /// subscription is issued, and the token belongs to a subsystem that does not exist yet -
        /// a save that touched either would be writing a fact the request did not state.
        /// </remarks>
        public void FromModel(Subscription model)
        {
            PlaylistId = model.PlaylistId;
            ExpireAt = model.ExpireAt;
            TariffPlanId = model.TariffPlanId;
            MaxConnections = model.MaxConnections;
            IsRestreamer = model.IsRestreamer;
            IpWhitelist = model.IpWhitelist.ToArray();
            UserAgentWhitelist = model.UserAgentWhitelist.ToArray();
            IsIspLock = model.IsIspLock;
            IsCountryLock = model.IsCountryLock;
            IsTrial = model.IsTrial;
            ResellerId = model.ResellerId;
            OutputFormats = model.OutputFormats;
            MagMac = model.MagMac;
            IsStbLock = model.IsStbLock;
            Enigma2Mac = model.Enigma2Mac;
        }
    }
}
