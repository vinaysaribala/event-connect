import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, apiFetch, onUnauthorized } from '../api'
import { AuthContext, type AuthContextValue, type CurrentUser } from './AuthContext'

/** The signed-in user, or null when signed out (GET /api/me answers 401). */
async function loadCurrentUser(): Promise<CurrentUser | null> {
  try {
    return await apiFetch<CurrentUser>('/api/me', { handleUnauthorized: false })
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) console.error(error)
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)
  const navigate = useNavigate()

  const refresh = useCallback(async () => setUser(await loadCurrentUser()), [])

  useEffect(() => {
    let active = true
    void loadCurrentUser().then((current) => {
      if (!active) return
      setUser(current)
      setLoading(false)
    })
    return () => {
      active = false
    }
  }, [])

  // Any 401 from a signed-in request means the session ended: forget the user and go to sign in.
  useEffect(
    () =>
      onUnauthorized(() => {
        setUser(null)
        const { pathname, search } = window.location
        if (pathname !== '/login') {
          navigate(`/login?returnTo=${encodeURIComponent(pathname + search)}`, { replace: true })
        }
      }),
    [navigate],
  )

  const login = useCallback(
    async (email: string, password: string) => {
      await apiFetch('/api/auth/login', { method: 'POST', body: { email, password }, handleUnauthorized: false })
      await refresh()
    },
    [refresh],
  )

  const logout = useCallback(async () => {
    await apiFetch('/api/auth/logout', { method: 'POST', handleUnauthorized: false })
    setUser(null)
    navigate('/login', { replace: true })
  }, [navigate])

  const value = useMemo<AuthContextValue>(
    () => ({ user, loading, login, logout, refresh, updateUser: setUser }),
    [user, loading, login, logout, refresh],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
