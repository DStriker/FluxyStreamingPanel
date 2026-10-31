/**
 * Which of the three blacklists an entry belongs to, spelled as the API spells it.
 *
 * The same three strings the endpoint accepts as its `type` parameter. The screen maps its menu
 * key to one of these, so a value renamed on the server reaches this union first rather than
 * becoming a request the server refuses.
 */
export type BlacklistType = 'ip' | 'isp' | 'user-agent'
