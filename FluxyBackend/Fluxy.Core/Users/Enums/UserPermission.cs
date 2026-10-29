using Fluxy.Core.Users.Utils;

namespace Fluxy.Core.Users.Enums
{
    /// <summary>
    /// One thing a group allows its members to do, as a flag so that "may this account do it"
    /// is a single bitwise test over the set its group carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A permission is <i>not</i> a level of access. The level of access is
    /// <see cref="UserRole"/>, which decides what part of the API and of the interface an
    /// account reaches at all; a permission only answers a question asked <i>inside</i> that
    /// part. An account whose group holds <see cref="EditAdmins"/> but sits in a group with
    /// role <see cref="Fluxy.Core.Users.Enums.UserRole.Client"/> still cannot reach the administrator's
    /// endpoints, because the role gate is checked first and separately.
    /// </para>
    /// <para>
    /// <b>The account permissions are split by the role of the account they act on, not by
    /// the role of the account doing the acting.</b> Every one of them is owned by
    /// <see cref="Fluxy.Core.Users.Enums.UserRole.Admin"/> - they are questions asked inside the admin area -
    /// and what they differ on is which accounts of the installation they reach:
    /// <see cref="ViewClients"/> answers for rows whose group is a group of clients, while
    /// <see cref="ViewAdmins"/> answers for the administrators. That is what lets a group of
    /// operators run the clients and leave the other operators alone, which one pair of
    /// global keys could not express at all.
    /// </para>
    /// <para>
    /// <b>The stream permissions are not split that way, because a stream is not an account.</b>
    /// There is one <see cref="ViewStreams"/> and one <see cref="EditStreams"/> for video
    /// streams, and one <see cref="ViewAudioStreams"/> and one <see cref="EditAudioStreams"/>
    /// for audio streams, with nothing narrower. The two media are separate pairs because they
    /// are separate screens and an operator may run one without the other; within a media there
    /// is one installation-wide list and one screen that draws it, so there is no second axis
    /// to split along. Each pair keeps the read and write grants apart so that a group which
    /// may watch a list and a group which may run it stay two grants.
    /// </para>
    /// <para>
    /// <b>The on-demand pair is the same question asked about the catalogue.</b> A video
    /// available on demand is neither a live stream nor an account, so it has its own read and
    /// write keys (<see cref="ViewVideoOnDemand"/> and <see cref="EditVideoOnDemand"/>), and a
    /// group that may watch the streams may not change the catalogue. The pair is separate for
    /// the reason the audio one is: the screen, the table and the form are their own.
    /// </para>
    /// <para>
    /// <b>The guide pair guards the sources a programme schedule is read from.</b>
    /// <see cref="ViewEpgSources"/> is the sources table and the channels that use a source;
    /// <see cref="EditEpgSources"/> is creating, changing and deleting a source. They are
    /// separate from the stream pair because a guide source is its own resource: a group may
    /// run the streams and have no business pointing them at a different guide.
    /// </para>
    /// <para>
    /// The numeric values are powers of two and are stored in <c>user_group_permissions</c>,
    /// so they are part of the schema: adding a member means adding a value nobody has used
    /// yet, and renumbering the existing ones would silently reinterpret rows already
    /// written. The account keys therefore start at 16 rather than at 1 - the values 1 and 2
    /// were the retired <c>viewUsers</c>/<c>editUsers</c> of the first version, and handing
    /// them to a new meaning would turn every stored grant into a grant of something else
    /// without a single query being made. New permissions are added here and registered in
    /// <see cref="UserPermissionCatalog"/>, which is what ties them to a role - no migration
    /// is needed for an addition, because the catalog is code and the base groups are
    /// reconciled with it at startup.
    /// </para>
    /// </remarks>
    [Flags]
    public enum UserPermission
    {
        /// <summary>No permission at all. Also the empty set a group may be saved with.</summary>
        None = 0,

        /// <summary>May read the groups: the groups list and one group in full.</summary>
        ViewUserGroups = 4,

        /// <summary>May change the groups: create, rename, re-role, re-status and delete them.</summary>
        EditUserGroups = 8,

        /// <summary>
        /// May read the accounts of the installation whose group is a group of clients: the
        /// users list and one such account in full, and only those.
        /// </summary>
        ViewClients = 16,

        /// <summary>
        /// May read the accounts whose group is a group of resellers, and only those.
        /// </summary>
        ViewResellers = 32,

        /// <summary>
        /// May read the accounts whose group is a group of administrators, and only those.
        /// </summary>
        ViewAdmins = 64,

        /// <summary>
        /// May change the accounts of a group of clients: create them in such a group, edit,
        /// confirm, block, unblock and delete them. Separate from <see cref="ViewClients"/> so
        /// that a group which may watch the clients and a group which may run them are two
        /// different grants rather than one.
        /// </summary>
        EditClients = 128,

