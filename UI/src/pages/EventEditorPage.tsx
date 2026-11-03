import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from 'primereact/button'
import { Card } from 'primereact/card'
import { Checkbox } from 'primereact/checkbox'
import { Dialog } from 'primereact/dialog'
import { Dropdown } from 'primereact/dropdown'
import { InputNumber } from 'primereact/inputnumber'
import { InputText } from 'primereact/inputtext'
import { InputTextarea } from 'primereact/inputtextarea'
import { Message } from 'primereact/message'
import { ProgressSpinner } from 'primereact/progressspinner'
import { SelectButton } from 'primereact/selectbutton'
import { ApiError, apiFetch, errorMessage } from '../api'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'
import { StatusTag } from '../events/StatusTag'
import { formatLabels, materialFields, toInput, type Category, type EventDto, type EventFormat, type EventInput } from '../events/types'
import { timeZoneOptions } from '../timeZones'

type FieldErrors = Partial<Record<keyof EventInput, string>>

const formatOptions = (Object.keys(formatLabels) as EventFormat[]).map((value) => ({ label: formatLabels[value], value }))
const visibilityOptions = [
  { label: 'Private', value: 'Private' },
  { label: 'Public', value: 'Public' },
]

function emptyInput(timeZone: string): EventInput {
  return {
    title: '',
    description: null,
    categoryId: null,
    format: null,
    startsAt: null,
    endsAt: null,
    timeZone,
    venueName: null,
    address: null,
    virtualUrl: null,
    rsvpDeadline: null,
    capacity: null,
    allowPlusOnes: false,
    defaultMaxPlusOnes: 1,
    visibility: 'Private',
  }
}

/** The API's PascalCase error keys as form field names, e.g. "CategoryId" → "categoryId". */
function toFieldErrors(error: ApiError): FieldErrors {
  const errors: FieldErrors = {}
  for (const [key, messages] of Object.entries(error.problem?.errors ?? {})) {
    errors[(key[0].toLowerCase() + key.slice(1)) as keyof EventInput] = messages[0]
  }
  return errors
}

function hasMaterialChange(before: EventDto, after: EventInput): boolean {
  return materialFields.some((field) => (before[field] ?? null) !== (after[field] ?? null))
}

