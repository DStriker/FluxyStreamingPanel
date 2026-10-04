/** `GET /auth/password/status` - whether this installation can send a code at all. */
export interface PasswordStatusResponse {
  configured: boolean
}
