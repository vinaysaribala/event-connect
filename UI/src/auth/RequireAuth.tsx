import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { ProgressSpinner } from 'primereact/progressspinner'
import { useAuth } from './useAuth'

/** Shows its children only to a signed-in user (with `role`, if given); otherwise redirects to sign in. */
export function RequireAuth({ children, role }: { children: ReactNode; role?: string }) {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '3rem' }}>
        <ProgressSpinner aria-label="Loading" />
      </div>
    )
  }
  if (!user) {
    return <Navigate to={`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`} replace />
  }
  if (role && !user.roles.includes(role)) {
    return <Navigate to="/dashboard" replace />
  }
  return children
}
