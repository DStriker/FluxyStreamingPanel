/**
 * The API request failure log's response contracts - the reason and content-kind
 * vocabularies, one row and the page it arrives in - re-exported as types only so a consumer
 * names the folder rather than a file. `export type` because nothing here is a runtime value.
 */
export type { ApiRequestFailureContentKind } from './ApiRequestFailureContentKind'
export type { ApiRequestFailureReason } from './ApiRequestFailureReason'
export type { ApiRequestFailureRow } from './ApiRequestFailureRow'
export type { ApiRequestFailureRowList } from './ApiRequestFailureRowList'
