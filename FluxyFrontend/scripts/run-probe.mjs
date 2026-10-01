/**
 * Runs the bundled registration probe with the dev certificate trusted.
 *
 * The dev server speaks https through `@vitejs/plugin-basic-ssl`, which generates a
 * self-signed certificate on first run and caches it under `node_modules/.vite/basic-ssl/`.
 * Node's fetch does not trust a self-signed certificate, so every request through the
 * proxy failed with `DEPTH_ZERO_SELF_SIGNED_CERT` before reaching the handler. The visible
 * symptom was misleading: `getCsrfToken` swallowed the network error and returned no
 * token, and the probe reported that as a failing check about the CSRF URL.
 *
 * `NODE_EXTRA_CA_CERTS` is read once, at process start, which is why this cannot be set
 * from inside the probe itself - a child process is spawned with it already in place.
 *
 * Deliberately NOT `NODE_TLS_REJECT_UNAUTHORIZED=0`. That disables verification for every
 * host the process talks to, and it also hides the certificate being wrong, which is the
 * one thing worth noticing. Trusting the specific certificate this repo generates keeps
 * every other check intact.
 *
 * Nothing to do when there is no certificate on disk: that is the plain-http setup, and an
 * https base is then reported with a hint instead of a bare `DEPTH_ZERO_SELF_SIGNED_CERT`.
 */
import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'

const here = dirname(fileURLToPath(import.meta.url))
const repo = join(here, '..')
const bundle = join(repo, 'node_modules', '.cache', 'probe-registration.mjs')
const cert = join(repo, 'node_modules', '.vite', 'basic-ssl', '_cert.pem')
const base = process.env.FLUXY_API ?? 'http://localhost:5159'

if (!existsSync(bundle)) {
  console.error(`Probe bundle is missing: ${bundle}\nRun it through \`npm run probe\`, which builds it first.`)
  process.exit(1)
}

const env = { ...process.env }
const isHttps = base.startsWith('https:')

if (isHttps && existsSync(cert)) {
  env.NODE_EXTRA_CA_CERTS = cert
} else if (isHttps) {
  console.warn(
    `No dev certificate at ${cert}\n` +
    'The probe will fail with DEPTH_ZERO_SELF_SIGNED_CERT. Start the dev server once - ' +
    '@vitejs/plugin-basic-ssl generates the certificate on its first run.',
  )
}

const child = spawn(process.execPath, [bundle], { stdio: 'inherit', env })
child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal)
    return
  }
  process.exit(code ?? 1)
})