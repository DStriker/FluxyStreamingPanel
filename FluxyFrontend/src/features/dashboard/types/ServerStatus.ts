/**
 * The state of a server, as `GET /admin/dashboard` spells it on the wire: the name of the
 * backend's enum member, in full and capitalised.
 *
 * This is an **own copy** rather than an import from `features/servers`: a feature imports
 * neither another feature nor a page, and the dashboard's answer is its own contract even
 * where a field happens to share a spelling with the servers table.
 *
 * It is not an ordered scale - `Active` and `Disabled` are states of one server, never
 * levels - so every branch in this feature is an equality.
 */
export type ServerStatus = 'Active' | 'Disabled'
