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
    plugins,
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
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
