import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api'
import { currentLocation, fakeAuth, renderWithAuth, testUser } from '../test/utils'
import { LoginPage } from './LoginPage'

async function signIn() {
  await userEvent.type(screen.getByLabelText('Email'), 'maria@example.com')
  await userEvent.type(screen.getByLabelText('Password'), 'correct-horse-42')
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('LoginPage', () => {
  it('signs in and goes to the dashboard', async () => {
    const auth = fakeAuth()
    renderWithAuth(<LoginPage />, { entry: '/login', auth })

    await signIn()

    expect(auth.login).toHaveBeenCalledWith('maria@example.com', 'correct-horse-42')
    expect(await currentLocation()).toHaveTextContent(/^\/dashboard$/)
  })

  it('goes back to the page the user originally tried to open', async () => {
    renderWithAuth(<LoginPage />, { entry: '/login?returnTo=%2Fprofile%3Ftab%3D1' })

    await signIn()

    expect(await currentLocation()).toHaveTextContent('/profile?tab=1')
  })

  it('ignores a returnTo that leaves the site', async () => {
    renderWithAuth(<LoginPage />, { entry: '/login?returnTo=%2F%2Fevil.example' })

    await signIn()

    expect(await currentLocation()).toHaveTextContent(/^\/dashboard$/)
  })

  it('shows the generic message when sign-in fails', async () => {
    const auth = fakeAuth({ login: vi.fn().mockRejectedValue(new ApiError(401, { title: 'Email or password is incorrect', status: 401 })) })
    renderWithAuth(<LoginPage />, { entry: '/login', auth })

    await signIn()

    expect(await screen.findByText('Email or password is incorrect')).toBeInTheDocument()
    expect(screen.queryByTestId('location')).not.toBeInTheDocument()
  })

  it('redirects a user who is already signed in', async () => {
    renderWithAuth(<LoginPage />, { entry: '/login?returnTo=%2Fprofile', auth: fakeAuth({ user: testUser }) })

    expect(await currentLocation()).toHaveTextContent('/profile')
  })

  it('shows a notice passed by the previous page, such as after a password reset', () => {
    renderWithAuth(<LoginPage />, { entry: { pathname: '/login', state: { notice: 'Your password has been changed.' } } })

    expect(screen.getByText('Your password has been changed.')).toBeInTheDocument()
  })

  it('links to forgot password and register', () => {
    renderWithAuth(<LoginPage />, { entry: '/login' })

    expect(screen.getByRole('link', { name: 'Forgot password?' })).toHaveAttribute('href', '/forgot-password')
    expect(screen.getByRole('link', { name: 'Create an account' })).toHaveAttribute('href', '/register')
  })
})
