import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { currentLocation, fakeAuth, renderWithAuth, testUser } from '../test/utils'
import { RequireAuth } from './RequireAuth'

describe('RequireAuth', () => {
  it('shows a spinner while the session is being checked', () => {
    renderWithAuth(<RequireAuth>secret</RequireAuth>, { entry: '/profile', auth: fakeAuth({ loading: true }) })

    expect(screen.getByLabelText('Loading')).toBeInTheDocument()
    expect(screen.queryByText('secret')).not.toBeInTheDocument()
  })

  it('redirects a signed-out visitor to sign in, remembering the page', async () => {
    renderWithAuth(<RequireAuth>secret</RequireAuth>, { entry: '/profile?tab=1' })

    expect(await currentLocation()).toHaveTextContent('/login?returnTo=%2Fprofile%3Ftab%3D1')
    expect(screen.queryByText('secret')).not.toBeInTheDocument()
  })

  it('shows the page to a signed-in user', () => {
    renderWithAuth(<RequireAuth>secret</RequireAuth>, { entry: '/profile', auth: fakeAuth({ user: testUser }) })

    expect(screen.getByText('secret')).toBeInTheDocument()
  })

  it('sends a user without the required role to the dashboard', async () => {
    renderWithAuth(<RequireAuth role="Administrator">admin only</RequireAuth>, { entry: '/admin', auth: fakeAuth({ user: testUser }) })

    expect(await currentLocation()).toHaveTextContent('/dashboard')
  })

  it('shows a role-restricted page to a user with the role', () => {
    const admin = { ...testUser, roles: ['Administrator', 'Organizer'] }

    renderWithAuth(<RequireAuth role="Administrator">admin only</RequireAuth>, { entry: '/admin', auth: fakeAuth({ user: admin }) })

    expect(screen.getByText('admin only')).toBeInTheDocument()
  })
})
