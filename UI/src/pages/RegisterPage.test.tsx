import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fetchCalls, reply, renderWithAuth, stubFetch, validationProblem } from '../test/utils'
import { RegisterPage } from './RegisterPage'

async function fillIn(email = 'maria@example.com', displayName = 'Maria Lopez', password = 'correct-horse-42') {
  await userEvent.type(screen.getByLabelText('Email'), email)
  await userEvent.type(screen.getByLabelText('Display name'), displayName)
  await userEvent.type(screen.getByLabelText('Password'), password)
  await userEvent.click(screen.getByRole('button', { name: 'Create account' }))
}

describe('RegisterPage', () => {
  it('registers, refreshes the signed-in user and asks the user to check their email', async () => {
    const fetchMock = stubFetch(reply(201, { id: 7, email: 'maria@example.com', displayName: 'Maria Lopez', emailVerified: false }))
    const { auth } = renderWithAuth(<RegisterPage />, { entry: '/register' })

    await fillIn()

    expect(await screen.findByText('Check your email to verify your account.')).toBeInTheDocument()
    expect(screen.getByText(/We sent a link to maria@example.com/)).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([
      ['/api/auth/register', 'POST', { email: 'maria@example.com', password: 'correct-horse-42', displayName: 'Maria Lopez' }],
    ])
    expect(auth.refresh).toHaveBeenCalledOnce()
  })

  it('shows the 409 message under the email field', async () => {
    stubFetch(reply(409, { title: 'An account with this email already exists.', status: 409 }))
    renderWithAuth(<RegisterPage />, { entry: '/register' })

    await fillIn()

    expect(await screen.findByText('An account with this email already exists.')).toHaveAttribute('id', 'email-help')
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-describedby', 'email-help')
  })

  it('shows each validation error under its field', async () => {
    stubFetch(
      validationProblem({
        Email: ['The Email field is not a valid e-mail address.'],
        Password: ['The field Password must be a string or array type with a minimum length of \'10\'.'],
        DisplayName: ['The DisplayName field is required.'],
      }),
    )
    renderWithAuth(<RegisterPage />, { entry: '/register' })

    await fillIn('bad', ' ', 'short')

    expect(await screen.findByText('The Email field is not a valid e-mail address.')).toHaveAttribute('id', 'email-help')
    expect(screen.getByText('The DisplayName field is required.')).toHaveAttribute('id', 'displayName-help')
    expect(screen.getByText(/minimum length of '10'/)).toHaveAttribute('id', 'password-help')
  })

  it('shows a general message for other failures', async () => {
    stubFetch(reply(429))
    renderWithAuth(<RegisterPage />, { entry: '/register' })

    await fillIn()

    expect(await screen.findByText('Too many attempts. Wait a minute and try again.')).toBeInTheDocument()
  })

  it('hints at password strength as the user types', async () => {
    renderWithAuth(<RegisterPage />, { entry: '/register' })
    const password = screen.getByLabelText('Password')

    expect(screen.getByText('At least 10 characters. A longer passphrase is stronger.')).toBeInTheDocument()
    await userEvent.type(password, 'abc')
    expect(screen.getByText('Strength: too short (3 of 10 characters).')).toBeInTheDocument()
    await userEvent.type(password, 'defghij')
    expect(screen.getByText('Strength: good. Longer, with numbers and symbols, is stronger.')).toBeInTheDocument()
    await userEvent.type(password, '-42!')
    expect(screen.getByText('Strength: strong.')).toBeInTheDocument()
  })

  it('links to sign in', () => {
    renderWithAuth(<RegisterPage />, { entry: '/register' })

    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute('href', '/login')
  })
})
