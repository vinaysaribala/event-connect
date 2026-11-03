import { fireEvent, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { testCategories, testEvent } from '../test/events'
import { currentLocation, fakeAuth, fetchCalls, reply, renderWithAuth, routeFetch, testUser, validationProblem } from '../test/utils'
import { EventEditorPage } from './EventEditorPage'

function renderNew() {
  return renderWithAuth(<EventEditorPage />, { entry: '/events/new', auth: fakeAuth({ user: testUser }) })
}

function renderEdit() {
  return renderWithAuth(<EventEditorPage />, { entry: '/events/12/edit', path: '/events/:id/edit', auth: fakeAuth({ user: testUser }) })
}

/** The fetch calls that change something (not GET). */
function writes(fetchMock: Parameters<typeof fetchCalls>[0]) {
  return fetchCalls(fetchMock).filter(([, method]) => method && method !== 'GET')
}

describe('EventEditorPage', () => {
  it('saves a new draft with only a title, in the profile time zone', async () => {
    const fetchMock = routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'POST /api/events': reply(201, testEvent({ title: 'Party' })),
    })
    renderNew()

    expect(screen.getByText('UTC', { selector: '.p-dropdown-label' })).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Title'), 'Party')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))

    const location = await currentLocation()
    expect(location).toHaveTextContent('/events/12')
    expect(location).toHaveAttribute('data-notice', 'Draft saved.')
    const [[url, , body]] = writes(fetchMock)
    expect(url).toBe('/api/events')
    expect(body).toMatchObject({ title: 'Party', timeZone: 'UTC', format: null, visibility: 'Private', allowPlusOnes: false })
  })

  it('shows the location fields that match the format', async () => {
    routeFetch({ 'GET /api/categories': reply(200, testCategories) })
    renderNew()

    expect(screen.queryByLabelText('Venue name')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'In person' }))
    expect(screen.getByLabelText('Venue name')).toBeInTheDocument()
    expect(screen.getByLabelText('Address')).toBeInTheDocument()
    expect(screen.queryByLabelText('Meeting link')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Virtual' }))
    expect(screen.getByLabelText('Meeting link')).toBeInTheDocument()
    expect(screen.queryByLabelText('Address')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Hybrid' }))
    expect(screen.getByLabelText('Address')).toBeInTheDocument()
    expect(screen.getByLabelText('Meeting link')).toBeInTheDocument()
  })

  it('sends date-times as local values without an offset', async () => {
    const fetchMock = routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'POST /api/events': reply(201, testEvent()),
    })
    renderNew()

    await userEvent.type(screen.getByLabelText('Title'), 'Party')
    fireEvent.change(screen.getByLabelText('Starts'), { target: { value: '2030-12-05T19:00' } })
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))

    await currentLocation()
    expect(writes(fetchMock)[0][2]).toMatchObject({ startsAt: '2030-12-05T19:00:00', endsAt: null })
  })

  it('saves, then lists every publish problem next to its field', async () => {
    const fetchMock = routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'POST /api/events': reply(201, testEvent({ title: 'Party' })),
      'POST /api/events/12/publish': validationProblem({
        CategoryId: ['Choose a category.'],
        StartsAt: ['Choose when the event starts.'],
      }),
      'PUT /api/events/12': reply(200, testEvent({ title: 'Party' })),
    })
    renderNew()

    await userEvent.type(screen.getByLabelText('Title'), 'Party')
    await userEvent.click(screen.getByRole('button', { name: 'Publish' }))

    expect(await screen.findByText('Fix these to publish:')).toBeInTheDocument()
    expect(screen.getAllByText('Choose a category.').some((el) => el.id === 'categoryId-help')).toBe(true)
    expect(screen.getAllByText('Choose when the event starts.').some((el) => el.id === 'startsAt-help')).toBe(true)

    // The draft now exists, so saving again replaces it rather than creating another.
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await currentLocation()
    expect(writes(fetchMock).map(([url, method]) => `${method} ${url}`)).toEqual([
      'POST /api/events',
      'POST /api/events/12/publish',
      'PUT /api/events/12',
    ])
  })

  it('publishes a complete event and goes to its overview', async () => {
    routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'GET /api/events/12': reply(200, testEvent()),
      'PUT /api/events/12': reply(200, testEvent()),
      'POST /api/events/12/publish': reply(200, testEvent({ status: 'Published' })),
    })
    renderEdit()

    await userEvent.click(await screen.findByRole('button', { name: 'Publish' }))

    expect(await currentLocation()).toHaveAttribute('data-notice', 'Event published.')
  })

  it('asks whether to notify guests when a published event’s time changes', async () => {
    const published = testEvent({ status: 'Published' })
    const fetchMock = routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'GET /api/events/12': reply(200, published),
      'PUT /api/events/12': reply(200, published),
    })
    renderEdit()

    fireEvent.change(await screen.findByLabelText('Starts'), { target: { value: '2030-12-05T20:00' } })
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    const dialog = await screen.findByRole('dialog', { name: 'Notify guests about this change?' })
    await userEvent.type(within(dialog).getByLabelText('Message (optional)'), 'Now at 8pm')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save and notify' }))

    expect(await currentLocation()).toHaveAttribute('data-notice', 'Changes saved.')
    expect(writes(fetchMock)[0][2]).toMatchObject({ startsAt: '2030-12-05T20:00:00', notifyGuests: true, changeMessage: 'Now at 8pm' })
  })

  it('saves a non-material change to a published event without asking', async () => {
    const published = testEvent({ status: 'Published' })
    const fetchMock = routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'GET /api/events/12': reply(200, published),
      'PUT /api/events/12': reply(200, published),
    })
    renderEdit()

    await userEvent.type(await screen.findByLabelText('Title'), '!')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await currentLocation()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    const body = writes(fetchMock)[0][2] as Record<string, unknown>
    expect(body.title).toBe("Maya's 30th Birthday!")
    expect(body).not.toHaveProperty('notifyGuests')
  })

  it('shows field errors when an edit is rejected', async () => {
    routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'GET /api/events/12': reply(200, testEvent()),
      'PUT /api/events/12': validationProblem({ VirtualUrl: ['Enter a full meeting link starting with https://.'] }),
    })
    renderEdit()

    await userEvent.click(await screen.findByRole('button', { name: 'Hybrid' }))
    await userEvent.type(screen.getByLabelText('Meeting link'), 'zoom')
    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))

    expect(await screen.findByText('Enter a full meeting link starting with https://.')).toHaveAttribute('id', 'virtualUrl-help')
  })

  it('says a cancelled event can’t be edited', async () => {
    routeFetch({
      'GET /api/categories': reply(200, testCategories),
      'GET /api/events/12': reply(200, testEvent({ status: 'Cancelled' })),
    })
    renderEdit()

    expect(await screen.findByText("This event is cancelled and can't be edited.")).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
  })

  it('says when the event isn’t found', async () => {
    routeFetch({ 'GET /api/categories': reply(200, testCategories) })
    renderEdit()

    expect(await screen.findByText('This event doesn’t exist or isn’t yours.')).toBeInTheDocument()
  })
})
