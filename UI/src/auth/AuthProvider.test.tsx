import { render, renderHook, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { describe, expect, it } from 'vitest'
import { apiFetch } from '../api'
import { fetchCalls, reply, stubFetch, testUser } from '../test/utils'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './useAuth'

function AuthState() {
  const { user, loading, login, logout } = useAuth()
  const location = useLocation()
  return (
    <>
      <p>{loading ? 'loading' : user ? `signed in as ${user.email}` : 'signed out'}</p>
      <p data-testid="location">{location.pathname + location.search}</p>
      <button onClick={() => void login('maria@example.com', 'correct-horse-42')}>login</button>
      <button onClick={() => void logout()}>logout</button>
      <button onClick={() => void apiFetch('/api/events').catch(() => {})}>load events</button>
    </>
  )
}

function renderProvider(path = '/dashboard') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <Routes>
          <Route path="*" element={<AuthState />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('AuthProvider', () => {
  it('loads the signed-in user from GET /api/me on start', async () => {
    const fetchMock = stubFetch(reply(200, testUser))

    renderProvider()

    expect(screen.getByText('loading')).toBeInTheDocument()
    expect(await screen.findByText('signed in as maria@example.com')).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([['/api/me', 'GET', undefined]])
  })

  it('treats a 401 from GET /api/me as signed out, without redirecting', async () => {
    stubFetch(reply(401))

    renderProvider('/')

    expect(await screen.findByText('signed out')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(/^\/$/)
  })

  it('signs in through POST /api/auth/login, then reloads the user', async () => {
    const fetchMock = stubFetch(reply(401), reply(200, { ...testUser, timeZone: undefined }), reply(200, testUser))
    renderProvider('/login')
    await screen.findByText('signed out')

    await userEvent.click(screen.getByRole('button', { name: 'login' }))

    expect(await screen.findByText('signed in as maria@example.com')).toBeInTheDocument()
    expect(fetchCalls(fetchMock).slice(1)).toEqual([
      ['/api/auth/login', 'POST', { email: 'maria@example.com', password: 'correct-horse-42' }],
      ['/api/me', 'GET', undefined],
    ])
  })

  it('signs out through POST /api/auth/logout and goes to sign in', async () => {
    const fetchMock = stubFetch(reply(200, testUser), reply(204))
    renderProvider('/profile')
    await screen.findByText('signed in as maria@example.com')

    await userEvent.click(screen.getByRole('button', { name: 'logout' }))

    expect(await screen.findByText('signed out')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/login')
    expect(fetchCalls(fetchMock)[1]).toEqual(['/api/auth/logout', 'POST', undefined])
  })

  it('forgets the user and redirects to sign in, with returnTo, when any request returns 401', async () => {
    window.history.pushState({}, '', '/profile?tab=1')
    stubFetch(reply(200, testUser), reply(401))
    renderProvider('/profile?tab=1')
    await screen.findByText('signed in as maria@example.com')

    await userEvent.click(screen.getByRole('button', { name: 'load events' }))

    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/login?returnTo=%2Fprofile%3Ftab%3D1'))
    expect(screen.getByText('signed out')).toBeInTheDocument()
    window.history.pushState({}, '', '/')
  })

  it('keeps no tokens in browser storage', async () => {
    stubFetch(reply(200, testUser))

    renderProvider()
    await screen.findByText('signed in as maria@example.com')

    expect(localStorage.length).toBe(0)
    expect(sessionStorage.length).toBe(0)
  })
})

describe('useAuth', () => {
  it('fails clearly outside <AuthProvider>', () => {
    expect(() => renderHook(() => useAuth())).toThrow('useAuth must be used inside <AuthProvider>')
  })
})
