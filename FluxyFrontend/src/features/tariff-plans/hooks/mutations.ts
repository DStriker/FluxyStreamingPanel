import { createTariffPlan, deleteTariffPlan, updateTariffPlan } from '../api'
import { useWrite } from '../../../hooks/useWrite'

/**
 * The tariff plans feature's writes: the create and the change the form sends, and the delete
 * the table's row action sends.
 *
 * Built on `useWrite` (`src/hooks/useWrite.ts`), so the antiforgery token is minted at call
 * time and the call sites hand over only the endpoint's own arguments.
 */

export const useCreateTariffPlan = () => useWrite(createTariffPlan)
export const useUpdateTariffPlan = () => useWrite(updateTariffPlan)
export const useDeleteTariffPlan = () => useWrite(deleteTariffPlan)
