import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { currentLocation, fetchCalls, reply, renderWithAuth, stubFetch, validationProblem } from '../test/utils'
import { ResetPasswordPage } from './ResetPasswordPage'

const invalidLink = 'This link is invalid, has already been used, or has expired.'

async function choosePassword(password: string, confirm = password) {
  await userEvent.type(screen.getByLabelText('New password'), password)
  await userEvent.type(screen.getByLabelText('Confirm new password'), confirm)
  await userEvent.click(screen.getByRole('button', { name: 'Change password' }))
}

describe('ResetPasswordPage', () => {
  it('resets the password with the link token and goes to sign in with a notice', async () => {
    const fetchMock = stubFetch(reply(204))
    renderWithAuth(<ResetPasswordPage />, { entry: '/reset-password?token=abc%2B1' })

    await choosePassword('another-strong-pass')

    const location = await currentLocation()
    expect(location).toHaveTextContent(/^\/login$/)
    expect(location).toHaveAttribute('data-notice', 'Your password has been changed. Sign in with your new password.')
    expect(fetchCalls(fetchMock)).toEqual([['/api/auth/reset-password', 'POST', { token: 'abc+1', newPassword: 'another-strong-pass' }]])
  })

  it("stops when the two passwords don't match", async () => {
    const fetchMock = stubFetch(reply(204))
    renderWithAuth(<ResetPasswordPage />, { entry: '/reset-password?token=abc' })

    await choosePassword('another-strong-pass', 'another-strong-pas')

    expect(screen.getByText("The passwords don't match.")).toHaveAttribute('id', 'confirmPassword-help')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows the invalid link screen when the API rejects the token', async () => {
    stubFetch(validationProblem({ Token: ['This link is invalid or has expired.'] }))
    renderWithAuth(<ResetPasswordPage />, { entry: '/reset-password?token=used' })

    await choosePassword('another-strong-pass')

    expect(await screen.findByText(invalidLink)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Request a new reset link' })).toHaveAttribute('href', '/forgot-password')
  })

  it('shows the invalid link screen straight away when there is no token', () => {
    renderWithAuth(<ResetPasswordPage />, { entry: '/reset-password' })

    expect(screen.getByText(invalidLink)).toBeInTheDocument()
  })

  it('shows a weak password error under the new password field', async () => {
    stubFetch(validationProblem({ NewPassword: ['Passwords must be at least 10 characters.'] }))
    renderWithAuth(<ResetPasswordPage />, { entry: '/reset-password?token=abc' })

    await choosePassword('short')

    expect(await screen.findByText('Passwords must be at least 10 characters.')).toHaveAttribute('id', 'newPassword-help')
  })
})
