/**
 * The two kinds of audio stream, spelled exactly as the backend's `AudioStreamType` spells
 * them.
 *
 * A union rather than `string` so that a third member added on the server is a compile
 * error at the form that offers the two rather than a value the endpoint refuses with
 * `validation_failed`. The members arrive on every row as the enum member's own name -
 * `OnDemand`, `Redirect` - the same spelling every other enum on this API uses.
 *
 * Like `UserStatus` and `ContentType`, this is **not an ordered scale**: the members are
 * kinds of stream, not levels of anything, so nothing anywhere may compare two of them.
 * Their numeric values on the backend are fixed and must never be renumbered, but those
 * values never reach this side - the wire carries the names.
 *
 * **Both kinds take the same payload**, and that is the whole difference from a video
 * stream: there is no kind here whose payload another kind can switch off, so the form
 * always draws the one ordered list of sources and a bulk type change clears nothing.
 */
export type AudioStreamType = 'OnDemand' | 'Redirect'
