/**
 * The six kinds of stream, spelled exactly as the backend's `VideoStreamType` spells them.
 *
 * A union rather than `string` so that a seventh member added on the server is a compile
 * error at the form that offers the six rather than a value the endpoint refuses with
 * `validation_failed`. The members arrive on every row as the enum member's own name -
 * `Permanent`, `VodPlaylist` - the same spelling every other enum on this API uses.
 *
 * Like `UserStatus` and `ContentType`, this is **not an ordered scale**: the members are
 * kinds of stream, not levels of anything, so nothing anywhere may compare two of them.
 * Their numeric values on the backend are fixed and must never be renumbered, but those
 * values never reach this side - the wire carries the names.
 *
 * The type is also what decides which payload a form may fill in: a URL list for
 * `Permanent`, `OnDemand` and `Redirect`, a series reference for `OnlineSeries`, a single
 * on-demand reference for `VodPlaylist`, a list of files for `FilePlaylist`. That rule is
 * the server's, and this union is what lets the form draw the right fields before a request
 * is made rather than after one is refused.
 */
export type VideoStreamType =
  | 'Permanent'
  | 'OnDemand'
  | 'Redirect'
  | 'OnlineSeries'
  | 'VodPlaylist'
  | 'FilePlaylist'
