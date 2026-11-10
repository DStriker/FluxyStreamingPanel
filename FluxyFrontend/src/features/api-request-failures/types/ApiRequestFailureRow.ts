import type { ApiRequestFailureContentKind } from './ApiRequestFailureContentKind'
import type { ApiRequestFailureReason } from './ApiRequestFailureReason'

/**
 * One entry of the API request failure log, as `GET /admin/api-request-failures` answers with
 * it.
 *
 * The row is one refused public-API request: why it was refused, what subscription data and
 * content it named, and the network it came from.
 *
 * **The content snapshot is stored, never resolved.** `contentKind`, `contentId` and
 * `contentName` are null *as a group*: a refusal that named no content carries none of them.
 * When they are present they are what the write path captured - the name stays legible after
 * the named row is gone, which is why no read path joins a content table. `contentId` is an
 * opaque identifier, not a foreign key, and it says nothing about whether that row still
 * exists.
 *
 * `reason` is the snake_case wire name the filter also accepts, so the client never learns
 * two spellings of one fact. `description` is the sentence the write path stored, in English:
 * it is the auditable record rather than a rendering of the reader's language, and the search
 * box matches it.
 */
export interface ApiRequestFailureRow {
  /** Identifier of the failure record, as text. */
  id: string
  /** Free-form access parameter the refused request named, or null. */
  subscriptionData: string | null
  /** Kind of content the request named, or null together with the two fields below. */
  contentKind: ApiRequestFailureContentKind | null
  /** Opaque identifier of that content, as text, or null. */
  contentId: string | null
  /** Name of that content as it read at write time, or null. */
  contentName: string | null
  /** Why the request was refused, as its wire name. */
  reason: ApiRequestFailureReason
  /** Address the request came from. */
  ip: string
  /** Provider the address belonged to, or null. */
  isp: string | null
  /** Upper-case ISO 3166-1 alpha-2 country of the address, or null. */
  countryCode: string | null
  /** English sentence describing the refusal. */
  description: string
  /** When the record was written, as an ISO timestamp. */
  createdAt: string
}
