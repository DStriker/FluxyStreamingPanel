/**
 * The MAG portal's default theme, in the owner's own spelling.
 *
 * `cappucino` is deliberately **not** corrected to `cappuccino` and `ocean blue` carries a
 * space: these are the wire values the backend stores and answers with, so a "fixed" spelling
 * here would be a value the server refuses. The selector therefore maps each member to a
 * locale key rather than building one from the value.
 */
export type MagTheme = 'default' | 'digital' | 'emerald' | 'cappucino' | 'ocean blue'
