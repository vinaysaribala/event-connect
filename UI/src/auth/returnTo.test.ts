import { describe, expect, it } from 'vitest'
import { safeReturnTo } from './returnTo'

describe('safeReturnTo', () => {
  it.each(['/profile', '/events/3?tab=guests', '/'])('keeps the on-site path %s', (path) => {
    expect(safeReturnTo(path)).toBe(path)
  })

  it.each([null, '', 'profile', 'https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)'])(
    'sends %s to the dashboard instead',
    (value) => {
      expect(safeReturnTo(value)).toBe('/dashboard')
    },
  )
})
