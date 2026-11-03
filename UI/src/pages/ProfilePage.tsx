import { useMemo, useState, type FormEvent } from 'react'
import { Button } from 'primereact/button'
import { Dropdown } from 'primereact/dropdown'
import { InputText } from 'primereact/inputtext'
import { Message } from 'primereact/message'
import { Tag } from 'primereact/tag'
import { ApiError, apiFetch, errorMessage } from '../api'
import type { CurrentUser } from '../auth/AuthContext'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'
import { FormPage } from '../components/FormPage'

type Notice = { severity: 'success' | 'error'; text: string }

/** IANA time zones known to the browser, plus UTC and the user's current zone in case either is missing. */
function timeZoneOptions(current: string): string[] {
  const zones = new Set(Intl.supportedValuesOf('timeZone'))
  zones.add('UTC')
  zones.add(current)
  return [...zones].sort()
}

export function ProfilePage() {
  const { user, updateUser } = useAuth()
  // RequireAuth guarantees a user here.
  const profile = user!
  const [displayName, setDisplayName] = useState(profile.displayName)
  const [timeZone, setTimeZone] = useState(profile.timeZone)
  const zones = useMemo(() => timeZoneOptions(profile.timeZone), [profile.timeZone])
  const [saving, setSaving] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<{ displayName?: string; timeZone?: string }>({})
  const [saveNotice, setSaveNotice] = useState<Notice | null>(null)
  const [resending, setResending] = useState(false)
  const [resendNotice, setResendNotice] = useState<Notice | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setFieldErrors({})
    setSaveNotice(null)
    try {
      const updated = await apiFetch<CurrentUser>('/api/me', { method: 'PUT', body: { displayName, timeZone } })
      updateUser(updated)
      setDisplayName(updated.displayName)
      setSaveNotice({ severity: 'success', text: 'Profile saved.' })
    } catch (e) {
      if (e instanceof ApiError && e.status === 400) {
        setFieldErrors({ displayName: e.fieldError('displayName'), timeZone: e.fieldError('timeZone') })
      } else {
        setSaveNotice({ severity: 'error', text: errorMessage(e) })
      }
    } finally {
      setSaving(false)
    }
  }

  async function resend() {
    setResending(true)
    setResendNotice(null)
    try {
      await apiFetch('/api/auth/resend-verification', { method: 'POST' })
      setResendNotice({ severity: 'success', text: `We've sent a new link to ${profile.email}.` })
    } catch (e) {
      setResendNotice({ severity: 'error', text: errorMessage(e) })
    } finally {
      setResending(false)
    }
  }

  return (
    <FormPage title="Your profile">
      <form onSubmit={handleSubmit} noValidate>
        <FormField id="email" label="Email">
          <InputText id="email" value={profile.email} readOnly disabled />
        </FormField>
        <div style={{ marginBottom: '1.1rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
            {profile.emailVerified ? (
              <Tag severity="success" icon="pi pi-check" value="Email verified" />
            ) : (
              <>
                <Tag severity="warning" icon="pi pi-exclamation-triangle" value="Email not verified" />
                <Button type="button" label="Resend" icon="pi pi-envelope" size="small" outlined loading={resending} onClick={() => void resend()} />
              </>
            )}
          </div>
          {resendNotice && <Message severity={resendNotice.severity} text={resendNotice.text} style={{ width: '100%', marginTop: '0.75rem' }} />}
        </div>
        <FormField id="displayName" label="Display name" error={fieldErrors.displayName}>
          <InputText
            id="displayName"
            autoComplete="name"
            required
            maxLength={100}
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            invalid={!!fieldErrors.displayName}
            aria-describedby={fieldErrors.displayName ? 'displayName-help' : undefined}
          />
        </FormField>
        <FormField id="timeZone" label="Default time zone" error={fieldErrors.timeZone} hint="Used to prefill new events.">
          <Dropdown
            inputId="timeZone"
            value={timeZone}
            options={zones}
            onChange={(e) => setTimeZone(e.value as string)}
            filter
            filterPlaceholder="Search time zones"
            virtualScrollerOptions={{ itemSize: 38 }}
            invalid={!!fieldErrors.timeZone}
            aria-describedby="timeZone-help"
            style={{ width: '100%' }}
          />
        </FormField>
        {saveNotice && <Message severity={saveNotice.severity} text={saveNotice.text} style={{ width: '100%', marginBottom: '1rem' }} />}
        <Button type="submit" label="Save profile" icon="pi pi-check" loading={saving} />
      </form>
    </FormPage>
  )
}
