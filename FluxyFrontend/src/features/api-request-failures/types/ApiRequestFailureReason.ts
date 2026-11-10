/**
 * Why one public-API request was refused, as the endpoint spells it.
 *
 * The six values are the server's `ApiRequestFailureReason` wire names
 * (`Fluxy.Core/Requests/Utils/ApiRequestFailureReasons.cs`), snake_case and parsed
 * case-insensitively as plain text on the way in. They are a union rather than `string` for
 * the reason every enum mirror here is: the filter picker builds the repeated `reasons`
 * parameter out of these values, and one bad element refuses the whole request with a 400
 * under that parameter's own name - so a typo has to be a compile error rather than a broken
 * screen.
 *
 * The numeric values behind them are schema (already written into rows) and are deliberately
 * not mirrored here: the frontend never sends or reads one.
 */
export type ApiRequestFailureReason =
  | 'unknown_id'
  | 'geo_ip_violation'
  | 'auth_failed'
  | 'too_many_connections'
  | 'subscription_expired'
  | 'user_blocked'
