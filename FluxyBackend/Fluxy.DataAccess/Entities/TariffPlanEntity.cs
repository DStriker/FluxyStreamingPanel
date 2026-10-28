using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Models;

namespace Fluxy.DataAccess.Entities
{
    /// <summary>
    /// Database representation of one tariff plan - the package a subscription is sold under. It
    /// is mutable, has no behaviour beyond mapping to <see cref="TariffPlan"/>, and knows nothing
    /// about HTTP.
    /// </summary>
    /// <remarks>
    /// What the plan is *sold to* and what it *delivers* are not columns on this row: the two
    /// junction tables (<see cref="TariffPlanUserGroupEntity"/> and
    /// <see cref="TariffPlanBouquetEntity"/>) own those, because each is a many-to-many relation
    /// the database can index and a deletion can clean up through a real foreign key.
    /// </remarks>
    public sealed class TariffPlanEntity : AuditableEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TariffPlanEntity"/> class for a plan that
        /// is about to be inserted. The identifier and both audit timestamps are generated.
        /// </summary>
        public TariffPlanEntity()
        {
        }

        /// <summary>
        /// Display name, at most <see cref="Configurations.TariffPlanConfiguration.NameMaxLength"/>
        /// characters. Unique for the installation **without regard to case** - see the unique
        /// index over <c>lower(name)</c> the configuration's migration creates.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the plan is a free trial.</summary>
        public bool IsTrial { get; set; }

        /// <summary>Cost in credits. Zero is a free plan.</summary>
        public int Cost { get; set; }

        /// <summary>How much of <see cref="DurationUnit"/> the plan lasts for.</summary>
        public int DurationValue { get; set; }

        /// <summary>The unit the validity period is counted in.</summary>
        public TariffPlanDurationUnit DurationUnit { get; set; } = TariffPlanDurationUnit.Days;

        /// <summary>Whether the plan may be delivered as an M3U playlist.</summary>
        public bool SupportsM3u { get; set; }

        /// <summary>Whether the plan may be delivered to a MAG set-top box.</summary>
        public bool SupportsMag { get; set; }

        /// <summary>Whether the plan may be delivered to an Enigma2 receiver.</summary>
        public bool SupportsEnigma2 { get; set; }

        /// <summary>Whether the plan may be delivered as an MPEG-TS stream.</summary>
        public bool OutputMpegTs { get; set; }

        /// <summary>Whether the plan may be delivered as an HLS stream.</summary>
        public bool OutputHls { get; set; }

        /// <summary>
        /// Whether a MAG device's STB identifier is checked strictly. Only ever true while
        /// <see cref="SupportsMag"/> is true - the service refuses any other combination.
        /// </summary>
        public bool StrictStbDeviceId { get; set; }

        /// <summary>Whether a subscriber may restream the subscription.</summary>
        public bool AllowRestream { get; set; }

        /// <summary>How many devices may be connected at the same time.</summary>
        public int MaxConnections { get; set; }

        /// <summary>Whether connections from different countries are allowed at the same time.</summary>
        public bool AllowMultipleCountries { get; set; } = true;

        /// <summary>Builds the business model out of this row.</summary>
        public TariffPlan ToModel() => new()
        {
            Id = Id,
            Name = Name,
            IsTrial = IsTrial,
            Cost = Cost,
            DurationValue = DurationValue,
            DurationUnit = DurationUnit,
            SupportsM3u = SupportsM3u,
            SupportsMag = SupportsMag,
            SupportsEnigma2 = SupportsEnigma2,
            OutputMpegTs = OutputMpegTs,
            OutputHls = OutputHls,
            StrictStbDeviceId = StrictStbDeviceId,
            AllowRestream = AllowRestream,
            MaxConnections = MaxConnections,
            AllowMultipleCountries = AllowMultipleCountries
        };

        /// <summary>Copies the changeable fields of the business model onto this row.</summary>
        public void FromModel(TariffPlan model)
        {
            Name = model.Name;
            IsTrial = model.IsTrial;
            Cost = model.Cost;
            DurationValue = model.DurationValue;
            DurationUnit = model.DurationUnit;
            SupportsM3u = model.SupportsM3u;
            SupportsMag = model.SupportsMag;
            SupportsEnigma2 = model.SupportsEnigma2;
            OutputMpegTs = model.OutputMpegTs;
            OutputHls = model.OutputHls;
            StrictStbDeviceId = model.StrictStbDeviceId;
            AllowRestream = model.AllowRestream;
            MaxConnections = model.MaxConnections;
            AllowMultipleCountries = model.AllowMultipleCountries;
        }
    }
}
