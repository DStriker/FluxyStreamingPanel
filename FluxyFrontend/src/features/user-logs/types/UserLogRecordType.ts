/**
 * Which kind of account event one audit-log row records, as the endpoint spells it.
 *
 * The ten values are the server's `UserLogRecordType` wire names
 * (`Fluxy.Core/Users/Utils/UserLogRecordTypes.cs`), snake_case and parsed case-insensitively
 * as plain text on the way in. They are a union rather than `string` for the reason every
 * enum mirror here is: the filter picker builds the repeated `recordTypes` parameter out of
 * these values, and one bad element refuses the whole request with a 400 under that
 * parameter's own name - so a typo has to be a compile error rather than a broken screen.
 *
 * The numeric values behind them are schema (already written into rows) and are deliberately
 * not mirrored here: the frontend never sends or reads one.
 */
export type UserLogRecordType =
  | 'registration_submit'
  | 'registration_confirm'
  | 'login'
  | 'password_reset_request'
  | 'password_reset_confirm'
  | 'username_change'
  | 'email_change'
  | 'password_change'
  | 'login_guard_change'
  | 'logout'