        /// <summary>May change the accounts of a group of resellers.</summary>
        EditResellers = 256,

        /// <summary>
        /// May change the accounts of a group of administrators - the highest grant in the
        /// system, and deliberately not implied by any of the others.
        /// </summary>
        EditAdmins = 512,

        /// <summary>
        /// May read the configured video streams: the streams table and one stream in full.
        /// </summary>
        ViewStreams = 1024,

        /// <summary>
        /// May change the configured video streams: create, edit, start, stop and delete them.
        /// Separate from <see cref="ViewStreams"/> so that a group which may watch the streams
        /// and a group which may run them are two different grants rather than one.
        /// </summary>
        EditStreams = 2048,

        /// <summary>
        /// May read the configured audio streams: the streams table and one stream in full.
        /// </summary>
        ViewAudioStreams = 4096,

        /// <summary>
        /// May change the configured audio streams: create, edit, start, stop and delete them.
        /// Separate from <see cref="ViewAudioStreams"/> for the reason <see cref="EditStreams"/>
        /// gives.
        /// </summary>
        EditAudioStreams = 8192,

        /// <summary>
        /// May read the on-demand catalogue: the videos table and one video in full.
        /// </summary>
        ViewVideoOnDemand = 16384,

        /// <summary>
        /// May change the on-demand catalogue: create, edit, import and delete videos, and ask
        /// for them to be converted. Separate from <see cref="ViewVideoOnDemand"/> for the
        /// reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditVideoOnDemand = 32768,

        /// <summary>
        /// May read the series catalogue: the series table and one series with its episodes.
        /// </summary>
        ViewSeries = 65536,

        /// <summary>
        /// May change the series catalogue: create, edit, delete and bulk-edit series, and
        /// create, edit, delete and bulk-edit their episodes and seasons. Separate from
        /// <see cref="ViewSeries"/> for the reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditSeries = 131072,

        /// <summary>
        /// May read the programme guide sources: the sources table, one source in full, and
        /// the streams that read their guide from a source.
        /// </summary>
        ViewEpgSources = 262144,

        /// <summary>
        /// May change the programme guide sources: create, edit and delete them. Separate from
        /// <see cref="ViewEpgSources"/> for the reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditEpgSources = 524288,

        /// <summary>
        /// May read the bouquets: the bouquets table, one bouquet's content and the bouquet
        /// picker on a content form.
        /// </summary>
        /// <remarks>
        /// A bouquet cuts across all four content kinds - it is a package of streams, videos
        /// and series rather than a catalogue of one of them - so this grant is about the
        /// packages themselves and not about their contents: an operator who may read the
        /// bouquets still needs <see cref="ViewStreams"/> and its siblings to see what a
        /// bouquet holds beyond its name.
        /// </remarks>
        ViewBouquets = 1048576,

        /// <summary>
        /// May change the bouquets: create, rename, re-order and delete them, and change which
        /// content each one holds and in what order. Separate from
        /// <see cref="ViewBouquets"/> for the reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditBouquets = 2097152,

        /// <summary>
        /// May read the tariff plans: the plans table, one plan in full with the groups it is
        /// offered to and the bouquets it delivers, and the plan picker anywhere else.
        /// </summary>
        /// <remarks>
        /// A tariff plan is the price a subscription is sold under rather than a package of
        /// content, so this is a pair of its own rather than a reuse of
        /// <see cref="ViewBouquets"/>: a group that may decide which packages are delivered and
        /// a group that may decide what a package costs are two different jobs. The plan still
        /// needs <see cref="ViewBouquets"/> and <see cref="ViewUserGroups"/> beside it to draw
        /// the two pickers - this grant is about the plans themselves.
        /// </remarks>
        ViewTariffPlans = 4194304,

        /// <summary>
        /// May change the tariff plans: create, edit and delete them, and change which groups
        /// they are offered to and which bouquets they deliver. Separate from
        /// <see cref="ViewTariffPlans"/> for the reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditTariffPlans = 8388608,

        /// <summary>
        /// May read the subscriptions: the subscriptions table, one subscription in full with its
        /// services and access parameters, and the plan picker on its form.
        /// </summary>
        /// <remarks>
        /// A subscription is neither a price list nor a package: it is the row a subscriber is
        /// actually issued, so it carries a pair of its own rather than reusing the plan one. An
        /// operator who may decide what a subscription costs and one who may issue it are two
        /// different jobs - the second touches somebody's access.
        /// </remarks>
        ViewSubscriptions = 16777216,

        /// <summary>
        /// May change the subscriptions: create, edit, delete and bulk-edit them, mint playlist
        /// ids and clear their services. Separate from <see cref="ViewSubscriptions"/> for the
        /// reason <see cref="EditStreams"/> gives.
        /// </summary>
        EditSubscriptions = 33554432
    }
}
