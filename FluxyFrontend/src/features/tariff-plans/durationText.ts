import type { TariffPlanDurationUnit } from './types'

/**
 * How a validity period is written in each language.
 *
 * The words live here rather than in the locale bundles because the number has to agree with
 * the noun, and that agreement is a rule rather than a string: Russian needs one form for 1,
 * another for 2–4 and a third for 5 and above, and i18next's plural machinery is not wired into
 * this application (the bundles carry no `_one`/`_few` keys at all). A function that picks the
 * form from the number is the smallest thing that gets the grammar right, and it is a pure
 * function, so the unit tests can pin all three forms of every unit.
 *
 * The unit is part of the sentence rather than appended to a number, which also means a
 * language that puts the number last has one place to say so.
 */

/**
 * The Russian plural form of a count. The three forms are the language's, not a guess: 1, 21,
 * 31 take the singular; 2–4, 22–24 take the paucal; everything else the plural. The teens are
 * the trap - 11–14 take the plural despite ending in 1–4.
 */
const russianPluralForm = (count: number): 'one' | 'few' | 'many' => {
  const absolute = Math.abs(count) % 100
  const last = absolute % 10

  if (absolute >= 11 && absolute <= 14) return 'many'
  if (last === 1) return 'one'
  if (last >= 2 && last <= 4) return 'few'

  return 'many'
}

/** The Russian words for one unit, in the three forms its grammar has. */
const RUSSIAN_UNITS: Record<
  TariffPlanDurationUnit,
  Readonly<Record<'one' | 'few' | 'many', string>>
> = {
  hours: { one: 'час', few: 'часа', many: 'часов' },
  days: { one: 'день', few: 'дня', many: 'дней' },
  months: { one: 'месяц', few: 'месяца', many: 'месяцев' },
  years: { one: 'год', few: 'года', many: 'лет' },
}

/** The English words, which have two forms and take the plural for everything but 1. */
const ENGLISH_UNITS: Record<TariffPlanDurationUnit, readonly [string, string]> = {
  hours: ['hour', 'hours'],
  days: ['day', 'days'],
  months: ['month', 'months'],
  years: ['year', 'years'],
}

/**
 * Writes one validity period as a person reads it: `3 дня`, `1 месяц`, `12 hours`.
 *
 * @param value Amount of the unit.
 * @param unit Unit the amount is counted in.
 * @param language Language the sentence is written in, as `i18n.resolvedLanguage` names it.
 *   Anything that is not Russian falls back to English, which is also the fallback bundle.
 */
export const formatTariffPlanDuration = (
  value: number,
  unit: TariffPlanDurationUnit,
  language: string | undefined,
): string => {
  const isRussian = (language ?? 'en').toLowerCase().startsWith('ru')

  if (isRussian) {
    return `${value} ${RUSSIAN_UNITS[unit][russianPluralForm(value)]}`
  }

  const [singular, plural] = ENGLISH_UNITS[unit]

  return `${value} ${Math.abs(value) === 1 ? singular : plural}`
}
