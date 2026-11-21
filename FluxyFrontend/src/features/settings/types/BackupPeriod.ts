/**
 * How often a scheduled backup would run, in the owner's lowercase wire words.
 *
 * A union rather than `string`, because the PATCH refuses any other word and the selector has
 * exactly four options to offer - a typo here is then a compile error rather than a 400 named
 * under `backupPeriod` after the click.
 */
export type BackupPeriod = 'hourly' | 'daily' | 'weekly' | 'monthly'
