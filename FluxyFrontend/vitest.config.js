import { defineConfig } from 'vitest/config'

/**
 * The unit-test runner for the pure logic of the application.
 *
 * It is deliberately separate from `vite.config.js`: that file carries the dev-server,
 * proxy and chunking decisions, and none of them apply here. `node` is the environment
 * because every test in this suite exercises a pure function - path rules, policy
 * mirrors, display helpers - and never a rendered component. Rendering stays with
 * `npm run probe:render`, which bundles the real modules with esbuild and asserts on the
 * HTML; the two verify different layers and neither replaces the other.
 *
 * `include` names the test files explicitly (`.test.ts` under `tests/`) rather than leaving
 * Vitest's default pattern, so what is a test here is stated rather than guessed. The
 * suite lives in `tests/`, mirroring `src/`, rather than beside the modules it covers:
 * one tree to look in, and `tsconfig.json` includes it so `tsc` checks the tests too.
 */
export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/**/*.test.ts'],
  },
})
