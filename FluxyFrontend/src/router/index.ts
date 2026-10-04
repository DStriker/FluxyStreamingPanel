import { createBrowserRouter } from 'react-router-dom'
import { routes } from './routes'

/**
 * The application's router.
 *
 * The table itself lives in `./routes` because `createBrowserRouter` reads `document` the
 * moment it is called, and `npm run probe:render` runs in Node with no document at all -
 * a file that did both would make the whole probe fail at import time. Keeping the
 * construction here means the app still has exactly one place the history is made, while
 * the assertions the probe needs have something to read.
 */
export const router = createBrowserRouter(routes)
