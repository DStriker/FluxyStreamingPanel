/**
 * Which kind of content a refused request named, as the endpoint spells it.
 *
 * The four values are the `ContentType` member names - `VideoStream`, `AudioStream`,
 * `VideoOnDemand`, `Series` - which is the spelling the content-categories contract already
 * speaks. The column reuses this installation's vocabulary rather than inventing a fifth
 * "failure" kind: the public API serves exactly those four catalogues.
 *
 * It is a union rather than `string` because the three content fields are null together, and
 * a renderer that branches on this value has to see every member. The snapshot is stored,
 * never resolved, so knowing the kind here says nothing about a table this screen could join.
 */
export type ApiRequestFailureContentKind =
  | 'VideoStream'
  | 'AudioStream'
  | 'VideoOnDemand'
  | 'Series'
