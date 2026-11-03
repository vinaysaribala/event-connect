import { describe, expect, it, vi } from 'vitest'
import { ApiError, apiFetch, errorMessage, onUnauthorized } from './api'
import { reply, stubFetch, validationProblem } from './test/utils'

describe('apiFetch', () => {
  it('sends JSON to a relative URL with same-origin credentials and returns the parsed body', async () => {
    const fetchMock = stubFetch(reply(200, { id: 7 }))

    const result = await apiFetch<{ id: number }>('/api/auth/login', { method: 'POST', body: { email: 'a@b.c' } })

    expect(result).toEqual({ id: 7 })
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: '{"email":"a@b.c"}',
    })
  })

  it('sends GET without a body or content type by default', async () => {
    const fetchMock = stubFetch(reply(200, {}))

    await apiFetch('/api/me')

    expect(fetchMock).toHaveBeenCalledWith('/api/me', { method: 'GET', credentials: 'same-origin', headers: undefined, body: undefined })
  })

  it('returns undefined for an empty response such as 204', async () => {
    stubFetch(reply(204))

    await expect(apiFetch('/api/auth/logout', { method: 'POST' })).resolves.toBeUndefined()
  })

  it('throws an ApiError carrying the status and problem details', async () => {
    stubFetch(reply(409, { title: 'An account with this email already exists.', status: 409 }))

    const error = await apiFetch('/api/auth/register', { method: 'POST', body: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, message: 'An account with this email already exists.' })
  })

  it('treats a 401 as "session ended" and tells the registered handler', async () => {
    const handler = vi.fn()
    const unsubscribe = onUnauthorized(handler)
    stubFetch(reply(401))

    await expect(apiFetch('/api/me')).rejects.toBeInstanceOf(ApiError)

    expect(handler).toHaveBeenCalledOnce()
    unsubscribe()
  })

  it('leaves a 401 alone when handleUnauthorized is false (sign-in, session check)', async () => {
    const handler = vi.fn()
    const unsubscribe = onUnauthorized(handler)
    stubFetch(reply(401, { title: 'Email or password is incorrect' }))

    await expect(apiFetch('/api/auth/login', { method: 'POST', body: {}, handleUnauthorized: false })).rejects.toBeInstanceOf(ApiError)

    expect(handler).not.toHaveBeenCalled()
    unsubscribe()
  })

  it('does not call the handler for other errors, or after unsubscribing', async () => {
    const handler = vi.fn()
    const unsubscribe = onUnauthorized(handler)
    stubFetch(reply(403))
    await expect(apiFetch('/api/me')).rejects.toBeInstanceOf(ApiError)

    unsubscribe()
    stubFetch(reply(401))
    await expect(apiFetch('/api/me')).rejects.toBeInstanceOf(ApiError)

    expect(handler).not.toHaveBeenCalled()
  })
})

describe('ApiError.fieldError', () => {
  it("matches the API's PascalCase keys without regard to case and returns the first message", async () => {
    stubFetch(validationProblem({ NewPassword: ['Too short.', 'Needs more.'] }))

    const error = (await apiFetch('/api/auth/reset-password', { method: 'POST', body: {} }).catch((e: unknown) => e)) as ApiError

    expect(error.fieldError('newPassword')).toBe('Too short.')
    expect(error.fieldError('token')).toBeUndefined()
  })

  it('returns undefined when there are no field errors', () => {
    expect(new ApiError(500, undefined).fieldError('email')).toBeUndefined()
  })
})

describe('errorMessage', () => {
  it('asks the user to wait after too many attempts (429)', () => {
    expect(errorMessage(new ApiError(429, undefined))).toBe('Too many attempts. Wait a minute and try again.')
  })

  it("shows the API's title for client errors", () => {
    expect(errorMessage(new ApiError(401, { title: 'Email or password is incorrect' }))).toBe('Email or password is incorrect')
  })

  it.each([new ApiError(500, { title: 'NullReferenceException' }), new ApiError(400, {}), new TypeError('Failed to fetch')])(
    'falls back to a generic sentence for %s',
    (error) => {
      expect(errorMessage(error)).toBe('Something went wrong. Please try again.')
    },
  )
})
