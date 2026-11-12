/**
 * The four playback domains the replacement reaches, as the wire spells them.
 *
 * A closed union rather than `string`: the answer always carries all four, in this order, so a
 * screen can map each to its label without a fallback for a value the server invented. It is
 * declared beside the count that carries it and re-exported from there, because the refusal
 * names one of the same four values and a second copy of the list would be a second place for
 * the spelling to drift.
 */
export type SourceReplacementDomain =
  | 'videoStreams'
  | 'audioStreams'
  | 'videoOnDemand'
  | 'seriesEpisodes'

/**
 * How one playback domain ended a run: entities changed, candidate addresses left unchanged.
 *
 * `updated` counts entities, `skipped` counts addresses - the same asymmetry the server
 * documents, and the reason the page labels the two counters differently.
 */
export interface SourceReplacementDomainResult {
  domain: SourceReplacementDomain
  updated: number
  skipped: number
}
