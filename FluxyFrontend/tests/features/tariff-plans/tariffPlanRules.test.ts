import { describe, expect, it } from 'vitest'
import {
  TARIFF_PLAN_CONNECTIONS_MAX,
  TARIFF_PLAN_CONNECTIONS_MIN,
  TRIAL_MAX_DAYS,
  TRIAL_MAX_HOURS,
  hasDeliveryMethod,
  hasOutputFormat,
  isStrictStbAllowed,
  isTrialDurationAllowed,
  isUnitOfferedToTrial,
} from '../../../src/features/tariff-plans/tariffPlanRules'
import { formatTariffPlanDuration } from '../../../src/features/tariff-plans/durationText'

/**
 * The tariff plan rules the form mirrors from the server, and the one display helper the table
 * calls.
 *
 * These are the only checker that reaches either module in this build: `probe:render` lands
 * before anything is typed, and the rules run inside validators no render can trigger. The
 * numbers are asserted against the constants rather than against literals, so a limit that moves
 * moves one place.
 */

describe('the trial ceiling', () => {
  it('accepts hours up to the ceiling and refuses one hour more', () => {
    expect(isTrialDurationAllowed(1, 'hours')).toBe(true)
    expect(isTrialDurationAllowed(TRIAL_MAX_HOURS, 'hours')).toBe(true)
    expect(isTrialDurationAllowed(TRIAL_MAX_HOURS + 1, 'hours')).toBe(false)
  })

  it('accepts days up to the ceiling and refuses one day more', () => {
    expect(isTrialDurationAllowed(1, 'days')).toBe(true)
    expect(isTrialDurationAllowed(TRIAL_MAX_DAYS, 'days')).toBe(true)
    expect(isTrialDurationAllowed(TRIAL_MAX_DAYS + 1, 'days')).toBe(false)
  })

  it('refuses months and years outright', () => {
    // A month is not thirty days in every month, so neither unit can be held inside the
    // ceiling without a conversion the rule does not make.
    expect(isTrialDurationAllowed(1, 'months')).toBe(false)
    expect(isTrialDurationAllowed(1, 'years')).toBe(false)
  })

  it('states the ceiling in the hours a day has', () => {
    expect(TRIAL_MAX_HOURS).toBe(TRIAL_MAX_DAYS * 24)
  })
})

describe('the delivery and format minimums', () => {
  it('refuses a plan with no delivery method and accepts any one of the three', () => {
    expect(hasDeliveryMethod({ m3u: false, mag: false, enigma2: false })).toBe(false)
    expect(hasDeliveryMethod({ m3u: true, mag: false, enigma2: false })).toBe(true)
    expect(hasDeliveryMethod({ m3u: false, mag: true, enigma2: false })).toBe(true)
    expect(hasDeliveryMethod({ m3u: false, mag: false, enigma2: true })).toBe(true)
  })

  it('refuses a plan with no output format and accepts either one', () => {
    expect(hasOutputFormat({ mpegTs: false, hls: false })).toBe(false)
    expect(hasOutputFormat({ mpegTs: true, hls: false })).toBe(true)
    expect(hasOutputFormat({ mpegTs: false, hls: true })).toBe(true)
  })
})

describe('the strict STB check', () => {
  it('is only allowed beside MAG support', () => {
    expect(isStrictStbAllowed(true)).toBe(true)
    expect(isStrictStbAllowed(false)).toBe(false)
  })
})

describe('the units a trial is offered', () => {
  it('offers hours and days and nothing else', () => {
    expect(isUnitOfferedToTrial('hours')).toBe(true)
    expect(isUnitOfferedToTrial('days')).toBe(true)
    expect(isUnitOfferedToTrial('months')).toBe(false)
    expect(isUnitOfferedToTrial('years')).toBe(false)
  })
})

describe('the connection bounds', () => {
  it('states the range the server accepts', () => {
    expect(TARIFF_PLAN_CONNECTIONS_MIN).toBe(1)
    expect(TARIFF_PLAN_CONNECTIONS_MAX).toBe(10000)
  })
})

describe('formatTariffPlanDuration', () => {
  it('declines Russian nouns by the number', () => {
    // One takes the singular, 2-4 the paucal, 5 and above the plural, and the teens are the
    // trap the function exists for.
    expect(formatTariffPlanDuration(1, 'days', 'ru')).toBe('1 день')
    expect(formatTariffPlanDuration(3, 'days', 'ru')).toBe('3 дня')
    expect(formatTariffPlanDuration(11, 'days', 'ru')).toBe('11 дней')
    expect(formatTariffPlanDuration(21, 'days', 'ru')).toBe('21 день')
    expect(formatTariffPlanDuration(22, 'days', 'ru')).toBe('22 дня')
    expect(formatTariffPlanDuration(25, 'days', 'ru')).toBe('25 дней')
  })

  it('declines every Russian unit rather than only the day', () => {
    expect(formatTariffPlanDuration(1, 'hours', 'ru')).toBe('1 час')
    expect(formatTariffPlanDuration(2, 'hours', 'ru')).toBe('2 часа')
    expect(formatTariffPlanDuration(5, 'months', 'ru')).toBe('5 месяцев')
    expect(formatTariffPlanDuration(1, 'years', 'ru')).toBe('1 год')
    expect(formatTariffPlanDuration(3, 'years', 'ru')).toBe('3 года')
    expect(formatTariffPlanDuration(12, 'years', 'ru')).toBe('12 лет')
  })

  it('writes English with the plural for everything but one', () => {
    expect(formatTariffPlanDuration(1, 'months', 'en')).toBe('1 month')
    expect(formatTariffPlanDuration(0, 'months', 'en')).toBe('0 months')
    expect(formatTariffPlanDuration(2, 'days', 'en')).toBe('2 days')
    expect(formatTariffPlanDuration(12, 'hours', 'en')).toBe('12 hours')
  })

  it('falls back to English for a language it does not write', () => {
    // `i18n.resolvedLanguage` is `undefined` before the bundle has resolved, and a third
    // language is a bundle this build does not ship - both read as the fallback bundle.
    expect(formatTariffPlanDuration(2, 'days', undefined)).toBe('2 days')
    expect(formatTariffPlanDuration(2, 'days', 'de')).toBe('2 days')
  })

  it('recognises a Russian locale with a region subtag', () => {
    expect(formatTariffPlanDuration(3, 'days', 'ru-RU')).toBe('3 дня')
  })
})
