import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fetchCalls, reply, renderWithAuth, stubFetch, validationProblem } from '../test/utils'
import { ForgotPasswordPage } from './ForgotPasswordPage'

async function requestLink(email: string) {
  await userEvent.type(screen.getByLabelText('Email'), email)
  await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }))
}

describe('ForgotPasswordPage', () => {
  it("posts the email and always shows the same answer, without saying whether it's registered", async () => {
    const fetchMock = stubFetch(reply(202))
    renderWithAuth(<ForgotPasswordPage />, { entry: '/forgot-password' })

    await requestLink('maria@example.com')

    expect(await screen.findByText("If that address has an account, we've sent a reset link.")).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([['/api/auth/forgot-password', 'POST', { email: 'maria@example.com' }]])
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toHaveAttribute('href', '/login')
  })

  it('shows a malformed email error under the field', async () => {
    stubFetch(validationProblem({ Email: ['The Email field is not a valid e-mail address.'] }))
    renderWithAuth(<ForgotPasswordPage />, { entry: '/forgot-password' })

    await requestLink('nope')

    expect(await screen.findByText('The Email field is not a valid e-mail address.')).toHaveAttribute('id', 'email-help')
  })

  it('shows a general message for other failures', async () => {
    stubFetch(reply(500))
    renderWithAuth(<ForgotPasswordPage />, { entry: '/forgot-password' })

    await requestLink('maria@example.com')

    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
  })
})
