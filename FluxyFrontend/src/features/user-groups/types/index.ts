/**
 * The user-groups feature's response contracts — a group's row, its page of rows and its
 * detail — re-exported as types only so a consumer names the folder rather than a file.
 * `export type` because nothing here is a runtime value.
 */
export type { UserGroup } from './UserGroup'
export type { UserGroupDetail } from './UserGroupDetail'
export type { UserGroupList } from './UserGroupList'
