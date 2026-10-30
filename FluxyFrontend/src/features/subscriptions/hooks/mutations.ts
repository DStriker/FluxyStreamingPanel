import {
  bulkSubscriptions,
  createSubscription,
  deleteSubscription,
  generatePlaylistId,
  patchSubscription,
  prolongSubscription,
  updateSubscription,
} from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The subscriptions feature's writes: the create and the change the form sends, the delete the
 * table's row action sends, the playlist-id generator, and the run over a selection.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at call time
 * and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateSubscription = () => useWrite(createSubscription)
export const useUpdateSubscription = () => useWrite(updateSubscription)
export const useDeleteSubscription = () => useWrite(deleteSubscription)
export const useBulkSubscriptions = () => useWrite(bulkSubscriptions)

/** Extends one subscription's period. The row action and the bulk `prolong` both answer it. */
export const useProlongSubscription = () => useWrite(prolongSubscription)

/**
 * Changes one service field of a subscription.
 *
 * Separate from `useUpdateSubscription` because the two bodies are different requests: the form
 * sends the whole row it holds, and a row strip sends only the field it touched and leaves the
 * rest as they are.
 */
export const usePatchSubscription = () => useWrite(patchSubscription)

/**
 * Mints a playlist id the server considers unused.
 *
 * A write built on `useWrite` because it mints something the operator is about to store: it needs
 * the antiforgery pair, and caching it would hand the same id back after the operator asked for a
 * new one. Called from an effect on the create form and from the regenerate button.
 */
export const useGeneratePlaylistId = () => useWrite(generatePlaylistId)
