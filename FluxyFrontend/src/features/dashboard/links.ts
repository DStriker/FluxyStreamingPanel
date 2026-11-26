/**
 * The two stream states the video streams page filters on, spelled as the wire names them.
 *
 * Declared here rather than imported from `features/video-streams`: features do not import
 * features, and the dashboard only ever links to one of the two.
 */
type VideoStreamStatus = 'Running' | 'Stopped'

/**
 * The two addresses the dashboard links into, built here rather than in the page so a
 * change to either shape is one edit and a unit test can pin both.
 *
 * The area is the signed-in prefix (`/admin`), and every path is relative to it - exactly
 * the rule `areaForRole` states. The status is sent with a **capital S** (`?status=Stopped`)
 * because the video streams page does an exact enum membership test; a lower-case value
 * would be dropped and the link would arrive unfiltered, looking as if the click did
 * nothing.
 */

/**
 * The live connections page, optionally narrowed to one server.
 *
 * `serverId` is the real `server_id` column: rows the capture path wrote without one are
 * the unattributed tail and no filter value can select them. The identifier is encoded even
 * though the contract says uuid, because a hand-edited address must not smuggle a second
 * parameter through the query string.
 */
export const liveConnectionsPath = (area: string, serverId?: string): string =>
  serverId
    ? `${area}/live-connections?serverId=${encodeURIComponent(serverId)}`
    : `${area}/live-connections`

/**
 * The video streams page, optionally narrowed to one status.
 *
 * The caller passes the wire spelling (`'Stopped'`), never a translated word: the page
 * compares the parameter against its enum, not against a label.
 */
export const videoStreamsPath = (area: string, status?: VideoStreamStatus): string =>
  status
    ? `${area}/video-streams?status=${encodeURIComponent(status)}`
    : `${area}/video-streams`
