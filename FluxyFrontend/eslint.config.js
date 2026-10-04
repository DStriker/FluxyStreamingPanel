import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'

/**
 * Two blocks on purpose rather than one.
 *
 * `files: ['**\/*.{js,jsx}']` and `files: ['**\/*.{ts,tsx}']` are separate because the two
 * need different parsers, and because `jsx: true` is a parser option the TypeScript
 * parser does not take - it decides by file extension. The globs have to name the
 * extensions explicitly: a config that matches only `.js`/`.jsx` does not fail on a
 * `.ts` file, it simply has no rules for it, and lint goes green over an unlinted tree.
 * That silence is the failure mode this file exists to prevent, so the two lists are
 * kept next to each other and both are exhaustive.
 *
 * `typescript-eslint`'s own `recommended` preset is *not* spread as-is: it ships a
 * `base` config with no `files`, which would hand the TypeScript parser to every `.js`
 * file as well, and a companion that switches off base rules (`no-undef`, `prefer-const`)
 * this config never turned on. Only the rules object is taken from it, and only under
 * the TypeScript glob.
 *
 * The `.js` block takes `@eslint/js`'s `recommended` rules by the same logic and for the
 * same reason it needs them: without them that block carried parser options and two
 * plugin rules and nothing else, so an undefined variable or an unused one in
 * `eslint.config.js`, `vite.config.js` or `scripts/probe-render.jsx` was invisible to it.
 * `typescript-eslint`'s `eslint-recommended` companion is what switches `no-undef` off for
 * `.ts`/`.tsx` - because the compiler already answers it better - and it is spread only
 * into the TS rules, so `no-undef` stays on where nothing but the linter can catch it.
 */
const plugins = {
  'react-hooks': reactHooks,
  'react-refresh': reactRefresh,
}

// The preset is `[base, eslint-recommended, recommended]`; only `base` (parser + plugin)
// and the rules of `recommended` are used here - see the note above.
const [tsBase, , tsRules] = tseslint.configs.recommended

export default [
  { ignores: ['dist'] },
  {
    files: ['**/*.{js,jsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
      parserOptions: {
        ecmaVersion: 'latest',
        ecmaFeatures: { jsx: true },
        sourceType: 'module',
      },
    },
    plugins: { ...plugins, '@typescript-eslint': tsBase.plugins['@typescript-eslint'] },
    rules: {
      ...js.configs.recommended.rules,
      ...reactHooks.configs.recommended.rules,
      // The core rule does not read a JSX element name as a reference to anything: every
      // import this file's `<MemoryRouter>`, `<AntApp>` and the rest of the probe would
      // otherwise be reported unused. The TypeScript spelling of the rule walks the same
      // scope tree and does see them, which is the reason this switch is spelled the same
      // way as the TS block's - and why `no-unused-vars` from the preset is switched off
      // rather than left to disagree with it on the same line.
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
  {
    // `.jsx` is JSX first and JavaScript second, and the rule above needs a scope tree
    // that records `<MemoryRouter>` as a reference to something: espree's does not, which
    // is why all seven imports on the right-hand side of `probe-render.jsx`'s renders were
    // reported unused while every one of them is rendered. The TypeScript parser decides
    // JSX by file extension the way `tsc` does, so it is not an option that has to be
    // asked for - and this is the whole reason `scripts/probe-render.jsx` is `.jsx` and
    // not `.mjs`, which the note at its top already says.
    files: ['**/*.jsx'],
    languageOptions: {
      parser: tsBase.languageOptions.parser,
    },
  },
  {
    // Every `.js`/`.jsx` file in this repo runs under Node - the two configs and the
    // probe script, and nothing else: `src/` is TypeScript all the way down, which is
    // what the note at the top of this file means by an exhaustive list. So the browser
    // globals above are the wrong set for them, and `process`, which two of the three
    // reach for, is not among them - `no-undef` from the preset is exactly the rule that
    // should be live for a file nothing else type-checks, and it cannot be if the only
    // globals in scope are the browser's.
    files: ['**/*.config.js', 'scripts/**/*.{js,jsx}'],
    languageOptions: {
      globals: { ...globals.browser, ...globals.node },
    },
  },
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
      parser: tsBase.languageOptions.parser,
      parserOptions: {
        ecmaVersion: 'latest',
        sourceType: 'module',
      },
    },
    plugins: {
      ...plugins,
      '@typescript-eslint': tsBase.plugins['@typescript-eslint'],
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      ...tsRules.rules,
      // `tsc --noEmit` under `strict` is the authority on types and it runs before every
      // build; lint re-reporting the same file with weaker rules only adds noise. The one
      // rule kept is `no-unused-vars` (the TS spelling), because a dead import is a
      // readability problem and not a type problem.
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      // Downgraded from the preset's `error`: an explicit `any` is a visible escape hatch
      // and `strict` already forbids the implicit kind. Failing lint on it would make the
      // linter disagree with the compiler about the very same line.
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },
  {
    // The rule exists to keep React fast refresh working, and it reads JSX. A `.ts` file
    // holds no JSX at all - `sessionContext`, `navigation`, the data half of `routes` - so
    // exporting `routes` or `NAVIGATION` from one would be reported as a refresh hazard it
    // cannot be. `react-refresh` is left applied and only its verdict switched off, so the
    // two blocks stay structurally the same.
    files: ['**/*.ts'],
    rules: {
      'react-refresh/only-export-components': 'off',
    },
  },
  {
    files: ['**/*.tsx'],
    rules: {
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
]
