import { describe, expect, it } from 'vitest'
import { groupKeyForPath, menuItemPath, menuPath } from '../../src/utils/navigation'

/**
 * The rail's address rules, asserted as functions rather than through a render.
 *
 * `AccountSidebar` uses both of these on every page: `menuPath` builds the key antd
 * selects and `groupKeyForPath` decides which category is open. A render cannot reach the
 * second half meaningfully - every probe lands before the first effect - so the path rules
 * themselves are checked here, the way the other navigation-adjacent mirrors are.
 */

describe('menuPath', () => {
  it('builds a section address under the role area', () => {
    expect(menuPath('Admin', 'users')).toBe('/admin/users')
    expect(menuPath('Client', 'orders')).toBe('/client/orders')
    expect(menuPath('Reseller', 'sessions/active')).toBe('/reseller/sessions/active')
  })

  it('takes the landing page for the item with no path of its own', () => {
    expect(menuPath('Admin', '')).toBe('/admin/dashboard')
    expect(menuPath('Client', '')).toBe('/client/index')
  })
})

describe('menuItemPath', () => {
  it('opens the section an item key names, without repeating its segment', () => {
    expect(menuItemPath('Admin', 'videoStreamsManage')).toBe('/admin/video-streams')
    expect(menuItemPath('Admin', 'streamProblems')).toBe('/admin/stream-problems')
  })

  it('answers null when the role has no such item', () => {
    expect(menuItemPath('Client', 'videoStreamsManage')).toBeNull()
  })
})

describe('groupKeyForPath', () => {
  it('names the category of each menu page', () => {
    expect(groupKeyForPath('Admin', '/admin/dashboard')).toBe('overview')
    expect(groupKeyForPath('Admin', '/admin/users')).toBe('users')
    expect(groupKeyForPath('Admin', '/admin/subscriptions')).toBe('subscriptions')
    expect(groupKeyForPath('Admin', '/admin/video-streams/categories')).toBe('videoStreams')
    expect(groupKeyForPath('Client', '/client/sessions/active')).toBe('account')
  })

  it('owns addresses that have no menu item of their own', () => {
    // The edit forms are `extra` routes: the table's row action opens them, and a reload
    // there must still leave the category that contains their list expanded.
    expect(groupKeyForPath('Admin', '/admin/users/8f3c1e2a')).toBe('users')
    // The subscription edit form is the same shape, and the subscriptions table owns it.
    expect(groupKeyForPath('Admin', '/admin/subscriptions/8f3c1e2a')).toBe('subscriptions')
    expect(groupKeyForPath('Admin', '/admin/user-groups/add')).toBe('users')
    expect(groupKeyForPath('Admin', '/admin/video-streams/8f3c1e2a')).toBe('videoStreams')
    // The refused-request log mirrors the backend's own address (`admin/api-request-failures`)
    // instead of nesting under the group's prefix - the credit-logs treatment - so the
    // three-list group stays open on it, and the old nested address no longer owns anything.
    expect(groupKeyForPath('Admin', '/admin/api-request-failures')).toBe('blacklists')
    expect(groupKeyForPath('Admin', '/admin/blacklists/api-request-failures')).toBeNull()
  })

  it('ignores a trailing slash, as the sidebar does before matching', () => {
    expect(groupKeyForPath('Admin', '/admin/users/')).toBe('users')
  })

  it('requires a whole path segment rather than a string prefix', () => {
    // `/admin/users` must not claim a sibling whose name merely starts with it - the
    // boundary slash is the difference between ownership and a substring.
    expect(groupKeyForPath('Admin', '/admin/usersomething')).toBeNull()
  })

  it('answers null for a path no category covers', () => {
    expect(groupKeyForPath('Admin', '/admin/unknown')).toBeNull()
  })
})
