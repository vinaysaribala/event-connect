import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { currentLocation, fakeAuth, fetchCalls, reply, renderWithAuth, stubFetch, testUser, validationProblem } from '../test/utils'
import { VerifyEmailPage } from './VerifyEmailPage'

const failed = "We couldn't verify your email"

describe('VerifyEmailPage', () => {
  it('verifies the token on load, refreshes the user and offers the dashboard', async () => {
    const fetchMock = stubFetch(reply(204))
    const { auth } = renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email?token=abc%2B1' })

    expect(screen.getByLabelText('Verifying')).toBeInTheDocument()
    expect(await screen.findByText('Email verified')).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([['/api/auth/verify-email', 'POST', { token: 'abc+1' }]])
    expect(auth.refresh).toHaveBeenCalledOnce()

    await userEvent.click(screen.getByRole('button', { name: 'Go to dashboard' }))
    expect(await currentLocation()).toHaveTextContent('/dashboard')
  })

  it('uses the single-use link only once, even when StrictMode runs effects twice', async () => {
    const fetchMock = stubFetch(reply(204))

    renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email?token=abc' })

    expect(await screen.findByText('Email verified')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledOnce()
  })

  it('shows an error with "Send a new link" when the link is invalid, used or expired', async () => {
    stubFetch(validationProblem({ Token: ['This link is invalid or has expired.'] }))
    renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email?token=used' })

    expect(await screen.findByText(failed)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Send a new link' })).toBeInTheDocument()
  })

  it('fails without calling the API when there is no token', () => {
    const fetchMock = stubFetch(reply(204))

    renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email' })

    expect(screen.getByText(failed)).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends a signed-out user to sign in, then the profile, to get a new link', async () => {
    stubFetch(reply(400))
    renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email?token=old' })

    await userEvent.click(await screen.findByRole('button', { name: 'Send a new link' }))

    expect(await currentLocation()).toHaveTextContent('/login?returnTo=%2Fprofile')
  })

  it('resends the link for a signed-in user', async () => {
    const fetchMock = stubFetch(reply(400), reply(202))
    renderWithAuth(<VerifyEmailPage />, { entry: '/verify-email?token=old', auth: fakeAuth({ user: testUser }) })

    await userEvent.click(await screen.findByRole('button', { name: 'Send a new link' }))

    expect(await screen.findByText("We've sent a new link to maria@example.com.")).toBeInTheDocument()
    expect(fetchCalls(fetchMock)[1]).toEqual(['/api/auth/resend-verification', 'POST', undefined])
  })
})
