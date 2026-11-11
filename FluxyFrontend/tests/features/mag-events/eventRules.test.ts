import { describe, expect, it } from 'vitest'
import {
  MESSAGE_MAX_LENGTH,
  eventMessageRule,
  messageErrorFor,
} from '../../../src/features/mag-events/eventRules'
import { MAG_EVENT_TYPES } from '../../../src/features/mag-events/events'
import type { MagEventType } from '../../../src/features/mag-events/types'

/**
 * The send rules mirrored from the server's `MagEventRules`, pinned case for case against
 * `MagEventRulesTests` in the backend.
 *
 * Why this exists at all: the send form refuses a value before a request is made, and the
 * refusal has to be the one the server would have given. A rule that drifted - a field the
 * form required and the server dropped, a shape the server accepts and the form rejects -
 * would be invisible to `typecheck`, `lint`, `build` and `probe:render`, because nothing in
 * any of them sends a body. The cases below are transcribed from the backend's own theory
 * data, so the two lists must be edited together.
 */

/** One row of the table below: an operation, a message, and what the form should say. */
const CASES: {
  event: MagEventType
  rule: 'none' | 'text' | 'channel'
  message: string | null
  error: 'required' | 'tooLong' | 'channel' | null
}[] = [
  // sendMessage requires a non-blank message, trimmed at its edges.
  { event: 'sendMessage', rule: 'text', message: 'Hello', error: null },
  { event: 'sendMessage', rule: 'text', message: '  Hello there  ', error: null },
  { event: 'sendMessage', rule: 'text', message: null, error: 'required' },
  { event: 'sendMessage', rule: 'text', message: '', error: 'required' },
  { event: 'sendMessage', rule: 'text', message: '   ', error: 'required' },
  // playChannel requires the stream identifier, and it must parse as a uuid.
  { event: 'playChannel', rule: 'channel', message: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', error: null },
  { event: 'playChannel', rule: 'channel', message: ' AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA ', error: null },
  { event: 'playChannel', rule: 'channel', message: 'not-a-channel', error: 'channel' },
  { event: 'playChannel', rule: 'channel', message: '42', error: 'channel' },
  { event: 'playChannel', rule: 'channel', message: '', error: 'required' },
  // The other four carry no payload at all - everything sent for them is dropped, so nothing
  // about a message can be wrong.
  { event: 'closePortal', rule: 'none', message: 'ignored payload', error: null },
  { event: 'closePortal', rule: 'none', message: null, error: null },
  { event: 'reloadPortal', rule: 'none', message: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', error: null },
  { event: 'reloadPortal', rule: 'none', message: '   ', error: null },
  { event: 'rebootDevice', rule: 'none', message: null, error: null },
  { event: 'rebootDevice', rule: 'none', message: 'ignored', error: null },
  { event: 'resetStbLock', rule: 'none', message: null, error: null },
  { event: 'resetStbLock', rule: 'none', message: 'ignored', error: null },
]

describe('the MAG event send rules', () => {
  it('offers the six operations in the server’s own wire order', () => {
    expect([...MAG_EVENT_TYPES]).toEqual([
      'playChannel',
      'closePortal',
      'reloadPortal',
      'rebootDevice',
      'sendMessage',
      'resetStbLock',
    ])
  })

  it.each(CASES)(
    'says $error for $event carrying $message',
    ({ event, rule, message, error }) => {
      expect(eventMessageRule(event)).toBe(rule)
      expect(messageErrorFor(event, message)).toBe(error)
    },
  )

  it('accepts a message at the column width and refuses one character more', () => {
    // The server trims before it measures, so the ceiling is on the trimmed value.
    const atWidth = 'm'.repeat(MESSAGE_MAX_LENGTH)
    const oneLonger = 'm'.repeat(MESSAGE_MAX_LENGTH + 1)

    expect(MESSAGE_MAX_LENGTH).toBe(1000)
    expect(messageErrorFor('sendMessage', atWidth)).toBeNull()
    expect(messageErrorFor('sendMessage', oneLonger)).toBe('tooLong')
    expect(messageErrorFor('sendMessage', `  ${oneLonger}  `)).toBe('tooLong')
  })

  it('measures before it judges the uuid shape, as the server does', () => {
    // A uuid-shaped string past the column is `tooLong`, not `channel`: the two refusals are
    // ordered on the server, and a form that swapped them would name the wrong problem.
    const longUuid = `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa${'x'.repeat(MESSAGE_MAX_LENGTH)}`

    expect(messageErrorFor('playChannel', longUuid)).toBe('tooLong')
  })

  it('never asks for a message on an operation the server stores without one', () => {
    for (const event of MAG_EVENT_TYPES) {
      if (eventMessageRule(event) === 'none') {
        expect(messageErrorFor(event, null)).toBeNull()
        expect(messageErrorFor(event, 'anything at all')).toBeNull()
      }
    }
  })
})
