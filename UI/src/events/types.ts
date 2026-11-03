// Event shapes from docs/requirements/02-events.md. Local date-times (start, end, deadline) are in the event's
// time zone and have no offset, e.g. "2026-12-05T19:00:00"; the other timestamps are UTC.

export type EventStatus = 'Draft' | 'Published' | 'Cancelled' | 'Archived'
export type EventFormat = 'Physical' | 'Virtual' | 'Hybrid'
export type EventVisibility = 'Private' | 'Public'

export interface EventDto {
  id: number
  status: EventStatus
  title: string
  description: string | null
  categoryId: number | null
  format: EventFormat | null
  startsAt: string | null
  endsAt: string | null
  timeZone: string | null
  venueName: string | null
  address: string | null
  virtualUrl: string | null
  rsvpDeadline: string | null
  capacity: number | null
  allowPlusOnes: boolean
  defaultMaxPlusOnes: number
  visibility: EventVisibility
  publishedAt: string | null
  cancelledAt: string | null
  archivedAt: string | null
  createdAt: string
  updatedAt: string
}

/** The editable fields: the body of POST /api/events and PUT /api/events/{id}. */
export type EventInput = Pick<
  EventDto,
  | 'title'
  | 'description'
  | 'categoryId'
  | 'format'
  | 'startsAt'
  | 'endsAt'
  | 'timeZone'
  | 'venueName'
  | 'address'
  | 'virtualUrl'
  | 'rsvpDeadline'
  | 'capacity'
  | 'allowPlusOnes'
  | 'defaultMaxPlusOnes'
  | 'visibility'
>

export interface Category {
  id: number
  name: string
  slug: string
}

export const formatLabels: Record<EventFormat, string> = {
  Physical: 'In person',
  Virtual: 'Virtual',
  Hybrid: 'Hybrid',
}

export const statusSeverity: Record<EventStatus, 'info' | 'success' | 'danger' | 'secondary'> = {
  Draft: 'info',
  Published: 'success',
  Cancelled: 'danger',
  Archived: 'secondary',
}

/** Fields whose change on a published event is a material change that can notify guests (FR-036). */
export const materialFields = ['startsAt', 'endsAt', 'timeZone', 'format', 'venueName', 'address', 'virtualUrl'] as const

export function toInput(event: EventDto): EventInput {
  return {
    title: event.title,
    description: event.description,
    categoryId: event.categoryId,
    format: event.format,
    startsAt: event.startsAt,
    endsAt: event.endsAt,
    timeZone: event.timeZone,
    venueName: event.venueName,
    address: event.address,
    virtualUrl: event.virtualUrl,
    rsvpDeadline: event.rsvpDeadline,
    capacity: event.capacity,
    allowPlusOnes: event.allowPlusOnes,
    defaultMaxPlusOnes: event.defaultMaxPlusOnes,
    visibility: event.visibility,
  }
}

/** "2026-12-05T19:00:00" → "Sat, Dec 5, 2026, 7:00 PM", read as the wall-clock time it is (no zone conversion). */
export function formatLocal(value: string | null): string {
  if (!value) return ''
  const [date, time = '00:00'] = value.split('T')
  const [year, month, day] = date.split('-').map(Number)
  const [hour, minute] = time.split(':').map(Number)
  return new Intl.DateTimeFormat('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, day, hour, minute)))
}
