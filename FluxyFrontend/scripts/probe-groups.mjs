/**
 * Exercises the user-group API against the running backend, through the app's own modules.
 *
 * Written because the group feature is the first place this application has two
 * independent gates on one endpoint - a **role** and a **permission** - and the two fail in
 * different ways. A page can look perfectly correct while the permission it demands is the
 * wrong one, or while a rule the server enforces (a base group may only be renamed, a group
 * with members may not be deleted) is being relied on by a button that happens to be
 * disabled for some other reason. Reading the code cannot tell those apart; asking the
 * server can.
 *
 * It needs two accounts rather than one, because the whole point is the difference between
 * them:
 *
 *   - one in a group holding **every** permission its role owns (`historyadmin`, in the base
 *     `Admins` group) - everything below must succeed for it;
 *   - one holding `viewClients` and `editClients` and **nothing else** (`admintest`, in the
 *     `Limited admins` group) - the group endpoints must refuse it with `permission_denied`,
 *     and an administrator's row must be refused the same way while the client rows keep
 *     working.
 *
 * That second account is the check this probe exists for. A single-account probe cannot see
 * a permission gate at all: everything it tries is allowed.
 *
 * Both passwords are the same throwaway value, set directly on the two rows (see the
 * backend's `users` table). Set FLUXY_ADMIN_PASSWORD to it.
 *
 * Run: npm run probe:groups
 */
import { config } from '../src/config/index'
import { apiFetch } from '../src/lib/http'
import {
  createUserGroup,
  deleteUserGroup,
  getProfile,
  getUser,
  getUserGroup,
  getUserGroups,
  getUsers,
  submitAuth,
  updateUserGroup,
} from '../src/lib/api'
import { getCsrfToken } from '../src/lib/csrf'

const BASE = process.env.FLUXY_API ?? 'http://localhost:5159'
config.API_BASE_URL = BASE

/** The account whose group grants everything its role owns. */
const FULL_USER = process.env.FLUXY_GROUP_ADMIN_USER ?? 'historyadmin'
/** The account whose group grants `viewClients` and `editClients` and nothing more. */
const LIMITED_USER = process.env.FLUXY_GROUP_LIMITED_USER ?? 'admintest'
const PASSWORD = process.env.FLUXY_ADMIN_PASSWORD ?? null

