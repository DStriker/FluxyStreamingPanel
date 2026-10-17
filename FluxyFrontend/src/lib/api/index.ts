/**
 * The endpoint list on top of `http.ts`, split by the part of the application it serves.
 *
 * Every consumer still writes `import { ... } from '../lib/api'`: this file is what that
 * specifier resolves to. The endpoints are a folder rather than one file because the file
 * had grown past fifteen hundred lines - a length at which a change to the reset flow and a
 * change to the streams table are read in the same document.
 */
export * from './common'
export * from './auth'
export * from './profile'
export * from './passwordReset'
export * from './sessions'
export * from './users'
export * from './userGroups'
export * from './contentCategories'
export * from './videoStreams'
