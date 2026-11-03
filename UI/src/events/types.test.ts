import { describe, expect, it } from 'vitest'
import { testEvent } from '../test/events'
import { formatLocal, toInput } from './types'

describe('formatLocal', () => {
  it('shows the wall-clock time as entered, without converting zones', () => {
    expect(formatLocal('2026-12-05T19:00:00')).toBe('Sat, Dec 5, 2026, 7:00 PM')
  })

  it('returns an empty string for no value', () => {
    expect(formatLocal(null)).toBe('')
  })
})

describe('toInput', () => {
  it('keeps only the editable fields', () => {
    const input = toInput(testEvent())

    expect(Object.keys(input)).not.toContain('id')
    expect(Object.keys(input)).not.toContain('status')
    expect(Object.keys(input)).not.toContain('createdAt')
    expect(input.title).toBe("Maya's 30th Birthday")
  })
})
