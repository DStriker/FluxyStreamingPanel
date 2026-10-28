import { useQuery } from '@tanstack/react-query'
import { getTariffPlan, getTariffPlans } from '../api'
import type { TariffPlanSortField, TariffPlanSortOrder } from '../api'
import { detailId } from '../../../utils/detailId'

/**
 * The tariff plans feature's reads: one page of the table, and one plan in full.
 */

/**
 * One page of the plans table.
 *
 * The key is the whole parameter set, so a search, an order or a page turn is a new entry
 * rather than an effect that had to notice one - the shape the streams table and the guide
 * sources table already use.
 */
export const useTariffPlans = (params: {
  page: number
  pageSize: number
  search: string
  sortBy: TariffPlanSortField
  sortOrder: TariffPlanSortOrder
}) =>
  useQuery({
    queryKey: ['tariffPlans', params],
    queryFn: ({ signal }) => getTariffPlans({ ...params, signal }),
  })

/**
 * One plan in full, for the edit form.
 *
 * Disabled while there is no identifier, which is what makes one component serve the add
 * address as well as the edit one - an add form must not ask for a plan it is about to create.
 * `detailId` is what states that invariant in code rather than in a cast.
 */
export const useTariffPlan = (id: string | undefined) =>
  useQuery({
    queryKey: ['tariffPlan', id],
    queryFn: ({ signal }) => getTariffPlan(detailId(id), signal),
    enabled: id !== undefined,
  })
