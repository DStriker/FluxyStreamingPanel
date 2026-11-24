/**
 * One entry of the server picker the subscription and stream forms draw: the identifier, the
 * name it is chosen by, and the one flag the stream form filters on.
 *
 * The endpoint that answers this list is gated by an "any of" policy over the subscription and
 * stream read grants rather than by the server pair, because choosing a server while editing
 * either form is part of editing it.
 */
export interface ServerOption {
  /** Identifier of the server, as text. */
  id: string
  /** Display name. */
  name: string
  /** Whether the server only serves timeshifted content. */
  timeshiftOnly: boolean
}
