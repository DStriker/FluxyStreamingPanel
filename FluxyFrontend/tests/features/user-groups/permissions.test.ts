import { describe, expect, it } from 'vitest'
import {
  ALL_PERMISSIONS,
  canEdit,
  canEditAny,
  canView,
  canViewAny,
  permissionsForRole,
} from '../../../src/features/user-groups/permissions'
import type { UserPermission } from '../../../src/types'

/**
 * The permission table is the frontend's copy of `UserPermissionCatalog`, and these tests
 * pin the two facts the pages read it for: which boxes a group form may draw, and which
 * controls the accounts table may show. It is asserted here rather than only through a
 * render because every answer is a lookup over module-level maps - nothing a browser adds.
 */

describe('ALL_PERMISSIONS', () => {
  it('keeps the catalog order the server, seeder and migration use', () => {
    expect(ALL_PERMISSIONS).toEqual([
      'viewClients',
      'editClients',
      'viewResellers',
      'editResellers',
      'viewAdmins',
      'editAdmins',
      'viewUserGroups',
      'editUserGroups',
      'viewStreams',
      'editStreams',
      'viewAudioStreams',
      'editAudioStreams',
      'viewVideoOnDemand',
      'editVideoOnDemand',
      'viewSeries',
      'editSeries',
      'viewEpgSources',
      'editEpgSources',
      // The bouquet pair closes the content resources: a bouquet is the package *of* the four
      // content kinds rather than a fifth kind, so it sits after them and after the guide.
      'viewBouquets',
      'editBouquets',
      // The tariff plan pair follows the bouquets: it is the price list the packages are sold
      // under rather than a fifth package.
      'viewTariffPlans',
      'editTariffPlans',
      // The subscription pair closes the catalog: a subscription is the row a plan is sold *as*,
      // so it sits after the price list that decides what it delivers.
      'viewSubscriptions',
      'editSubscriptions',
    ])
  })
})

describe('permissionsForRole', () => {
  it('gives the admin level the whole catalog, in catalog order', () => {
    expect(permissionsForRole('Admin')).toEqual([...ALL_PERMISSIONS])
  })

  it('gives the other two levels nothing - which is a fact about the catalog, not a bug', () => {
    // A level decides which part of the API an account reaches at all; a permission only
    // answers a question asked inside that part. This is the precondition that keeps the
    // group form from drawing checkboxes whose every tick would be dropped.
    expect(permissionsForRole('Client')).toEqual([])
    expect(permissionsForRole('Reseller')).toEqual([])
  })
})

describe('canView / canEdit', () => {
  it('reads the permission of the role being acted on', () => {
    expect(canView(['viewClients'], 'Client')).toBe(true)
    expect(canView(['viewClients'], 'Admin')).toBe(false)
    expect(canEdit(['editAdmins'], 'Admin')).toBe(true)
    expect(canEdit(['editAdmins'], 'Client')).toBe(false)
  })

  it('does not read an edit grant as a view grant, or the other way round', () => {
    expect(canView(['editClients'], 'Client')).toBe(false)
    expect(canEdit(['viewClients'], 'Client')).toBe(false)
  })

  it('answers false to everything for an empty grant', () => {
    expect(canView([], 'Admin')).toBe(false)
    expect(canEdit([], 'Admin')).toBe(false)
  })
})

describe('canViewAny / canEditAny', () => {
  it('is true when some account role is covered', () => {
    expect(canViewAny(['viewResellers'])).toBe(true)
    expect(canEditAny(['editClients'])).toBe(true)
    expect(canViewAny([...ALL_PERMISSIONS])).toBe(true)
    expect(canEditAny([...ALL_PERMISSIONS])).toBe(true)
  })

  it('is false when nothing covers an account - including the three resource pairs', () => {
    // `viewStreams`/`editStreams`, `viewAudioStreams`/`editAudioStreams` and
    // `viewVideoOnDemand`/`editVideoOnDemand` sit in the catalog (a group form has to offer
    // them) but name configured content, not accounts, so they must not satisfy these gates.
    expect(canViewAny([])).toBe(false)
    expect(canEditAny([])).toBe(false)
    expect(canViewAny(['editAdmins'])).toBe(false)
    expect(canEditAny(['viewAdmins'])).toBe(false)
    expect(canViewAny(['viewStreams', 'editStreams'])).toBe(false)
    expect(canEditAny(['viewStreams', 'editStreams'])).toBe(false)
    expect(canViewAny(['viewAudioStreams', 'editAudioStreams'])).toBe(false)
    expect(canEditAny(['viewAudioStreams', 'editAudioStreams'])).toBe(false)
    expect(canViewAny(['viewVideoOnDemand', 'editVideoOnDemand'])).toBe(false)
    expect(canEditAny(['viewVideoOnDemand', 'editVideoOnDemand'])).toBe(false)
    expect(canViewAny(['viewEpgSources', 'editEpgSources'])).toBe(false)
    expect(canEditAny(['viewEpgSources', 'editEpgSources'])).toBe(false)
    // The bouquet pair names packages of content rather than accounts, so it must not
    // satisfy these gates either - the same rule the five resource pairs above follow.
    expect(canViewAny(['viewBouquets', 'editBouquets'])).toBe(false)
    expect(canEditAny(['viewBouquets', 'editBouquets'])).toBe(false)
    // The plan and subscription pairs name the price list and the issued rows rather than accounts,
    // so they must not satisfy these gates either.
    expect(canViewAny(['viewTariffPlans', 'editTariffPlans'])).toBe(false)
    expect(canEditAny(['viewTariffPlans', 'editTariffPlans'])).toBe(false)
    expect(canViewAny(['viewSubscriptions', 'editSubscriptions'])).toBe(false)
    expect(canEditAny(['viewSubscriptions', 'editSubscriptions'])).toBe(false)
  })

  it('takes every member of the union without a branch for a name that is not one', () => {
    // A compile-time property, checked here so a member added to the catalog without a
    // home in `VIEW_FOR`/`EDIT_FOR` fails a test rather than a page at runtime.
    const granted: UserPermission[] = [...ALL_PERMISSIONS]

    expect(canViewAny(granted)).toBe(true)
  })
})