/** Create or edit an event (`/events/new`, `/events/:id/edit`). */
export function EventEditorPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  // RequireAuth guarantees a user here.
  const profileTimeZone = user!.timeZone

  // The saved event; null until the first save of a new event.
  const [event, setEvent] = useState<EventDto | null>(null)
  const [form, setForm] = useState<EventInput>(() => emptyInput(profileTimeZone))
  const [categories, setCategories] = useState<Category[]>([])
  const [loading, setLoading] = useState(id !== undefined)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [publishProblems, setPublishProblems] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<'save' | 'publish' | null>(null)
  const [notifyOpen, setNotifyOpen] = useState(false)
  const [changeMessage, setChangeMessage] = useState('')
  const zones = useMemo(() => timeZoneOptions(form.timeZone), [form.timeZone])

  useEffect(() => {
    let ignore = false
    apiFetch<Category[]>('/api/categories')
      .then((list) => {
        if (!ignore) setCategories(list)
      })
      .catch(() => {})
    if (id !== undefined) {
      apiFetch<EventDto>(`/api/events/${id}`)
        .then((loaded) => {
          if (ignore) return
          setEvent(loaded)
          setForm(toInput(loaded))
        })
        .catch((e: unknown) => {
          if (!ignore) setLoadError(e instanceof ApiError && e.status === 404 ? 'This event doesn’t exist or isn’t yours.' : errorMessage(e))
        })
        .finally(() => {
          if (!ignore) setLoading(false)
        })
    }
    return () => {
      ignore = true
    }
  }, [id])

  function set<K extends keyof EventInput>(field: K, value: EventInput[K]) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  /** Saves the form (create or replace) and returns the saved event, or null after showing what went wrong. */
  async function persist(extra: { notifyGuests?: boolean; changeMessage?: string } = {}): Promise<EventDto | null> {
    setFieldErrors({})
    setPublishProblems([])
    setError(null)
    try {
      const saved = event
        ? await apiFetch<EventDto>(`/api/events/${event.id}`, { method: 'PUT', body: { ...form, ...extra } })
        : await apiFetch<EventDto>('/api/events', { method: 'POST', body: form })
      setEvent(saved)
      return saved
    } catch (e) {
      if (e instanceof ApiError && e.status === 400) setFieldErrors(toFieldErrors(e))
      else setError(errorMessage(e))
      return null
    }
  }

  async function saveDraft() {
    setBusy('save')
    const saved = await persist()
    setBusy(null)
    if (saved) navigate(`/events/${saved.id}`, { state: { notice: 'Draft saved.' } })
  }

  async function publish() {
    setBusy('publish')
    try {
      const saved = await persist()
      if (!saved) return
      await apiFetch<EventDto>(`/api/events/${saved.id}/publish`, { method: 'POST' })
      navigate(`/events/${saved.id}`, { state: { notice: 'Event published.' } })
    } catch (e) {
      if (e instanceof ApiError && e.status === 400) {
        const errors = toFieldErrors(e)
        setFieldErrors(errors)
        setPublishProblems(Object.values(errors).filter((m): m is string => !!m))
      } else {
        setError(errorMessage(e))
      }
    } finally {
      setBusy(null)
    }
  }

  async function saveChanges(notify?: { changeMessage: string }) {
    setNotifyOpen(false)
    setBusy('save')
    const saved = await persist(notify ? { notifyGuests: true, changeMessage: notify.changeMessage || undefined } : {})
    setBusy(null)
    if (saved) navigate(`/events/${saved.id}`, { state: { notice: 'Changes saved.' } })
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (event?.status === 'Published') {
      if (hasMaterialChange(event, form)) {
        setChangeMessage('')
        setNotifyOpen(true)
      } else {
        void saveChanges()
      }
    } else {
      void saveDraft()
    }
  }

  if (loading) {
    return (
      <Page>
        <ProgressSpinner style={{ width: 48, height: 48 }} />
      </Page>
    )
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

  if (event && (event.status === 'Cancelled' || event.status === 'Archived')) {
    return (
      <Page>
        <Message severity="info" text={`This event is ${event.status.toLowerCase()} and can't be edited.`} style={{ width: '100%' }} />
        <p>
          <Link to={`/events/${event.id}`}>Back to the event</Link>
        </p>
      </Page>
    )
  }

  const isPublished = event?.status === 'Published'
  const showVenue = form.format === 'Physical' || form.format === 'Hybrid'
  const showUrl = form.format === 'Virtual' || form.format === 'Hybrid'
  const errorProps = (field: keyof EventInput) => ({
    invalid: !!fieldErrors[field],
    'aria-describedby': fieldErrors[field] ? `${field}-help` : undefined,
  })

  return (
    <Page>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '1rem' }}>
        <h1 style={{ fontSize: '1.5rem', margin: 0 }}>{id === undefined ? 'New event' : 'Edit event'}</h1>
        {event && <StatusTag status={event.status} />}
      </div>

      <form onSubmit={handleSubmit} noValidate>
        <Section title="Basics">
          <FormField id="title" label="Title" error={fieldErrors.title}>
            <InputText id="title" required maxLength={200} value={form.title} onChange={(e) => set('title', e.target.value)} {...errorProps('title')} />
          </FormField>
          <FormField id="description" label="Description" error={fieldErrors.description}>
            <InputTextarea
              id="description"
              rows={4}
              maxLength={5000}
              value={form.description ?? ''}
              onChange={(e) => set('description', e.target.value || null)}
              {...errorProps('description')}
            />
          </FormField>
          <FormField id="categoryId" label="Category" error={fieldErrors.categoryId}>
            <Dropdown
              inputId="categoryId"
              value={form.categoryId}
              options={categories}
              optionLabel="name"
              optionValue="id"
              placeholder="Choose a category"
              showClear
              onChange={(e) => set('categoryId', (e.value as number | undefined) ?? null)}
              style={{ width: '100%' }}
              {...errorProps('categoryId')}
            />
          </FormField>
        </Section>

        <Section title="When">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', columnGap: '1rem' }}>
            <FormField id="startsAt" label="Starts" error={fieldErrors.startsAt}>
              <InputText id="startsAt" type="datetime-local" value={toInputValue(form.startsAt)} onChange={(e) => set('startsAt', fromInputValue(e.target.value))} {...errorProps('startsAt')} />
            </FormField>
            <FormField id="endsAt" label="Ends (optional)" error={fieldErrors.endsAt}>
              <InputText id="endsAt" type="datetime-local" value={toInputValue(form.endsAt)} onChange={(e) => set('endsAt', fromInputValue(e.target.value))} {...errorProps('endsAt')} />
            </FormField>
          </div>
          <FormField id="timeZone" label="Time zone" error={fieldErrors.timeZone}>
            <Dropdown
              inputId="timeZone"
              value={form.timeZone}
              options={zones}
              onChange={(e) => set('timeZone', e.value as string)}
              filter
              filterPlaceholder="Search time zones"
              virtualScrollerOptions={{ itemSize: 38 }}
              style={{ width: '100%' }}
              {...errorProps('timeZone')}
            />
          </FormField>
        </Section>

        <Section title="Where">
          <FormField id="format" label="Format" error={fieldErrors.format}>
            <SelectButton
              id="format"
              value={form.format}
              options={formatOptions}
              onChange={(e) => set('format', (e.value as EventFormat | null) ?? null)}
              invalid={!!fieldErrors.format}
            />
          </FormField>
          {showVenue && (
            <>
              <FormField id="venueName" label="Venue name" error={fieldErrors.venueName}>
                <InputText id="venueName" maxLength={200} value={form.venueName ?? ''} onChange={(e) => set('venueName', e.target.value || null)} {...errorProps('venueName')} />
              </FormField>
              <FormField id="address" label="Address" error={fieldErrors.address}>
                <InputText id="address" maxLength={500} value={form.address ?? ''} onChange={(e) => set('address', e.target.value || null)} {...errorProps('address')} />
              </FormField>
            </>
          )}
          {showUrl && (
            <FormField id="virtualUrl" label="Meeting link" error={fieldErrors.virtualUrl} hint="A full https:// link, e.g. a Zoom or Teams meeting.">
              <InputText
                id="virtualUrl"
                type="url"
                maxLength={2000}
                value={form.virtualUrl ?? ''}
                onChange={(e) => set('virtualUrl', e.target.value || null)}
                invalid={!!fieldErrors.virtualUrl}
                aria-describedby="virtualUrl-help"
              />
            </FormField>
          )}
        </Section>

        <Section title="RSVP settings">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', columnGap: '1rem' }}>
            <FormField id="rsvpDeadline" label="RSVP deadline (optional)" error={fieldErrors.rsvpDeadline}>
              <InputText
                id="rsvpDeadline"
                type="datetime-local"
                value={toInputValue(form.rsvpDeadline)}
                onChange={(e) => set('rsvpDeadline', fromInputValue(e.target.value))}
                {...errorProps('rsvpDeadline')}
              />
            </FormField>
            <FormField id="capacity" label="Capacity" error={fieldErrors.capacity} hint={fieldErrors.capacity ? undefined : 'Leave empty for unlimited.'}>
              <InputNumber
                inputId="capacity"
                value={form.capacity}
                min={1}
                useGrouping={false}
                placeholder="Unlimited"
                onValueChange={(e) => set('capacity', e.value ?? null)}
                invalid={!!fieldErrors.capacity}
                aria-describedby="capacity-help"
              />
            </FormField>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.1rem' }}>
            <Checkbox inputId="allowPlusOnes" checked={form.allowPlusOnes} onChange={(e) => set('allowPlusOnes', !!e.checked)} />
            <label htmlFor="allowPlusOnes">Allow plus-ones</label>
          </div>
          {form.allowPlusOnes && (
            <FormField id="defaultMaxPlusOnes" label="Plus-ones per guest" error={fieldErrors.defaultMaxPlusOnes}>
              <InputNumber
                inputId="defaultMaxPlusOnes"
                value={form.defaultMaxPlusOnes}
                min={0}
                max={10}
                showButtons
                onValueChange={(e) => set('defaultMaxPlusOnes', e.value ?? 0)}
                {...errorProps('defaultMaxPlusOnes')}
              />
            </FormField>
          )}
          <FormField
            id="visibility"
            label="Visibility"
            hint={form.visibility === 'Public' ? 'Anyone with the event’s share link can see it.' : 'Only invited guests can see it, through their own link.'}
          >
            <SelectButton
              id="visibility"
              value={form.visibility}
              options={visibilityOptions}
              allowEmpty={false}
              onChange={(e) => set('visibility', e.value as EventInput['visibility'])}
              aria-describedby="visibility-help"
            />
          </FormField>
        </Section>

        {publishProblems.length > 0 && (
          <Message
            severity="warn"
            style={{ width: '100%', marginBottom: '1rem', justifyContent: 'flex-start' }}
            content={
              <div>
                <strong>Fix these to publish:</strong>
                <ul style={{ margin: '0.5rem 0 0', paddingLeft: '1.25rem' }}>
                  {publishProblems.map((problem) => (
                    <li key={problem}>{problem}</li>
                  ))}
                </ul>
              </div>
            }
          />
        )}
        {error && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          {isPublished ? (
            <Button type="submit" label="Save changes" icon="pi pi-check" loading={busy === 'save'} disabled={busy !== null} />
          ) : (
            <>
              <Button type="submit" label="Save draft" icon="pi pi-save" outlined loading={busy === 'save'} disabled={busy !== null} />
              <Button type="button" label="Publish" icon="pi pi-send" loading={busy === 'publish'} disabled={busy !== null} onClick={() => void publish()} />
            </>
          )}
          <Button type="button" label="Cancel" text onClick={() => navigate(event ? `/events/${event.id}` : '/dashboard')} />
        </div>
      </form>

      <Dialog
        header="Notify guests about this change?"
        visible={notifyOpen}
        onHide={() => setNotifyOpen(false)}
        style={{ width: 'min(480px, 95vw)' }}
        footer={
          <div>
            <Button label="Save without notifying" text onClick={() => void saveChanges()} />
            <Button label="Save and notify" icon="pi pi-envelope" onClick={() => void saveChanges({ changeMessage })} />
          </div>
        }
      >
        <p style={{ marginTop: 0 }}>You changed the date, time or location. Guests who are coming or haven’t answered can get an email about it.</p>
        <FormField id="changeMessage" label="Message (optional)">
          <InputTextarea id="changeMessage" rows={3} maxLength={1000} value={changeMessage} onChange={(e) => setChangeMessage(e.target.value)} />
        </FormField>
      </Dialog>
    </Page>
  )
}

function Page({ children }: { children: ReactNode }) {
  return <main style={{ maxWidth: 760, margin: '0 auto', padding: '2rem 1rem' }}>{children}</main>
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card title={<h2 style={{ fontSize: '1.15rem', margin: 0 }}>{title}</h2>} style={{ marginBottom: '1rem' }}>
      {children}
    </Card>
  )
}

/** "2026-12-05T19:00:00" → "2026-12-05T19:00" for a datetime-local input. */
function toInputValue(value: string | null): string {
  return value ? value.slice(0, 16) : ''
}

/** A datetime-local input value as the API's local date-time, or null when cleared. */
function fromInputValue(value: string): string | null {
  return value ? `${value}:00` : null
}
