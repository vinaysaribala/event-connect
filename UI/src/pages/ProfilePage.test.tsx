import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { fakeAuth, fetchCalls, reply, renderWithAuth, stubFetch, testUser, validationProblem } from '../test/utils'
import { ProfilePage } from './ProfilePage'

function renderProfile(user = testUser) {
  return renderWithAuth(<ProfilePage />, { entry: '/profile', auth: fakeAuth({ user }) })
}

describe('ProfilePage', () => {
  it('shows the email read-only, the display name and the time zone', () => {
    renderProfile()

    expect(screen.getByLabelText('Email')).toHaveValue('maria@example.com')
    expect(screen.getByLabelText('Email')).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Display name')).toHaveValue('Maria Lopez')
    expect(screen.getByText('UTC', { selector: '.p-dropdown-label' })).toBeInTheDocument()
  })

  it('shows "Email not verified" with a Resend button for an unverified user', () => {
    renderProfile()

    expect(screen.getByText('Email not verified')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Resend' })).toBeInTheDocument()
  })

  it('shows "Email verified" and no Resend button once verified', () => {
    renderProfile({ ...testUser, emailVerified: true })

    expect(screen.getByText('Email verified')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resend' })).not.toBeInTheDocument()
  })

  it('resends the verification link', async () => {
    const fetchMock = stubFetch(reply(202))
    renderProfile()

    await userEvent.click(screen.getByRole('button', { name: 'Resend' }))

    expect(await screen.findByText("We've sent a new link to maria@example.com.")).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([['/api/auth/resend-verification', 'POST', undefined]])
  })

  it('saves the profile through PUT /api/me and updates the signed-in user', async () => {
    const updated = { ...testUser, displayName: 'Maria L.' }
    const fetchMock = stubFetch(reply(200, updated))
    const { auth } = renderProfile()
    const name = screen.getByLabelText('Display name')

    await userEvent.clear(name)
    await userEvent.type(name, 'Maria L.')
    await userEvent.click(screen.getByRole('button', { name: 'Save profile' }))

    expect(await screen.findByText('Profile saved.')).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toEqual([['/api/me', 'PUT', { displayName: 'Maria L.', timeZone: 'UTC' }]])
    expect(auth.updateUser).toHaveBeenCalledWith(updated)
  })

  it('shows validation errors under their fields', async () => {
    stubFetch(
      validationProblem({
        DisplayName: ['The DisplayName field is required.'],
        TimeZone: ['Unknown time zone. Use an IANA name such as America/New_York.'],
      }),
    )
    renderProfile()

    await userEvent.clear(screen.getByLabelText('Display name'))
    await userEvent.click(screen.getByRole('button', { name: 'Save profile' }))

    expect(await screen.findByText('The DisplayName field is required.')).toHaveAttribute('id', 'displayName-help')
    expect(screen.getByText(/Unknown time zone/)).toHaveAttribute('id', 'timeZone-help')
  })
})
