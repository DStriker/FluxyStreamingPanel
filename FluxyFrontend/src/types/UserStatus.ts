/**
 * The three states of an account, spelled exactly as the backend serializes them
 * (`UserStatus` by name: `Unregistered`, `Registered`, `Blocked`).
 *
 * **Not an ordered scale**, unlike `Role`: these are states one account moves between, not
 * levels of access, so nothing here may compare them with `<` or `>=`. An account goes
 * `Unregistered → Registered` when its address is confirmed, `Registered → Blocked` when it
 * is shut off, and `Blocked` back to whichever of the first two it came from - the way back
 * is decided by whether `registeredAt` is still set, which is why that timestamp is never
 * cleared by a block.
 *
 * A closed union rather than `string` for the reason `Role` gives: the filter and the form
 * offer these three and nothing else, and a member the build has never heard of should be a
 * compile error at the place that names them rather than a blank cell in a table.
 */
export type UserStatus = 'Unregistered' | 'Registered' | 'Blocked'
