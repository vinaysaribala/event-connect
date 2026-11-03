import type { Category, EventDto } from '../events/types'

export const testCategories: Category[] = [
  { id: 1, name: 'Birthday', slug: 'birthday' },
  { id: 2, name: 'Wedding', slug: 'wedding' },
]

/** The sample event from docs/requirements/02-events.md, as a draft. */
export function testEvent(overrides: Partial<EventDto> = {}): EventDto {
  return {
    id: 12,
    status: 'Draft',
    title: "Maya's 30th Birthday",
    description: 'Dinner and drinks on the rooftop.',
    categoryId: 1,
    format: 'Physical',
    startsAt: '2030-12-05T19:00:00',
    endsAt: '2030-12-05T23:00:00',
    timeZone: 'America/New_York',
    venueName: 'Skyline Rooftop',
    address: '123 Main St, New York, NY 10001',
    virtualUrl: null,
    rsvpDeadline: '2030-11-28T23:59:00',
    capacity: 40,
    allowPlusOnes: true,
    defaultMaxPlusOnes: 1,
    visibility: 'Private',
    publishedAt: null,
    cancelledAt: null,
    archivedAt: null,
    createdAt: '2026-11-02T15:04:00Z',
    updatedAt: '2026-11-02T15:04:00Z',
    ...overrides,
  }
}
