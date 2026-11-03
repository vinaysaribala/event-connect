import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { Button } from 'primereact/button'
import { Card } from 'primereact/card'
import { ConfirmDialog } from 'primereact/confirmdialog'
import { Dialog } from 'primereact/dialog'
import { InputTextarea } from 'primereact/inputtextarea'
import { Message } from 'primereact/message'
import { ProgressSpinner } from 'primereact/progressspinner'
import { ApiError, apiFetch, errorMessage } from '../api'
import { FormField } from '../components/FormField'
import { StatusTag } from '../events/StatusTag'
import { formatLabels, formatLocal, type Category, type EventDto } from '../events/types'

type Notice = { severity: 'success' | 'error' | 'warn'; text: string; items?: string[] }

/** One event: its details and the actions its status allows (`/events/:id`). */
export function EventOverviewPage() {
  const { id } = useParams()
  const location = useLocation()
  // Start afresh on every navigation, e.g. from an event to its duplicate.
  return <EventOverview key={location.key} id={id!} passedNotice={(location.state as { notice?: string } | null)?.notice} />
}

/** `passedNotice` comes from the page that navigated here, e.g. "Draft saved." */
function EventOverview({ id, passedNotice }: { id: string; passedNotice?: string }) {
  const navigate = useNavigate()
  const [event, setEvent] = useState<EventDto | null>(null)
  const [categories, setCategories] = useState<Category[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [notice, setNotice] = useState<Notice | null>(passedNotice ? { severity: 'success', text: passedNotice } : null)
  const [busy, setBusy] = useState<string | null>(null)
  const [confirm, setConfirm] = useState<'archive' | 'delete' | null>(null)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelMessage, setCancelMessage] = useState('')

  useEffect(() => {
    let ignore = false
    apiFetch<Category[]>('/api/categories')
      .then((list) => {
        if (!ignore) setCategories(list)
      })
      .catch(() => {})
    apiFetch<EventDto>(`/api/events/${id}`)
      .then((loaded) => {
        if (!ignore) setEvent(loaded)
      })
      .catch((e: unknown) => {
        if (!ignore) setLoadError(e instanceof ApiError && e.status === 404 ? 'This event doesn’t exist or isn’t yours.' : errorMessage(e))
      })
    return () => {
      ignore = true
    }
  }, [id])

  /** Runs an action, showing a failure as a notice. Returns false when it failed. */
  async function run(name: string, action: () => Promise<void>): Promise<boolean> {
    setBusy(name)
    setNotice(null)
    try {
      await action()
      return true
    } catch (e) {
      if (e instanceof ApiError && e.status === 400 && e.problem?.errors) {
        const items = Object.values(e.problem.errors).flat()
        setNotice({ severity: 'warn', text: e.problem.title ?? 'Event is not ready to publish.', items })
      } else {
        setNotice({ severity: 'error', text: errorMessage(e) })
      }
      return false
    } finally {
      setBusy(null)
    }
  }

  if (loadError) {
    return (
      <Page>
        <Message severity="error" text={loadError} style={{ width: '100%' }} />
        <p>
          <Link to="/dashboard">Back to your events</Link>
        </p>
      </Page>
    )
  }

  if (!event) {
    return (
      <Page>
        <ProgressSpinner style={{ width: 48, height: 48 }} />
      </Page>
    )
  }

  const current = event
  const category = categories.find((c) => c.id === current.categoryId)

  const publish = () =>
    run('publish', async () => {
      setEvent(await apiFetch<EventDto>(`/api/events/${current.id}/publish`, { method: 'POST' }))
      setNotice({ severity: 'success', text: 'Event published.' })
    })

  const cancelEvent = async () => {
    setCancelOpen(false)
    await run('cancel', async () => {
      setEvent(await apiFetch<EventDto>(`/api/events/${current.id}/cancel`, { method: 'POST', body: { message: cancelMessage || null } }))
      setNotice({ severity: 'success', text: 'Event cancelled.' })
    })
  }

  const archive = () =>
    run('archive', async () => {
      setEvent(await apiFetch<EventDto>(`/api/events/${current.id}/archive`, { method: 'POST' }))
      setNotice({ severity: 'success', text: 'Event archived.' })
    })

  const duplicate = () =>
    run('duplicate', async () => {
      const copy = await apiFetch<EventDto>(`/api/events/${current.id}/duplicate`, { method: 'POST' })
      navigate(`/events/${copy.id}`, { state: { notice: 'Copy created as a draft. Guests weren’t copied.' } })
    })

  const remove = () =>
    run('delete', async () => {
      await apiFetch(`/api/events/${current.id}`, { method: 'DELETE' })
      navigate('/dashboard', { state: { notice: 'Draft deleted.' } })
    })

  const editable = current.status === 'Draft' || current.status === 'Published'
  const zone = current.timeZone ? ` (${current.timeZone})` : ''

  return (
    <Page>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <h1 style={{ fontSize: '1.5rem', margin: 0 }}>{current.title}</h1>
        <StatusTag status={current.status} />
      </div>

      {notice && (
        <Message
          severity={notice.severity}
          style={{ width: '100%', marginBottom: '1rem', justifyContent: 'flex-start' }}
          content={
            <div>
              {notice.text}
              {notice.items && (
                <ul style={{ margin: '0.5rem 0 0', paddingLeft: '1.25rem' }}>
                  {notice.items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              )}
            </div>
          }
        />
      )}

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
        {editable && <Button label="Edit" icon="pi pi-pencil" outlined onClick={() => navigate(`/events/${current.id}/edit`)} />}
        {current.status === 'Draft' && <Button label="Publish" icon="pi pi-send" loading={busy === 'publish'} onClick={() => void publish()} />}
        {current.status === 'Published' && (
          <Button
            label="Cancel event"
            icon="pi pi-times"
            severity="danger"
            outlined
            loading={busy === 'cancel'}
            onClick={() => {
              setCancelMessage('')
              setCancelOpen(true)
            }}
          />
        )}
        <Button label="Duplicate" icon="pi pi-copy" text loading={busy === 'duplicate'} onClick={() => void duplicate()} />
        {current.status !== 'Archived' && <Button label="Archive" icon="pi pi-inbox" text loading={busy === 'archive'} onClick={() => setConfirm('archive')} />}
        {current.status === 'Draft' && (
          <Button label="Delete" icon="pi pi-trash" text severity="danger" loading={busy === 'delete'} onClick={() => setConfirm('delete')} />
        )}
      </div>

      <Card>
        <Detail label="When">
          {current.startsAt ? `${formatLocal(current.startsAt)}${current.endsAt ? ` – ${formatLocal(current.endsAt)}` : ''}${zone}` : 'Not set'}
        </Detail>
        <Detail label="Where">
          {current.format ? (
            <>
              <div>{formatLabels[current.format]}</div>
              {current.venueName && <div>{current.venueName}</div>}
              {current.address && <div>{current.address}</div>}
              {current.virtualUrl && (
                <a href={current.virtualUrl} target="_blank" rel="noreferrer">
                  {current.virtualUrl}
                </a>
              )}
            </>
          ) : (
            'Not set'
          )}
        </Detail>
        <Detail label="Category">{category?.name ?? (current.categoryId ? '' : 'Not set')}</Detail>
        <Detail label="RSVP deadline">{current.rsvpDeadline ? `${formatLocal(current.rsvpDeadline)}${zone}` : 'None'}</Detail>
        <Detail label="Capacity">{current.capacity ?? 'Unlimited'}</Detail>
        <Detail label="Plus-ones">
          {current.allowPlusOnes ? `Up to ${current.defaultMaxPlusOnes} per guest` : 'Not allowed'}
        </Detail>
        <Detail label="Visibility">{current.visibility}</Detail>
        {current.description && <Detail label="Description">{current.description}</Detail>}
      </Card>

      <ConfirmDialog
        visible={confirm !== null}
        onHide={() => setConfirm(null)}
        header={confirm === 'delete' ? 'Delete this draft?' : 'Archive this event?'}
        message={confirm === 'delete' ? 'The draft is deleted permanently.' : 'Archived events are read-only and hidden from your event list.'}
        icon="pi pi-exclamation-triangle"
        acceptLabel={confirm === 'delete' ? 'Delete' : 'Archive'}
        rejectLabel="Keep"
        accept={() => void (confirm === 'delete' ? remove() : archive())}
      />

      <Dialog
        header="Cancel this event?"
        visible={cancelOpen}
        onHide={() => setCancelOpen(false)}
        style={{ width: 'min(480px, 95vw)' }}
        footer={
          <div>
            <Button label="Keep event" text onClick={() => setCancelOpen(false)} />
            <Button label="Cancel event" icon="pi pi-times" severity="danger" onClick={() => void cancelEvent()} />
          </div>
        }
      >
        <p style={{ marginTop: 0 }}>RSVPs close straight away and guests are told the event is cancelled. This can’t be undone.</p>
        <FormField id="cancelMessage" label="Message to guests (optional)">
          <InputTextarea id="cancelMessage" rows={3} maxLength={1000} value={cancelMessage} onChange={(e) => setCancelMessage(e.target.value)} />
        </FormField>
      </Dialog>
    </Page>
  )
}

function Page({ children }: { children: ReactNode }) {
  return <main style={{ maxWidth: 760, margin: '0 auto', padding: '2rem 1rem' }}>{children}</main>
}

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(110px, 160px) 1fr', gap: '1rem', padding: '0.5rem 0' }}>
      <div style={{ fontWeight: 600, color: 'var(--text-color-secondary)' }}>{label}</div>
      <div style={{ whiteSpace: 'pre-wrap' }}>{children}</div>
    </div>
  )
}
