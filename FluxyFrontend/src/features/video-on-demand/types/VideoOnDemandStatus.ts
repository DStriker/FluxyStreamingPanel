/**
 * The two states an on-demand video can be in, spelled exactly as the backend's
 * `VodStatus` spells them.
 *
 * Two descriptions, not a ramp - the same reasoning `UserStatus` and `VideoStreamStatus`
 * carry. A video is published or it is not, and neither state is "more" of anything.
 */
export type VideoOnDemandStatus = 'Inactive' | 'Active'
