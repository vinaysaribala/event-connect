import { StrictMode, type ReactNode } from 'react'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation, type InitialEntry } from 'react-router'
import { vi, type Mock } from 'vitest'
import { AuthContext, type AuthContextValue, type CurrentUser } from '../auth/AuthContext'

export const testUser: CurrentUser = {
  id: 7,
  email: 'maria@example.com',
  displayName: 'Maria Lopez',
  emailVerified: false,
  roles: ['Organizer'],
  timeZone: 'UTC',
}

/** A fetch Response with an optional JSON body, as the API sends. */
export function reply(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  })
}

/** An ASP.NET Core validation problem (400) with PascalCase field keys. */
export function validationProblem(errors: Record<string, string[]>): Response {
  return reply(400, { title: 'One or more validation errors occurred.', status: 400, errors })
}

/** Replaces fetch; each call gets the next reply, or the last one once they run out. */
export function stubFetch(...replies: Response[]): Mock<typeof fetch> {
  let index = 0
  const fetchMock = vi.fn<typeof fetch>(async () => (replies[Math.min(index++, replies.length - 1)] ?? reply(204)).clone())
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/**
 * Replaces fetch with replies chosen by request, keyed "METHOD /path" (e.g. "GET /api/events/12"). A list gives
 * successive replies to the same request, repeating the last. Unmatched requests get 404. Suits pages that load
 * several things at once, or load twice under StrictMode.
 */
export function routeFetch(routes: Record<string, Response | Response[]>): Mock<typeof fetch> {
  const calls = new Map<string, number>()
  const fetchMock = vi.fn<typeof fetch>(async (url, init) => {
    const key = `${init?.method ?? 'GET'} ${String(url)}`
    const route = routes[key]
    if (!route) return reply(404)
    const replies = Array.isArray(route) ? route : [route]
    const index = calls.get(key) ?? 0
    calls.set(key, index + 1)
    return replies[Math.min(index, replies.length - 1)].clone()
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** The [url, method, parsed body] of each fetch call. */
export function fetchCalls(fetchMock: Mock<typeof fetch>): [string, string | undefined, unknown][] {
  return fetchMock.mock.calls.map(([url, init]) => [String(url), init?.method, init?.body ? JSON.parse(String(init.body)) : undefined])
}

export function fakeAuth(overrides: Partial<AuthContextValue> = {}): AuthContextValue {
  return {
    user: null,
    loading: false,
    login: vi.fn(async () => {}),
    logout: vi.fn(async () => {}),
    refresh: vi.fn(async () => {}),
    updateUser: vi.fn(),
    ...overrides,
  }
}

/** Shows where the app navigated to, so tests can assert redirects. */
function LocationProbe() {
  const location = useLocation()
  const notice = (location.state as { notice?: string } | null)?.notice
  return (
    <div data-testid="location" data-notice={notice}>
      {location.pathname + location.search}
    </div>
  )
}

/**
 * Renders `ui` at `entry` with the given auth state, under StrictMode as in main.tsx (React only runs effects
 * twice when StrictMode wraps the root). Navigating anywhere else renders a probe; read it with `currentLocation()`.
 * Pass `path` (e.g. `/events/:id`) when the page reads route params; it defaults to the entry's own path.
 */
export function renderWithAuth(
  ui: ReactNode,
  { entry = '/', auth = fakeAuth(), path }: { entry?: InitialEntry; auth?: AuthContextValue; path?: string } = {},
) {
  path ??= (typeof entry === 'string' ? entry : (entry.pathname ?? '/')).split('?')[0]
  const result = render(
    <StrictMode>
      <AuthContext value={auth}>
        <MemoryRouter initialEntries={[entry]}>
          <Routes>
            <Route path={path} element={ui} />
            <Route path="*" element={<LocationProbe />} />
          </Routes>
        </MemoryRouter>
      </AuthContext>
    </StrictMode>,
  )
  return { ...result, auth }
}

export async function currentLocation(): Promise<HTMLElement> {
  return screen.findByTestId('location')
}
