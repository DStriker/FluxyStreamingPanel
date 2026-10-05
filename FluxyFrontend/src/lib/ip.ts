/**
 * Format checks for what the login guard stores as an allowed network: a bare address
 * or a CIDR block, in either family.
 *
 * The server stays the authority - it parses whatever arrives and may refuse a value
 * this check accepts. This exists so that a typo is named under the field it was typed
 * in, instead of arriving as a refusal about the whole save.
 */

const IPV4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/

/**
 * `999.1.1.1` has the shape of an IPv4 address, so the shape is only half the check:
 * every octet still has to be in range.
 */
const isIpv4 = (value: string): boolean => {
  const match = IPV4.exec(value)
  if (!match) return false
  return match.slice(1).every((octet) => Number(octet) <= 255)
}

/**
 * Eight groups of one to four hex digits, or fewer with exactly one `::` standing for
 * the run it compresses - written as a check rather than as the usual alternation of
 * nine patterns, because both describe the same thing and this one can be read.
 *
 * The `::` is what makes the counts differ: it stands for at least one group, so a
 * compressed address holds at most seven, while `::` itself holds none at all.
 */
const isIpv6 = (value: string): boolean => {
  if (!value.includes(':') || !/^[0-9a-f:]+$/i.test(value)) return false
  const parts = value.split('::')
  if (parts.length > 2) return false // two runs of `::` say nothing about what they stand for
  const groups = parts.flatMap((part) => (part === '' ? [] : part.split(':')))
  if (groups.some((group) => !/^[0-9a-f]{1,4}$/i.test(group))) return false
  return parts.length === 1 ? groups.length === 8 : groups.length <= 7
}

/** An address as the guard may store it - one entry of the list, with no prefix. */
export const isIpAddress = (value: string): boolean => isIpv4(value) || isIpv6(value)

/**
 * An address or an address with a CIDR prefix: `203.0.113.7`, `203.0.113.0/24`,
 * `2001:db8::/32`. The prefix is checked against the width of the family it belongs to,
 * because `/64` is a valid network and a broken IPv4 one.
 */
export const isIpOrCidr = (value: string): boolean => {
  const [address = '', prefix, ...rest] = value.split('/')
  if (rest.length > 0 || !isIpAddress(address)) return false
  if (prefix === undefined) return true
  if (!/^\d{1,3}$/.test(prefix)) return false
  return Number(prefix) <= (isIpv4(address) ? 32 : 128)
}
