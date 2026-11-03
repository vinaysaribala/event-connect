import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from 'primereact/button'
import { InputText } from 'primereact/inputtext'
import { Message } from 'primereact/message'
import { Password } from 'primereact/password'
import { ApiError, apiFetch, errorMessage } from '../api'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'
import { FormPage } from '../components/FormPage'

// Shown inline under the field; PrimeReact's strength popup covers the submit button.
function passwordHint(password: string): string {
  if (!password) return 'At least 10 characters. A longer passphrase is stronger.'
  if (password.length < 10) return `Strength: too short (${password.length} of 10 characters).`
  const varied = /[A-Za-z]/.test(password) && /\d/.test(password) && /[^A-Za-z\d]/.test(password)
  return password.length >= 14 && varied ? 'Strength: strong.' : 'Strength: good. Longer, with numbers and symbols, is stronger.'
}

export function RegisterPage() {
  const { refresh } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<ApiError | string | null>(null)
  const [registered, setRegistered] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      // Registering also signs the user in.
      await apiFetch('/api/auth/register', { method: 'POST', body: { email, password, displayName }, handleUnauthorized: false })
      await refresh()
      setRegistered(true)
    } catch (e) {
      setError(e instanceof ApiError && (e.status === 400 || e.status === 409) ? e : errorMessage(e))
    } finally {
      setSubmitting(false)
    }
  }

  if (registered) {
    return (
      <FormPage title="Check your email">
        <Message severity="success" style={{ width: '100%', marginBottom: '1rem' }} text="Check your email to verify your account." />
        <p>We sent a link to {email}. It expires in 24 hours. You can start drafting events in the meantime.</p>
        <Button label="Go to dashboard" icon="pi pi-arrow-right" iconPos="right" onClick={() => navigate('/dashboard')} />
      </FormPage>
    )
  }

  const apiError = error instanceof ApiError ? error : null
  const emailError = apiError?.status === 409 ? apiError.problem?.title : apiError?.fieldError('email')
  const nameError = apiError?.fieldError('displayName')
  const passwordError = apiError?.fieldError('password')

  return (
    <FormPage title="Create your account" subTitle="Plan events, send invitations and track RSVPs.">
      <form onSubmit={handleSubmit} noValidate>
        {typeof error === 'string' && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}
        <FormField id="email" label="Email" error={emailError}>
          <InputText
            id="email"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            invalid={!!emailError}
            aria-describedby={emailError ? 'email-help' : undefined}
          />
        </FormField>
        <FormField id="displayName" label="Display name" error={nameError} hint="Shown to your guests, e.g. Maria Lopez.">
          <InputText
            id="displayName"
            autoComplete="name"
            required
            maxLength={100}
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            invalid={!!nameError}
            aria-describedby="displayName-help"
          />
        </FormField>
        <FormField id="password" label="Password" error={passwordError} hint={passwordHint(password)}>
          <Password
            inputId="password"
            autoComplete="new-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            feedback={false}
            toggleMask
            invalid={!!passwordError}
            aria-describedby="password-help"
            style={{ width: '100%' }}
            inputStyle={{ width: '100%' }}
          />
        </FormField>
        <Button type="submit" label="Create account" loading={submitting} style={{ width: '100%' }} />
        <p style={{ marginBottom: 0, textAlign: 'center' }}>
          Already have an account? <Link to="/login">Sign in</Link>
        </p>
      </form>
    </FormPage>
  )
}
