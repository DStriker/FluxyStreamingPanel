import type { BulkItemResponse } from './BulkItemResponse'

/**
 * The answer to a bulk operation: what the whole run was called, how much of it landed, and
 * how each row ended - the frontend's copy of `BulkOperationResponse`.
 *
 * **Always a `200` from the server, and that is deliberate rather than optimistic.** What
 * happened to a particular row lives in `items`, because a status code can only describe the
 * request as a whole - and the request as a whole *was* carried out: every identifier it
 * named came back with a verdict. Folding the worst verdict into the status would make "nine
 * accounts blocked, one of them was mine" indistinguishable from "nothing happened", and this
 * page would be unable to redraw the list without guessing.
 *
 * So the two halves have two jobs: `code`/`message` say the run happened (and, through
 * `succeeded`/`failed`, how much of it landed), and `items` says what is true of each row.
 */
export interface BulkOperationResponse {
  /** Machine readable outcome of the run as a whole. */
  code: string
  /** English fallback sentence for the run as a whole. */
  message: string
  /** How many rows the operation actually changed. */
  succeeded: number
  /** How many rows were refused, and why - each reason in `items`. */
  failed: number
  /** One entry per identifier the request named, in the order it was named. */
  items: BulkItemResponse[]
}