const results = []
const record = (name, detail, ok = true) => {
  // Both arguments after the name are easy to swap, and swapping them is silent: a non-empty
  // string is truthy, so `record(name, <verdict>, <detail>)` used to push a string as the
  // verdict and print PASS for a check that had just failed. Twelve of this file's checks were
  // in that order - among them the whole of the limited-account half - and a run that reported
  // every one of them green proved nothing about them. Refusing a non-boolean verdict turns
  // the next mix-up into a crash on the line it happened rather than a reassuring PASS.
  if (typeof ok !== 'boolean') {
    throw new TypeError(
      `record(): the third argument must be the verdict, got ${typeof ok} (${JSON.stringify(ok)}) - "${name}"`,
    )
  }
  results.push({ name, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}\n      ${detail}`)
}

// ---------------------------------------------------------------- cookie jar
//
// Node has no cookie jar and no `document.cookie`; both are emulated with the browser's
// rules so `getCsrfToken`'s cookie short-circuit can fire and the antiforgery pair is the
// pair a browser would send. HttpOnly cookies are kept out of the page side, which is the
// split that makes the emulation honest rather than merely convenient.
const jar = new Map()
const visible = new Map()

globalThis.document = {
  get cookie() {
    return [...visible].map(([k, v]) => `${k}=${v}`).join('; ')
  },
}

const realFetch = globalThis.fetch
globalThis.fetch = async (url, init = {}) => {
  const cookie = [...jar].map(([k, v]) => `${k}=${v}`).join('; ')
  const response = await realFetch(url, {
    ...init,
    headers: { ...(init.headers ?? {}), ...(cookie ? { Cookie: cookie } : {}) },
  })

  for (const raw of response.headers.getSetCookie?.() ?? []) {
    const [pair, ...attrs] = raw.split(';')
    const idx = pair.indexOf('=')
    if (idx <= 0) continue

    const name = pair.slice(0, idx).trim()
    const value = pair.slice(idx + 1)
    const httpOnly = attrs.some((a) => a.trim().toLowerCase() === 'httponly')

    // An empty value is how this backend deletes a cookie, and replaying one the server has
    // just cleared would make the probe stricter than a browser and hide the very failure
    // it is here for.
    if (value === '') {
      jar.delete(name)
      visible.delete(name)
      continue
    }

    jar.set(name, value)
    if (httpOnly) visible.delete(name)
    else visible.set(name, value)
  }

  return response
}

// ---------------------------------------------------------------- helpers

const call = async (fn, payload) => {
  try {
    return { ok: true, data: await fn(payload) }
  } catch (err) {
    return { ok: false, err }
  }
}

const refused = (r, code, status) => !r.ok && r.err.code === code && r.err.status === status

const refusedAs = (r) => (r.ok ? `answered code=${r.data?.code}` : `${r.err.status} ${r.err.code}`)

/** What every write in this application does first: mint a token at call time. */
const csrf = async () => getCsrfToken()

const signIn = async (username) => {
  const token = await getCsrfToken()
  return call(submitAuth, {
    action: 'admin-login',
    values: { username, password: PASSWORD },
    csrfToken: token,
    captchaToken: null,
  })
}

// ---------------------------------------------------------------- reachability

console.log(`\nAPI base: ${config.API_BASE_URL}\nfull: ${FULL_USER}\nlimited: ${LIMITED_USER}\n`)

if (!PASSWORD) {
  console.log('SKIP  FLUXY_ADMIN_PASSWORD is not set - the group checks cannot run.')
  console.log('      Set it to the throwaway password of both accounts above.')
  process.exit(0)
}

// ---------------------------------------------------------------- the full account

const full = await signIn(FULL_USER)

if (!full.ok && full.err.code === 'login_rate_limited') {
  console.log('\nSKIP  the sign-in window is full (429) - the group checks cannot run.')
  console.log(
    `      Clear it and re-run:\n` +
      `      docker exec redis redis-cli --user fluxy -a <REDIS_PASSWORD> DEL 'fluxy:throttle:login:Admin:::1'`,
  )
  process.exit(0)
}

record(
  `${FULL_USER} signs in`,
  full.ok ? `code=${full.data.code}` : refusedAs(full),
  full.ok,
)
if (!full.ok) process.exit(1)

// The list is the first thing the table asks for, and the first place a permission gate
// would show: the wrong policy here is a page that renders and then fails to fill.
const listed = await call(getUserGroups, { page: 1, pageSize: 100 })
record(
  'the account holding every permission lists the groups',
  listed.ok
    ? `${listed.data.total} groups, first: ${listed.data.items[0]?.name}`
    : refusedAs(listed),
  listed.ok,
)
if (!listed.ok) process.exit(1)

const groups = listed.data.items
const baseAdmins = groups.find((g) => g.role === 'Admin' && g.isBase)
const baseClients = groups.find((g) => g.role === 'Client' && g.isBase)
const limited = groups.find((g) => g.name === 'Limited admins')

record(
  'the three base groups are there and only those are marked as base',
  `${groups.length} groups, ${groups.filter((g) => g.isBase).length} of them base`,
  groups.filter((g) => g.isBase).length === 3 && !!baseAdmins && !!baseClients,
)

record(
  'the base Admin group grants every permission its role owns',
  `${baseAdmins?.name} grants ${baseAdmins?.permissionsCount}`,
  baseAdmins?.permissionsCount === 8,
)

record(
  'the base Client group grants none, and says so as zero rather than as a gap',
  `${baseClients?.name} grants ${baseClients?.permissionsCount}`,
  baseClients?.permissionsCount === 0,
)

// The detail the edit form paints from. `members` is the number that tells an administrator
// whether the `409 user_group_in_use` below is still theoretical.
const detail = await call(getUserGroup, baseAdmins.id)
record(
  'the base group detail arrives with its permissions as names and its member count',
  detail.ok
    ? `isBase=${detail.data.isBase}, ${detail.data.permissions.length} permissions, ${detail.data.members} members`
    : refusedAs(detail),
  detail.ok && detail.data.isBase && detail.data.members >= 1,
)

// --------------------------------------------------------------- create

const created = await call(createUserGroup, {
  values: {
    name: 'Probe group',
    role: 'Admin',
    status: 'Registered',
    permissions: ['viewClients', 'editClients'],
  },
  csrfToken: await csrf(),
})
record(
  'a new group is created',
  created.ok ? `code=${created.data.code}` : refusedAs(created),
  created.ok,
)

const createdId = created.ok ? (await getUserGroups({ page: 1, pageSize: 100, search: 'Probe group' })).items[0]?.id : null

const duplicate = await call(createUserGroup, {
  values: { name: 'Probe group', role: 'Client', status: 'Registered', permissions: [] },
  csrfToken: await csrf(),
})
record(
  'a second group with the same name is refused with 409',
  refusedAs(duplicate),
  refused(duplicate, 'user_group_already_exists', 409),
)

const unknownPermission = await call(createUserGroup, {
  values: { name: 'Probe unknown', role: 'Admin', status: 'Registered', permissions: ['viewEverything'] },
  csrfToken: await csrf(),
})
record(
  'a permission name that does not exist is refused, not dropped',
  refusedAs(unknownPermission),
  refused(unknownPermission, 'validation_failed', 400),
)

// The asymmetry the catalog is built on: a name that does not exist is a client that is
// wrong about the contract and is told so, while a *valid* permission the chosen role does
// not own is simply not granted - because that is a client describing a group, and the
// server already knows what a Client group may hold.
const wrongRole = await call(createUserGroup, {
  values: {
    name: 'Probe client',
    role: 'Client',
    status: 'Registered',
    permissions: ['viewClients'],
  },
  csrfToken: await csrf(),
})
const wrongRoleDetail = wrongRole.ok
  ? await getUserGroup((await getUserGroups({ page: 1, pageSize: 100, search: 'Probe client' })).items[0].id)
  : null
record(
  'a permission the chosen role does not own is dropped rather than refused',
  wrongRole.ok
    ? `created with ${wrongRoleDetail?.permissions.length} permissions`
    : refusedAs(wrongRole),
  wrongRole.ok && wrongRoleDetail?.permissions.length === 0,
)

// Its identifier, kept so the cleanup at the end of the probe can take it back out: a probe
// that leaves a row behind turns its own fixtures into the next run's fixtures, and the
// "a second group with the same name is refused" check above would start refusing for the
// wrong reason.
const wrongRoleId = wrongRole.ok ? wrongRoleDetail.id : null

// --------------------------------------------------------------- the base group's walls

const renameBase = await call(updateUserGroup, {
  id: baseAdmins.id,
  patch: { name: baseAdmins.name },
  csrfToken: await csrf(),
})
record(
  'a base group may be renamed',
  renameBase.ok ? `code=${renameBase.data.code}` : refusedAs(renameBase),
  renameBase.ok,
)

const reshapedBase = await call(updateUserGroup, {
  id: baseAdmins.id,
  patch: { role: 'Client' },
  csrfToken: await csrf(),
})
record(
  'a base group may not have its role changed: 409 user_group_immutable',
  refusedAs(reshapedBase),
  refused(reshapedBase, 'user_group_immutable', 409),
)

const strippedBase = await call(updateUserGroup, {
  id: baseAdmins.id,
  patch: { permissions: [] },
  csrfToken: await csrf(),
})
record(
  'a base group may not have its permissions stripped: 409 user_group_immutable',
  refusedAs(strippedBase),
  refused(strippedBase, 'user_group_immutable', 409),
)

const deletedBase = await call(deleteUserGroup, { id: baseClients.id, csrfToken: await csrf() })
record(
  'a base group may not be deleted: 409 user_group_immutable',
  refusedAs(deletedBase),
  refused(deletedBase, 'user_group_immutable', 409),
)

// `Limited admins` has a member, so this is the refusal the member count on the detail page
// is there to predict.
const inUse = await call(deleteUserGroup, { id: limited.id, csrfToken: await csrf() })
record(
  'a group with members may not be deleted: 409 user_group_in_use',
  refusedAs(inUse),
  refused(inUse, 'user_group_in_use', 409),
)

const missing = await call(deleteUserGroup, {
  id: '99999999-9999-9999-9999-999999999999',
  csrfToken: await csrf(),
})
record(
  'an identifier nobody answers to is 404 user_group_not_found',
  refusedAs(missing),
  refused(missing, 'user_group_not_found', 404),
)

// No antiforgery token at all, on a write that has one: the check runs before any of the
// rules above, so this must not be a 409 wearing a different hat.
const noCsrf = await apiFetch('/admin/user-groups', {
  method: 'POST',
  body: { name: 'Probe no csrf', role: 'Client', status: 'Registered', permissions: [] },
  csrfToken: null,
}).then(
  (data) => ({ ok: true, data }),
  (err) => ({ ok: false, err }),
)
record(
  'a write without an antiforgery token is refused with 400 csrf_invalid',
  refusedAs(noCsrf),
  refused(noCsrf, 'csrf_invalid', 400),
)

// --------------------------------------------------------------- the accounts side

const accounts = await call(getUsers, { page: 1, pageSize: 20, groupId: limited.id })
record(
  'the accounts list filters by a group identifier',
  accounts.ok
    ? `${accounts.data.total} accounts in "${limited.name}", all of them: ${accounts.data.items.every((u) => u.groupId === limited.id)}`
    : refusedAs(accounts),
  accounts.ok && accounts.data.total >= 1 && accounts.data.items.every((u) => u.groupId === limited.id),
)

// A row the limited account must not be able to open. `historyadmin` is an administrator and
// `Limited admins` may reach only the clients, so its identifier is the shape of the refusal
// the permission split exists for: not "there is no such account" but "not the role you may
// read". Looked up while the full account is still signed in, because the list a limited
// caller receives is already filtered down to the roles their group holds.
const adminRows = await call(getUsers, {
  page: 1,
  pageSize: 100,
  groupId: baseAdmins?.id,
})
const adminAccountId = adminRows.ok ? (adminRows.data.items[0]?.id ?? null) : null

// The group the probe just made has no members, which is exactly what makes it deletable.
const deletable = await call(deleteUserGroup, { id: createdId, csrfToken: await csrf() })
record(
  'an empty group of one’s own is deleted',
  deletable.ok ? `code=${deletable.data.code}` : refusedAs(deletable),
  deletable.ok,
)

// The other fixture, taken back out for the same reason. It is empty too, so this is the
// one path a *client* group can be removed by - and a second run that found it still there
// would answer its own duplicate-name check with a 409 it had caused itself.
const removedFixture = wrongRoleId
  ? await call(deleteUserGroup, { id: wrongRoleId, csrfToken: await csrf() })
  : null
record(
  'the second fixture is cleaned up as well',
  removedFixture === null || removedFixture.ok,
  removedFixture === null ? 'nothing to remove' : refusedAs(removedFixture),
)

// --------------------------------------------------------------- the limited account
//
// The half of the feature that a single-account probe cannot see. This account holds
// `viewClients` and `editClients` and nothing else, so the client rows must keep working
// while every group endpoint refuses it - and while an administrator's row is refused by the
// per-target check inside the service rather than by the policy on the endpoint. Every
// refusal here must be `permission_denied`, not `invalid_credentials` or a 404, because the
// difference between "you may not" and "there is no such thing" is the difference between an
// operator asking their administrator for access and an operator filing a bug.

jar.clear()
visible.clear()

const limitedSignIn = await signIn(LIMITED_USER)
record(
  `${LIMITED_USER} signs in`,
  limitedSignIn.ok ? `code=${limitedSignIn.data.code}` : refusedAs(limitedSignIn),
  limitedSignIn.ok,
)

if (limitedSignIn.ok) {
  const stillAccounts = await call(getUsers, { page: 1, pageSize: 20 })
  record(
    'the limited account can still read the accounts list',
    stillAccounts.ok
      ? `${stillAccounts.data.total} accounts`
      : refusedAs(stillAccounts),
    stillAccounts.ok,
  )

  // The list is not merely reachable, it is *narrow*: `VisibleRoles` is applied before the
  // count, so a total over rows the caller may not open would be a pager lying about the
  // pages behind it.
  record(
    'every row it is shown belongs to a role its group may reach',
    stillAccounts.ok
      ? `${stillAccounts.data.items.length} drawn of ${stillAccounts.data.total}, every one ${[...new Set(stillAccounts.data.items.map((u) => u.role))].join(', ') || '(none)'}`
      : refusedAs(stillAccounts),
    stillAccounts.ok && stillAccounts.data.items.every((u) => u.role === 'Client'),
  )

  // The browser needs the same two answers, and this is where the second one comes from.
  // Asserted here rather than by a render because the pages never draw these buttons under
  // a static probe - they arrive before the profile call does.
  const ownProfile = await call(getProfile)
  record(
    'the profile reports exactly the two grants its group holds',
    ownProfile.ok
      ? `permissions=[${ownProfile.data.permissions.join(', ')}]`
      : refusedAs(ownProfile),
    ownProfile.ok &&
      ownProfile.data.permissions.length === 2 &&
      ownProfile.data.permissions.includes('viewClients') &&
      ownProfile.data.permissions.includes('editClients'),
  )

  // The per-target check that replaced one `editUsers` bit for accounts in general. The
  // policy on the endpoint is coarse on purpose - it settles "may this caller read accounts
  // at all" - and the refusal for a particular row is the service's, so this is the only
  // place the difference is visible from outside. Recorded either way: a fixture that
  // produced no administrator row would otherwise leave the check simply absent from the
  // run, and a missing check reads exactly like a passing one in the total.
  const refusedAdminRow = adminAccountId ? await call(getUser, adminAccountId) : null
  record(
    'the limited account is refused an administrator’s row with permission_denied',
    adminAccountId === null
      ? `no administrator row to try: ${adminRows.ok ? 'the base Admin group has no members' : refusedAs(adminRows)}`
      : refusedAdminRow.ok
        ? 'the row was READ, which viewClients must not allow'
        : refusedAs(refusedAdminRow),
    adminAccountId !== null && refused(refusedAdminRow, 'permission_denied', 403),
  )

  const refusedList = await call(getUserGroups, { page: 1, pageSize: 100 })
  record(
    'the limited account is refused the group list with permission_denied',
    refusedAs(refusedList),
    refused(refusedList, 'permission_denied', 403),
  )

  const refusedDetail = await call(getUserGroup, baseAdmins.id)
  record(
    'the limited account is refused one group with permission_denied',
    refusedAs(refusedDetail),
    refused(refusedDetail, 'permission_denied', 403),
  )

  const refusedCreate = await call(createUserGroup, {
    values: { name: 'Probe refused', role: 'Client', status: 'Registered', permissions: [] },
    csrfToken: await csrf(),
  })
  record(
    'the limited account is refused creating a group with permission_denied',
    refusedAs(refusedCreate),
    refused(refusedCreate, 'permission_denied', 403),
  )

  const refusedPatch = await call(updateUserGroup, {
    id: limited.id,
    patch: { name: 'Probe refused rename' },
    csrfToken: await csrf(),
  })
  record(
    'the limited account is refused changing a group with permission_denied',
    refusedAs(refusedPatch),
    refused(refusedPatch, 'permission_denied', 403),
  )

  const refusedDelete = await call(deleteUserGroup, { id: createdId, csrfToken: await csrf() })
  record(
    'the limited account is refused deleting a group with permission_denied',
    refusedAs(refusedDelete),
    refused(refusedDelete, 'permission_denied', 403),
  )
}

// --------------------------------------------------------------- the code table
//
// The codes this probe has been asserting by name are what the client translates. A code
// that reaches the UI untranslated is a sentence in English on a page otherwise in Russian,
// so each one is checked against both locale bundles rather than trusted to be there.

if (results.some((r) => !r.ok) === false) {
  const needed = [
    'permission_denied',
    'csrf_invalid',
    'user_group_created',
    'user_group_deleted',
    'user_group_already_exists',
    'user_group_immutable',
    'user_group_in_use',
    'user_group_not_found',
  ]
  const { default: en } = await import('../src/locales/en.ts')
  const { default: ru } = await import('../src/locales/ru.ts')
  const missingKeys = needed.filter((code) => !en.messages?.api?.[code] || !ru.messages?.api?.[code])
  record(
    'every code this probe asserts on is translated in both locales',
    missingKeys.length === 0
      ? `${needed.length} codes present in en and ru`
      : `MISSING: ${missingKeys.join(', ')}`,
    missingKeys.length === 0,
  )
}

console.log(`\n${results.filter((r) => r.ok).length}/${results.length} passed`)
if (results.some((r) => !r.ok)) process.exitCode = 1
