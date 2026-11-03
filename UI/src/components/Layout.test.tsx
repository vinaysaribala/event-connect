import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeAuth, renderWithAuth, testUser } from '../test/utils'
import { Layout } from './Layout'

const banner = /Verify your email to publish events/

describe('Layout', () => {
  it('shows the unverified banner, linking to the profile, until the email is verified', () => {
    renderWithAuth(<Layout />, { auth: fakeAuth({ user: testUser }) })

    expect(screen.getByText(banner)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Resend the link' })).toHaveAttribute('href', '/profile')
  })

  it('hides the banner for a verified user', () => {
    renderWithAuth(<Layout />, { auth: fakeAuth({ user: { ...testUser, emailVerified: true } }) })

    expect(screen.queryByText(banner)).not.toBeInTheDocument()
  })

  it('offers sign in and register when signed out, with no banner', () => {
    renderWithAuth(<Layout />)

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument()
    expect(screen.queryByText(banner)).not.toBeInTheDocument()
  })

  it('shows the user name and signs out', async () => {
    const auth = fakeAuth({ user: testUser })
    renderWithAuth(<Layout />, { auth })

    expect(screen.getByRole('button', { name: 'Maria Lopez' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(auth.logout).toHaveBeenCalledOnce()
  })
})
