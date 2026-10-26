/**
 * One stream that reads its guide from a source, as
 * `GET /admin/epg-sources/{id}/channels` answers with it.
 *
 * Read-only and derived: nothing here is a stored row. The pair a stream carries - the source
 * it names and the key it is filed under inside that guide - is what makes it a channel of
 * the source, and this is the projection of that pair.
 */
export interface EpgSourceChannel {
  /** Identifier of the stream that uses the source, as text. */
  streamId: string
  /** Display name of that stream, which is what the table draws. */
  streamName: string
  /** Channel key the stream is filed under inside this guide. */
  epgChannelKey: string
}
