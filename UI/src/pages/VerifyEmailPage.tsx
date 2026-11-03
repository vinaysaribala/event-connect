import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { Button } from 'primereact/button'
import { Message } from 'primereact/message'
import { ProgressSpinner } from 'primereact/progressspinner'
import { apiFetch, errorMessage } from '../api'
import { useAuth } from '../auth/useAuth'
import { FormPage } from '../components/FormPage'

type Status = 'verifying' | 'verified' | 'failed'

export function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const { user, refresh } = useAuth()
  const navigate = useNavigate()
  const [status, setStatus] = useState<Status>(token ? 'verifying' : 'failed')
  const [resendMessage, setResendMessage] = useState<{ severity: 'success' | 'error'; text: string } | null>(null)
  const [resending, setResending] = useState(false)
  // The link is single-use, so call verify once even when React runs effects twice in development.
  const started = useRef(false)

  useEffect(() => {
    if (!token || started.current) return
    started.current = true
    apiFetch('/api/auth/verify-email', { method: 'POST', body: { token }, handleUnauthorized: false })
      .then(async () => {
        await refresh()
        setStatus('verified')
      })
      .catch(() => setStatus('failed'))
  }, [token, refresh])

  async function resend() {
    // Sending a new link needs a session; ask the user to sign in first, then they can resend from their profile.
    if (!user) {
      navigate('/login?returnTo=%2Fprofile')
      return
    }
    setResending(true)
    try {
      await apiFetch('/api/auth/resend-verification', { method: 'POST' })
      setResendMessage({ severity: 'success', text: `We've sent a new link to ${user.email}.` })
    } catch (e) {
      setResendMessage({ severity: 'error', text: errorMessage(e) })
    } finally {
      setResending(false)
    }
  }

  if (status === 'verifying') {
    return (
      <FormPage title="Verifying your email">
        <div style={{ display: 'flex', justifyContent: 'center' }}>
          <ProgressSpinner aria-label="Verifying" />
        </div>
      </FormPage>
    )
  }

  if (status === 'verified') {
    return (
      <FormPage title="Email verified">
        <Message severity="success" style={{ width: '100%', marginBottom: '1rem' }} text="Your email is verified. You can now publish events and send invitations." />
        <Button label="Go to dashboard" icon="pi pi-arrow-right" iconPos="right" onClick={() => navigate('/dashboard')} />
      </FormPage>
    )
  }

  return (
    <FormPage title="We couldn't verify your email">
      <Message severity="error" style={{ width: '100%', marginBottom: '1rem' }} text="This link is invalid, has already been used, or has expired." />
      {resendMessage && <Message severity={resendMessage.severity} style={{ width: '100%', marginBottom: '1rem' }} text={resendMessage.text} />}
      <Button label="Send a new link" icon="pi pi-envelope" loading={resending} onClick={() => void resend()} />
    </FormPage>
  )
}
