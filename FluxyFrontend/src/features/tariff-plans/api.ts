import { apiFetch, apiGet } from '../../utils/http'
import type { MessageResponse } from '../../types'
import type { TariffPlanDetail, TariffPlanDurationUnit, TariffPlanList } from './types'

/**
 * The tariff plans: the package a subscription is sold under, the page of them the table
 * draws, and the writes its form sends.
 *
 * The domain has three shapes and this module keeps them apart. A list row is what the table
 * draws - settings plus the counts of the two relations. The detail is what the edit form
 * reads - the same settings plus the identifiers of both relations, so its pickers open on
 * what the plan actually has. `TariffPlanValues` is what either write sends; a create always
 * sends every field, a change sends the ones the form holds.
 */

/** The columns the table may be ordered by, as the query-string spelling. */
export type TariffPlanSortField = 'name' | 'cost' | 'createdAt'

export type TariffPlanSortOrder = 'asc' | 'desc'

/** The page size the pager opens with, which is also the server's default. */
export const TARIFF_PLAN_PAGE_SIZE = 20

/** Every setting a plan carries plus its two relations - the body of both writes. */
export interface TariffPlanValues {
  /** Display name, unique across the installation in any spelling. */
  name: string
  /** Whether the plan is a free trial. */
  isTrial: boolean
  /** Cost in credits. */
  cost: number
  /** How much of `durationUnit` the plan lasts for. */
  durationValue: number
  /** The unit the validity period is counted in. */
  durationUnit: TariffPlanDurationUnit
  /** Whether an M3U playlist is delivered. */
  supportsM3u: boolean
  /** Whether a MAG device is supported. */
  supportsMag: boolean
  /** Whether an Enigma2 device is supported. */
  supportsEnigma2: boolean
  /** Whether MPEG-TS is delivered. */
  outputMpegTs: boolean
  /** Whether HLS is delivered. */
  outputHls: boolean
  /** Whether a MAG device's STB identifier is checked strictly. */
  strictStbDeviceId: boolean
  /** Whether a subscriber may restream the subscription. */
  allowRestream: boolean
  /** How many devices may be connected at the same time. */
  maxConnections: number
  /** Whether connections from different countries are allowed at the same time. */
  allowMultipleCountries: boolean
  /** Every group that may be sold the plan. An empty list means none may. */
  userGroupIds: string[]
  /** Every bouquet the plan delivers. An empty list means it delivers nothing. */
  bouquetIds: string[]
}

/**
 * The body both writes send: the name trimmed, and both relations always present.
 *
 * The relations travel on every write rather than only when they changed, for the rule the
 * content forms already state: an empty array is the statement "related to nothing", and a
 * field omitted from this body would mean "keep what the row has" on a change. A form holds
 * both pickers, so it always has both answers.
 */
const toBody = (values: TariffPlanValues) => ({
  name: (values.name ?? '').trim(),
  isTrial: values.isTrial,
  cost: values.cost,
  durationValue: values.durationValue,
  durationUnit: values.durationUnit,
  supportsM3u: values.supportsM3u,
  supportsMag: values.supportsMag,
  supportsEnigma2: values.supportsEnigma2,
  outputMpegTs: values.outputMpegTs,
  outputHls: values.outputHls,
  strictStbDeviceId: values.strictStbDeviceId,
  allowRestream: values.allowRestream,
  maxConnections: values.maxConnections,
  allowMultipleCountries: values.allowMultipleCountries,
  userGroupIds: values.userGroupIds,
  bouquetIds: values.bouquetIds,
})

/**
 * One page of the table: `GET /admin/tariff-plans`.
 *
 * A plain read behind the admin session, so no antiforgery token - nothing is changed. The
 * page, the filter and the order are the server's because the pager draws a `total`; a filter
 * applied here would narrow the rows on screen while the pager still counted everything.
 */
export const getTariffPlans = ({
  page,
  pageSize,
  search,
  sortBy,
  sortOrder,
  signal,
}: {
  page: number
  pageSize: number
  search?: string
  sortBy: TariffPlanSortField
  sortOrder: TariffPlanSortOrder
  signal?: AbortSignal
}): Promise<TariffPlanList> => {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
    sortBy,
    sortOrder,
  })

  if (search) query.set('search', search)

  return apiGet<TariffPlanList>(`/admin/tariff-plans?${query.toString()}`, signal)
}

/** One plan in full, for the edit form: `GET /admin/tariff-plans/{id}`. */
export const getTariffPlan = (id: string, signal?: AbortSignal): Promise<TariffPlanDetail> =>
  apiGet<TariffPlanDetail>(`/admin/tariff-plans/${encodeURIComponent(id)}`, signal)

/**
 * Creates a plan: `POST /admin/tariff-plans`, answered with `201 tariff_plan_created` and a
 * `Location` naming it.
 *
 * A name another plan holds *in any spelling* is `409 tariff_plan_already_exists` with the
 * field named in `errors`, so the form shows it under the input rather than as a toast.
 */
export const createTariffPlan = ({
  values,
  csrfToken,
}: {
  values: TariffPlanValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch('/admin/tariff-plans', {
    method: 'POST',
    body: toBody(values),
    csrfToken,
  })

/**
 * Changes a plan: `PATCH /admin/tariff-plans/{id}`, answered with
 * `200 tariff_plan_updated`.
 *
 * The whole form travels rather than a diff, for the reason every other form here does: one
 * screen holds one plan, so what the operator sees and what the row becomes are the same
 * statement - and the two relations are sets the form always has the complete answer to.
 */
export const updateTariffPlan = ({
  id,
  values,
  csrfToken,
}: {
  id: string
  values: TariffPlanValues
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/tariff-plans/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: toBody(values),
    csrfToken,
  })

/**
 * Deletes a plan: `DELETE /admin/tariff-plans/{id}`, answered with
 * `200 tariff_plan_deleted`.
 *
 * The two relations go with it through their foreign keys; nothing else has to be emptied
 * first today, because nothing references a plan yet.
 */
export const deleteTariffPlan = ({
  id,
  csrfToken,
}: {
  id: string
  csrfToken: string | null
}): Promise<MessageResponse> =>
  apiFetch(`/admin/tariff-plans/${encodeURIComponent(id)}`, { method: 'DELETE', csrfToken })
