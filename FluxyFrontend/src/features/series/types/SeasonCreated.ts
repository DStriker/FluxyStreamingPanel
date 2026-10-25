/**
 * What a successful season creation answers: the count of episode rows written and the range
 * their numbers cover.
 */
export interface SeasonCreated {
  /** Code reported once the season has been created. */
  code: string
  /** Human-readable confirmation. */
  message: string
  /** Series the episodes were filed under, as text. */
  seriesId: string
  /** Season number every created episode carries. */
  seasonNumber: number
  /** How many episode rows were created. */
  addedCount: number
  /** Number of the first created episode, always one. */
  firstEpisodeNumber: number
  /** Number of the last created episode. */
  lastEpisodeNumber: number
}
