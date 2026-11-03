import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { Button } from 'primereact/button'
import { Card } from 'primereact/card'
import { Column } from 'primereact/column'
import { DataTable } from 'primereact/datatable'
import { Message } from 'primereact/message'
import { apiFetch, errorMessage } from '../api'
import { useAuth } from '../auth/useAuth'
import { StatusTag } from '../events/StatusTag'
import { formatLocal, type EventDto } from '../events/types'

// A plain list of the organizer's events until the organizer dashboard (docs/requirements/08-dashboard.md) is built.
export function DashboardPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const notice = (location.state as { notice?: string } | null)?.notice
  const [events, setEvents] = useState<EventDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let ignore = false
    apiFetch<EventDto[]>('/api/events')
      .then((list) => {
        if (!ignore) setEvents(list)
      })
      .catch((e: unknown) => {
        if (!ignore) setError(errorMessage(e))
      })
    return () => {
      ignore = true
    }
  }, [])

  return (
    <main style={{ maxWidth: 960, margin: '0 auto', padding: '2rem 1rem' }}>
      <Card
        title={
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
            <h1 style={{ fontSize: '1.5rem', margin: 0 }}>Your events</h1>
            <Button label="New event" icon="pi pi-plus" onClick={() => navigate('/events/new')} />
          </div>
        }
        subTitle={`Signed in as ${user?.displayName}`}
      >
        {notice && <Message severity="success" text={notice} style={{ width: '100%', marginBottom: '1rem' }} />}
        {error && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}
        <DataTable value={events ?? []} loading={events === null && !error} dataKey="id" emptyMessage="No events yet. Create your first one.">
          <Column header="Event" body={(e: EventDto) => <Link to={`/events/${e.id}`}>{e.title}</Link>} />
          <Column header="Status" body={(e: EventDto) => <StatusTag status={e.status} />} />
          <Column header="Starts" body={(e: EventDto) => (e.startsAt ? `${formatLocal(e.startsAt)}${e.timeZone ? ` (${e.timeZone})` : ''}` : 'Not set')} />
        </DataTable>
      </Card>
    </main>
  )
}
