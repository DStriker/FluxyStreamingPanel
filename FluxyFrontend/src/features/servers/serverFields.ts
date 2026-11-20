/**
 * The two conversions the server form needs, and nothing else.
 *
 * The wire keeps the throughput in kbps - the unit is in the field's own name, and the API
 * never converts - while an operator thinks in Mbps. `0` is a real state on both sides: it
 * means "unknown" on the wire, so the conversion has to keep it rather than turn it into a
 * fraction of a megabit.
 *
 * These are pure functions rather than expressions inside the form because the rounding is
 * the part that can be wrong at a boundary (a fractional Mbps, a value at the server's
 * ceiling), and a unit test can pin it without a DOM.
 */

/** The wire's kbps as the Mbps the form shows. */
export const kbpsToMbps = (kbps: number): number => kbps / 1000

/** The form's Mbps as the wire's kbps, rounded to the nearest whole kilobit. */
export const mbpsToKbps = (mbps: number): number => Math.round(mbps * 1000)
