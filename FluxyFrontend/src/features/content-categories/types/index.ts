/**
 * The content-categories feature's response contracts — one kind of content and the
 * categories filed under it — re-exported as types only so a consumer names the folder
 * rather than a file. `export type` because nothing here is a runtime value.
 */
export type { ContentCategory } from './ContentCategory'
export type { ContentCategoryList } from './ContentCategoryList'
export type { ContentType } from './ContentType'
