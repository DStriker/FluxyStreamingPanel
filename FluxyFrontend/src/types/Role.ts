/**
 * The three roles, spelled exactly as the backend serializes them (`UserRole` by name:
 * `Client`, `Reseller`, `Admin`).
 *
 * It is a closed union rather than `string` because every table keyed by role - the
 * navigation, the landing paths - is keyed by these three and nothing else. The server is
 * still free to answer with a role this build has never heard of; the functions that turn
 * a role into a path take `string` and fall back, which is the behaviour that keeps an
 * unrecognised role from bouncing between guards.
 */
export type Role = 'Client' | 'Reseller' | 'Admin'
