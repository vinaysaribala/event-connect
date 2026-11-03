import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { testEvent } from '../test/events'
import { currentLocation, fakeAuth, renderWithAuth, reply, routeFetch, testUser } from '../test/utils'
import { DashboardPage } from './DashboardPage'

function renderDashboard() {
  return renderWithAuth(<DashboardPage />, { entry: '/dashboard', auth: fakeAuth({ user: testUser }) })
}

describe('DashboardPage', () => {
  it('lists the organizer’s events with status and start, linking to each', async () => {
    routeFetch({
      'GET /api/events': reply(200, [testEvent({ status: 'Published' }), testEvent({ id: 13, title: 'Undated', startsAt: null, timeZone: 'UTC' })]),
    })
    renderDashboard()

    expect(await screen.findByRole('link', { name: "Maya's 30th Birthday" })).toHaveAttribute('href', '/events/12')
    expect(screen.getByText('Published')).toBeInTheDocument()
    expect(screen.getByText('Thu, Dec 5, 2030, 7:00 PM (America/New_York)')).toBeInTheDocument()
    expect(screen.getByText('Not set')).toBeInTheDocument()
  })

  it('invites the organizer to create a first event', async () => {
    routeFetch({ 'GET /api/events': reply(200, []) })
    renderDashboard()

    expect(await screen.findByText('No events yet. Create your first one.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'New event' }))
    expect(await currentLocation()).toHaveTextContent('/events/new')
  })
})
