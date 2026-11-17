/**
 * One entry of the profile picker the four content forms draw: the identifier and the name it
 * is chosen by.
 *
 * The endpoint that answers this list is gated by any of the four content-view grants rather
 * than by the profile pair, because choosing a profile while editing a stream is part of
 * editing the stream.
 */
export interface TranscodingProfileOption {
  /** Identifier of the profile, as text. */
  id: string
  /** Display name. */
  name: string
}
