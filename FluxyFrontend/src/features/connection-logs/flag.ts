/**
 * Where a country's flag is fetched from, and the URL shape that makes it work.
 *
 * It used to be `www.ip-tracker.org/images/ip-flags/{code}.png`, and that host answers **403 with
 * a Cloudflare challenge** for every image request - measured, not inferred: the body begins
 * `<!DOCTYPE html>…<title>Just a moment`, so the browser fires `onError` and the cell silently fell
 * back to two letters on every row. The URL shape mattered less than it looked, because the
 * service never answered with an image at all.
 *
 * This host answers a real PNG: `200`, `image/png`, a 559-byte file for `de`, carrying
 * `Cache-Control: max-age=31536000`, so the browser keeps one picture per country rather than one
 * request per row. Two facts about it are load-bearing and both were measured:
 *
 * - **It is lower-case only.** `de.png` is the flag and `DE.png` is a 404, which is why the code
 *   is folded here rather than passed as the row stored it (the database stores `DE` upper case).
 * - **It is not hotlink-protected.** The same URL answers `200 image/png` with an `Origin` and a
 *   `Referer` naming the page's own address, so a browser's request is indistinguishable from the
 *   one the diagnosis made.
 *
 * A code with no picture (a non-country such as `XX`) is a 404, which is the one path the cell's
 * `onError` fallback is for.
 *
 * This module exists rather than living beside the component because
 * `react-refresh/only-export-components` lets a component's file export only its component, and
 * the URL is a rule about the wire that a unit test can pin - a static render never fetches an
 * image, and jsdom is not installed here.
 */

/** The directory every flag is served from. Also the constant a test asserts against. */
export const FLAG_BASE_URL = 'https://www.iptrackeronline.com/flags'

/**
 * The address of one country's flag image.
 *
 * @param code ISO 3166-1 alpha-2 country code, in any case.
 * @returns The absolute URL the `<img>` loads, with the code folded to the only case the service
 *   answers for.
 */
export const flagUrl = (code: string): string =>
  `${FLAG_BASE_URL}/${code.trim().toLowerCase()}.png`
