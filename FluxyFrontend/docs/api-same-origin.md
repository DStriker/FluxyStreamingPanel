> Part of the FluxyFrontend AGENTS.md instruction set — read on demand. Back to [AGENTS.md](../AGENTS.md).


## The API shares an origin with the page, and that is load-bearing

The antiforgery cookies the backend sets are `SameSite=Lax` (`AntiforgeryExtensions.cs` for the framework cookie, `RegistrationController.GetCsrfToken` for the readable `XSRF-TOKEN`), and a *site* in the SameSite sense is scheme **plus** registrable domain. So an `http://localhost:5173` page calling `https://localhost:7221` is a **cross-site** request even though both hosts read as "localhost". A browser withholds Lax cookies from cross-site subresource requests — `credentials: 'include'` does not change that — and hides them from `document.cookie`, so `getCsrfToken` finds no cookie and returns no token either. Both halves of the pair are lost and every submit comes back `csrf_invalid`.

Switching the base URL to `https://localhost:7221` does not rescue it: the scheme still differs. It only looks like it helps because the `https` launch profile answers the http port with a **307**, and a `fetch` cannot follow that without `Access-Control-Allow-Origin` on the redirect, so the http URL fails differently and worse.

So there are three ways to serve the API in development, and only one of them keeps `SameSite=Lax`:

| Setup | SameSite | Verdict |
| --- | --- | --- |
| Vite proxy over https, `VITE_API_BASE_URL` empty | first-party, irrelevant | **What this repo does** |
| Page on `https://localhost:5173`, API on `https://localhost:7221` | same scheme, so same site | Works, but needs a Node-trusted cert |
| Page on `http`, API on `https` (or the reverse) | cross-**site** | Never works while the cookies are Lax |

Making it work without the proxy would mean either weakening the backend to `SameSite=None; Secure` — which degrades CSRF protection for production, where it is unnecessary, and leaves the app on an insecure origin — or giving the SPA its own trusted certificate. The proxy avoids both and matches the production topology, which is one origin behind a reverse proxy.

### The dev server speaks https, via `@vitejs/plugin-basic-ssl`

`server.https` is on, so the page is `https://localhost:5173` — the same scheme the API is served under in production. The plugin generates a self-signed certificate on first run and caches it at `node_modules/.vite/basic-ssl/_cert.pem`; nothing goes into the machine's trust store. Expect **one** browser warning about the certificate until you accept it, after which Chrome remembers it per host.

With the page on `http` and the backend on `https` — which is exactly what Visual Studio's `https` launch profile produces — the two are different schemes, which is the class of mismatch that produced the `307` → cross-origin → refused chain described under the middleware notes in the backend's [`http-pipeline.md`](../../FluxyBackend/docs/http-pipeline.md). Both halves being https removes the whole category rather than papering over its current instance. It also means the local dev loop is no longer a special case that behaves differently from a deployment.

The proxy target stays the backend's **http** port (`VITE_API_PROXY_TARGET`, default `http://localhost:5159`). The hop from the proxy to the backend is server-to-server on loopback and invisible to the browser, so its scheme has no bearing on SameSite; the browser only ever sees `https://localhost:5173`.

**`npm run probe` goes through `scripts/run-probe.mjs`, not straight to `node`.** Node's `fetch` does not trust a self-signed certificate, so every request through the https dev server failed with `DEPTH_ZERO_SELF_SIGNED_CERT` — and the symptom was badly misleading: `getCsrfToken` treats a network error as "no token" and returns `null`, so the probe reported a failing check *about the CSRF URL* when nothing was wrong with it. The runner spawns the bundle with `NODE_EXTRA_CA_CERTS` pointing at the generated certificate, which is read once at process start and therefore cannot be set from inside the probe. Do not "simplify" this to `NODE_TLS_REJECT_UNAUTHORIZED=0`: that turns off verification for every host the process talks to and hides a genuinely wrong certificate, which is the one thing worth seeing. With the runner in place the probe is 11/11 over `https://localhost:5173` and 11/11 over a plain `http://localhost:5159`; the http path involves no certificate at all, which is a useful control that the fix did not break the no-TLS case.

`npm run probe` runs in Node, which has no cookie or SameSite semantics at all — the script has to fake a cookie jar. It can therefore verify the proxy forwards correctly and the whole flow completes, and it did (11/11 through `https://localhost:5173`, and 11/11 over plain `http://localhost:5159`), but it **cannot** detect this class of bug. Only a real browser can, and every check here was made without one.

A Node-only failure can *masquerade* as an application bug. When the dev server turned to https, every probe request failed with `DEPTH_ZERO_SELF_SIGNED_CERT`, `getCsrfToken` converted that to `null`, and the check that reported it was the one asserting `getCsrfToken` uses the API base — pointing at a module that had not changed and was not wrong. A check that fails for a transport reason while describing a logic property is the most expensive kind of failure to read, so check the transport before believing the assertion.
