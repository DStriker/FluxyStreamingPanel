import {
  createContentCategory,
  deleteContentCategory,
  reorderContentCategories,
  updateContentCategory,
} from '../../lib/api'
import { useWrite } from '../../hooks/useWrite'

/**
 * The content-categories feature's writes: the four the table and its dialog run.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at
 * call time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateContentCategory = () => useWrite(createContentCategory)
export const useUpdateContentCategory = () => useWrite(updateContentCategory)
export const useDeleteContentCategory = () => useWrite(deleteContentCategory)

/** Sends the whole arrangement on screen as the new order, in one request. */
export const useReorderContentCategories = () => useWrite(reorderContentCategories)
