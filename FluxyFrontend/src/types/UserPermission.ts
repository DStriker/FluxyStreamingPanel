/**
 * One thing a group may let its members do, spelled exactly as the server's catalog spells it.
 *
 * These twenty-four strings are the whole catalog at the time of writing. They are a union rather
 * than `string` for the reason `errors` lands under camelCase JSON names on the other side:
 * a key typed here is checked where it is written, while a permission typed as `string` would
 * accept `viewClientss` and the server would answer `validation_failed` naming a field the form
 * never showed.
 *
 * **The four `*Users` permissions were replaced by one per target role.** `viewUsers` and
 * `editUsers` said "may this group touch accounts at all", which is a question about no
 * particular account - every page here draws rows of a *known* role, so a group allowed to run
 * the clients could still open, patch and delete an administrator. The replacement is a pair
 * per role: `viewClients`/`editClients`, `viewResellers`/`editResellers`,
 * `viewAdmins`/`editAdmins`. The two groups permissions are unchanged, because "may read the
 * group table" is not a claim about any particular group.
 *
 * A permission belongs to **exactly one role**, and on this installation that role is
 * `Admin` for all twenty-four - see `permissionsForRole` in `src/features/user-groups/permissions.ts`, which mirrors
 * `UserPermissionCatalog.ForRole` on the backend. A grant the group's role does not own is
 * dropped on the way in rather than refused, which is why a Client group sent all twenty-four comes
 * back with none: they would mean nothing, since the role gate ahead of them already says no.
 *
 * **The two stream permissions are the first pair that is not about accounts.**
 * `viewStreams`/`editStreams` ask about the configured streams, so they name no role of any
 * particular account - which is why they are a pair of their own rather than three. The
 * backend keeps them at 1024 and 2048, the first free values above the account pair.
 *
 * **The audio pair is the same question asked about another medium.**
 * `viewAudioStreams`/`editAudioStreams` gate the audio streams table, its form and its
 * import, and they are deliberately *not* the video pair: a group that may watch the video
 * streams may not touch the audio ones, and one that may edit the audio ones may not touch
 * the video ones. The backend keeps them at 4096 and 8192.
 *
 * **The series pair is the same question asked about the series catalogue.**
 * `viewSeries`/`editSeries` gate the series table, its form, the episodes and the season
 * form. They are deliberately not the on-demand pair: the screens, the rows and the
 * permissions are their own. The backend keeps them at 65536 and 131072.
 *
 * **The guide pair is the same question asked about the programme guide sources.**
 * `viewEpgSources`/`editEpgSources` gate the sources table, the channels that read a source
 * and the source form. A guide source is its own resource behind its own pair - a group may
 * run the streams and have no business pointing them at a different guide. The backend keeps
 * them at 262144 and 524288.
 *
 * **The bouquet pair is the same question asked about the packages.** `viewBouquets` and
 * `editBouquets` gate the bouquets table, the content manager behind it, and the bouquet
 * picker on the four content forms. They are a pair of their own rather than a reuse of any
 * content pair, because a bouquet is a package *of* the content rather than content: a
 * group that may run the streams and a group that may decide which packages they are sold
 * in are two different jobs. The backend keeps them at 1048576 and 2097152.
 *
 * **The tariff plan pair is the same question asked about the price list.** `viewTariffPlans`
 * and `editTariffPlans` gate the plans table and the plan form, which is where a plan's two
 * relations - the groups it may be sold to and the bouquets it delivers - are decided. The
 * pair is separate from the bouquet one because what a package costs and what a package
 * contains are decided by different jobs. The backend keeps them at 4194304 and 8388608.
 *
 * **The subscription pair is the same question asked about the rows a plan is sold as.**
 * `viewSubscriptions` and `editSubscriptions` gate the subscriptions table, its form and the
 * seven bulk operations. A subscription is not a plan: a plan says what a subscriber may buy,
 * while a subscription is one issued row that carries somebody's access, so pricing it and
 * issuing it are two different jobs. The backend keeps them at 16777216 and 33554432.
 *
 * Note the two questions are spelled the same way and are not the same question:
 * `permissionsForRole(role)` takes the role of the group *holding* the grant, while
 * `canView(granted, role)` takes the role of the account being *looked at*.
 */
export type UserPermission =
  | 'viewClients'
  | 'editClients'
  | 'viewResellers'
  | 'editResellers'
  | 'viewAdmins'
  | 'editAdmins'
  | 'viewUserGroups'
  | 'editUserGroups'
  | 'viewStreams'
  | 'editStreams'
  | 'viewAudioStreams'
  | 'editAudioStreams'
  | 'viewVideoOnDemand'
  | 'editVideoOnDemand'
  | 'viewSeries'
  | 'editSeries'
  | 'viewEpgSources'
  | 'editEpgSources'
  | 'viewBouquets'
  | 'editBouquets'
  | 'viewTariffPlans'
  | 'editTariffPlans'
  | 'viewSubscriptions'
  | 'editSubscriptions'

