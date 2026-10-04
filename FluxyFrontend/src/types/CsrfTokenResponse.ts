/** `GET /auth/csrf` - the antiforgery token paired with the cookie the server set. */
export interface CsrfTokenResponse {
  token: string
}
