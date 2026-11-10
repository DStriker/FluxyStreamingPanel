/**
 * One entry of the stream problem journal, as `GET /admin/stream-problems` answers with it.
 *
 * The row is one problem the stream producer reported: which stream it was about, what the
 * producer said, and when. `streamName` is resolved through a join **on read**, never stored
 * on the row, so a renamed stream draws its current name; under the schema's foreign key it is
 * never null.
 *
 * `id` and `streamId` are text because a JavaScript client parses a uuid as a number
 * otherwise. They are unrelated: the first is this record's own key, the second the stream the
 * problem is about - which is why the stream cell shows only the name.
 */
export interface StreamProblemRow {
  /** Identifier of the problem record, as text. */
  id: string
  /** Identifier of the stream the problem is about, as text. */
  streamId: string
  /** Display name of that stream, resolved by the join on read. */
  streamName: string
  /** Failure message as the producer stored it. */
  errorMessage: string
  /** When the problem was recorded, as an ISO timestamp. */
  createdAt: string
}
