import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { EventDto } from '../events/types'
import { testCategories, testEvent } from '../test/events'
import { currentLocation, fakeAuth, fetchCalls, reply, renderWithAuth, routeFetch, testUser, validationProblem } from '../test/utils'
import { EventOverviewPage } from './EventOverviewPage'

function renderOverview(event: EventDto | null, routes: Record<string, Response> = {}) {
  const fetchMock = routeFetch({
    'GET /api/categories': reply(200, testCategories),
    ...(event ? { [`GET /api/events/${event.id}`]: reply(200, event) } : {}),
    ...routes,
  })
  renderWithAuth(<EventOverviewPage />, { entry: '/events/12', path: '/events/:id', auth: fakeAuth({ user: testUser }) })
  return fetchMock
}

const heading = () => screen.findByRole('heading', { name: "Maya's 30th Birthday" })
const button = (name: string) => screen.queryByRole('button', { name })

describe('EventOverviewPage', () => {
  it('shows the details of the event', async () => {
    renderOverview(testEvent())

    expect(await heading()).toBeInTheDocument()
    expect(screen.getByText('Draft')).toBeInTheDocument()
    expect(screen.getByText('Thu, Dec 5, 2030, 7:00 PM – Thu, Dec 5, 2030, 11:00 PM (America/New_York)')).toBeInTheDocument()
    expect(await screen.findByText('Birthday')).toBeInTheDocument()
    expect(screen.getByText('Skyline Rooftop')).toBeInTheDocument()
    expect(screen.getByText('Up to 1 per guest')).toBeInTheDocument()
  })

  it('offers the actions a draft allows', async () => {
    renderOverview(testEvent())
    await heading()

    for (const name of ['Edit', 'Publish', 'Duplicate', 'Archive', 'Delete']) expect(button(name)).toBeInTheDocument()
    expect(button('Cancel event')).not.toBeInTheDocument()
  })

  it('offers the actions a published event allows', async () => {
    renderOverview(testEvent({ status: 'Published' }))
    await heading()

    for (const name of ['Edit', 'Cancel event', 'Duplicate', 'Archive']) expect(button(name)).toBeInTheDocument()
    expect(button('Publish')).not.toBeInTheDocument()
    expect(button('Delete')).not.toBeInTheDocument()
  })

  it('offers only duplicate and archive for a cancelled event', async () => {
    renderOverview(testEvent({ status: 'Cancelled' }))
    await heading()

    expect(button('Edit')).not.toBeInTheDocument()
    expect(button('Duplicate')).toBeInTheDocument()
    expect(button('Archive')).toBeInTheDocument()
  })

  it('publishes a draft', async () => {
    renderOverview(testEvent(), { 'POST /api/events/12/publish': reply(200, testEvent({ status: 'Published' })) })

    await userEvent.click(await screen.findByRole('button', { name: 'Publish' }))

    expect(await screen.findByText('Event published.')).toBeInTheDocument()
    expect(screen.getByText('Published')).toBeInTheDocument()
  })

  it('lists what is missing when publishing fails', async () => {
    renderOverview(testEvent({ categoryId: null }), {
      'POST /api/events/12/publish': reply(400, { title: 'Event is not ready to publish.', status: 400, errors: { CategoryId: ['Choose a category.'] } }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Publish' }))

    expect(await screen.findByText('Event is not ready to publish.')).toBeInTheDocument()
    expect(screen.getByText('Choose a category.')).toBeInTheDocument()
  })

  it('cancels a published event with a message for guests', async () => {
    const fetchMock = renderOverview(testEvent({ status: 'Published' }), {
      'POST /api/events/12/cancel': reply(200, testEvent({ status: 'Cancelled' })),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel event' }))
    const dialog = await screen.findByRole('dialog', { name: 'Cancel this event?' })
    await userEvent.type(within(dialog).getByLabelText('Message to guests (optional)'), 'Storm')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel event' }))

    expect(await screen.findByText('Event cancelled.')).toBeInTheDocument()
    expect(fetchCalls(fetchMock)).toContainEqual(['/api/events/12/cancel', 'POST', { message: 'Storm' }])
  })

  it('shows a rejected cancel message as a notice', async () => {
    renderOverview(testEvent({ status: 'Published' }), {
      'POST /api/events/12/cancel': validationProblem({ Message: ['The field Message must be a string with a maximum length of 1000.'] }),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel event' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel event' }))

    expect(await screen.findByText(/maximum length of 1000/)).toBeInTheDocument()
  })

  it('deletes a draft after confirming and returns to the dashboard', async () => {
    const fetchMock = renderOverview(testEvent(), { 'DELETE /api/events/12': reply(204) })

    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Delete this draft?' })).getByRole('button', { name: 'Delete' }))

    const location = await currentLocation()
    expect(location).toHaveTextContent('/dashboard')
    expect(location).toHaveAttribute('data-notice', 'Draft deleted.')
    expect(fetchCalls(fetchMock)).toContainEqual(['/api/events/12', 'DELETE', undefined])
  })

  it('archives after confirming', async () => {
    renderOverview(testEvent(), { 'POST /api/events/12/archive': reply(200, testEvent({ status: 'Archived' })) })

    await userEvent.click(await screen.findByRole('button', { name: 'Archive' }))
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Archive this event?' })).getByRole('button', { name: 'Archive' }))

    expect(await screen.findByText('Event archived.')).toBeInTheDocument()
    await waitFor(() => expect(button('Archive')).not.toBeInTheDocument())
  })

  it('duplicates and opens the copy', async () => {
    const copy = testEvent({ id: 13, title: "Maya's 30th Birthday (copy)" })
    renderOverview(testEvent({ status: 'Published' }), {
      'POST /api/events/12/duplicate': reply(201, copy),
      'GET /api/events/13': reply(200, copy),
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Duplicate' }))

    expect(await screen.findByRole('heading', { name: "Maya's 30th Birthday (copy)" })).toBeInTheDocument()
    expect(screen.getByText('Copy created as a draft. Guests weren’t copied.')).toBeInTheDocument()
  })

  it('says when the event isn’t found', async () => {
    renderOverview(null)

    expect(await screen.findByText('This event doesn’t exist or isn’t yours.')).toBeInTheDocument()
  })
})
