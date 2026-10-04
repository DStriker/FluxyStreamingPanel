import type { Role } from './Role'

/** `GET /auth/profile` - the three facts the profile page shows. */
export interface ProfileResponse {
  username: string
  email: string
  role: Role
}
