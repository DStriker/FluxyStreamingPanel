/**
 * The state of a server, as `GET /admin/servers` spells it on the wire: the name of the
 * backend's enum member, in full and capitalised - the spelling the stream responses use.
 *
 * It is not an ordered scale: the two members are states of one server, never levels, so a
 * comparison between them says nothing and every branch here is an equality.
 */
export type ServerStatus = 'Active' | 'Disabled'
