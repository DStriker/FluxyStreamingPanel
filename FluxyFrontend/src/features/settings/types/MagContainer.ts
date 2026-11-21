/**
 * The container the MAG portal delivers by default, as the response spells it.
 *
 * `mpeg-ts` is the canonical spelling the GET answers with; the PATCH also accepts `mpegts`,
 * but a screen that read or wrote that spelling would be inventing a second name for one
 * value. The selector is built from this union alone.
 */
export type MagContainer = 'mpeg-ts' | 'hls'
