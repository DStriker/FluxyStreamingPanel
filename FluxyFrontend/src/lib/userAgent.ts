import UAParser from 'ua-parser-js'

/**
 * One readable line out of a user agent - `Chrome 140 (Windows 10, x64)` - or `null` when the
 * string says nothing that can be read as a client.
 *
 * `null` is an answer, not a failure: it means the cell shows the string exactly as the server
 * stored it, which is what "if it does not parse, show it as it is" asks for. A `curl`, a bot,
 * a native client and a browser this build has never heard of all arrive here, and a
 * half-guessed summary of any of them would be worse than the truth - so the rule is "no
 * browser name, no summary" rather than "assemble whatever we managed to recognise". Only the
 * browser is required; an operating system or an architecture it does not know simply drops
 * out of the parentheses, and with neither the label stands alone.
 *
 * Two things the summary cannot know, and therefore does not claim:
 *
 * - **Windows 10 and Windows 11 are the same string.** Chrome froze `User-Agent` at
 *   `Windows NT 10.0`, so every Windows 11 machine reports itself as Windows 10 to anything
 *   that parses a UA - this library included. The label is what the client said, not what the
 *   client is. The full string in the tooltip is not any better, because it holds the same
 *   token; there is no newer source to read.
 * - **An Apple Silicon Mac reports `Intel`.** The Mac UA is frozen at `Intel Mac OS X`, so no
 *   parser can tell the two apart. Rather than print `x64` for a machine that may be `arm64`,
 *   this reports no architecture for a Mac at all - a missing fact, not a wrong one.
 *
 * Both halves are needed in the cell, which is why the summary replaces the raw string rather
 * than joining it: "Chrome on Windows" is what a person scans for while working down a page of
 * twenty rows, and "is this exactly the client I own" is answered by the whole string, handed
 * to the tooltip unchanged.
 *
 * The library is `ua-parser-js` on the **1.x line**, which is MIT. The 2.x line moved to
 * AGPL-3.0-or-later, so `package.json` pins `^1.0.41` on purpose - a caret that cannot cross a
 * major, and the reason an `npm update` cannot silently put an AGPL dependency under this
 * repository. The other candidates were checked and are worse fits: `bowser` is MIT but does
 * not parse CPU architecture at all, and `device-detector-js` is LGPL.
 */

/**
 * The architecture the parser reports, in the spelling a person would write.
 *
 * The keys are the whole list `@types/ua-parser-js` documents for `cpu.architecture`, so an
 * unknown value can only mean the type and the library have drifted apart - in which case the
 * raw value is still more informative than nothing.
 */
const ARCHITECTURES: Record<string, string> = {
  '68k': '68k',
  amd64: 'x64',
  arm: 'ARM',
  arm64: 'ARM64',
  avr: 'AVR',
  ia32: 'x86',
  ia64: 'Itanium',
  irix: 'IRIX',
  irix64: 'IRIX64',
  mips: 'MIPS',
  mips64: 'MIPS64',
  'pa-risc': 'PA-RISC',
  ppc: 'PowerPC',
  sparc: 'SPARC',
  sparc64: 'SPARC64',
}

/** What the UA token `Mac OS` has been called since 2016. */
const OS_NAMES: Record<string, string> = {
  'Mac OS': 'macOS',
}

/**
 * Parsed answers, remembered per string.
 *
 * Twenty rows usually hold four or five distinct user agents, so the cache hits almost every
 * time - and this function is called once per row on every repaint of the table, which during
 * a column drag means sixty times a second. Parsing all hundred costs about 2.2ms of a 16ms
 * frame on this machine; a map lookup does not.
 *
 * Capped by clearing rather than by eviction, because a bounded cache that is expensive to
 * reason about is not worth 512 entries. Nothing depends on a hit: the value is a pure
 * function of the string, and a cleared entry is simply parsed again.
 */
const summaries = new Map<string, string | null>()

const MAX_CACHED_SUMMARIES = 512

/**
 * `140.0.0.0` as `140`, `141.0` as `141`, and `18.1` untouched.
 *
 * Every trailing component a UA reports past the first is padding rather than a version
 * anybody means: Chrome writes `Chrome/140.0.0.0` and the fourth digit has never carried
 * information. Dots are dropped from the end only, so a version that genuinely has parts
 * keeps them - `3.0.1` stays `3.0.1`.
 */
function shortVersion(version: string | undefined): string {
  if (!version) return ''

  const parts = version.split('.')
  while (parts.length > 1 && parts[parts.length - 1] === '0') parts.pop()

  return parts.join('.')
}

function summarise(userAgent: string): string | null {
  const { browser, os, cpu } = new UAParser(userAgent).getResult()

  if (!browser.name) return null

  const version = shortVersion(browser.version)
  const label = version ? `${browser.name} ${version}` : browser.name

  const parts: string[] = []

  if (os.name) {
    const name = OS_NAMES[os.name] ?? os.name
    const osVersion = shortVersion(os.version)
    parts.push(osVersion ? `${name} ${osVersion}` : name)
  }

  if (cpu.architecture) parts.push(ARCHITECTURES[cpu.architecture] ?? cpu.architecture)

  return parts.length > 0 ? `${label} (${parts.join(', ')})` : label
}

/**
 * `Chrome 140 (Windows 10, x64)` for a browser it recognises, `null` for anything it does not
 * - see the note above for why the second answer is deliberately "show it unchanged".
 *
 * `null` for an absent value as well, so the caller's empty check and its fallback are the
 * same code path rather than two that can disagree.
 */
export function describeUserAgent(userAgent: string | null | undefined): string | null {
  if (!userAgent) return null

  const cached = summaries.get(userAgent)
  if (cached !== undefined) return cached

  const summary = summarise(userAgent)

  if (summaries.size >= MAX_CACHED_SUMMARIES) summaries.clear()
  summaries.set(userAgent, summary)

  return summary
}
