import { createBouquet, deleteBouquet, renameBouquet, reorderBouquets, setBouquetContent } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The bouquets feature's writes: the four the table and its dialog run, and the one the
 * content manager runs per kind of content.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at call
 * time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateBouquet = () => useWrite(createBouquet)
export const useRenameBouquet = () => useWrite(renameBouquet)
export const useDeleteBouquet = () => useWrite(deleteBouquet)

/** Sends the whole arrangement on screen as the new order, in one request. */
export const useReorderBouquets = () => useWrite(reorderBouquets)

/** Replaces what one bouquet holds of one kind, in the order the manager arranged. */
export const useSetBouquetContent = () => useWrite(setBouquetContent)
