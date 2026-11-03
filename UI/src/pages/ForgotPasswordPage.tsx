import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { Button } from 'primereact/button'
import { InputText } from 'primereact/inputtext'
import { Message } from 'primereact/message'
import { ApiError, apiFetch, errorMessage } from '../api'
import { FormField } from '../components/FormField'
import { FormPage } from '../components/FormPage'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [sent, setSent] = useState(false)
  const [emailError, setEmailError] = useState<string>()
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setEmailError(undefined)
    setError(null)
    try {
      await apiFetch('/api/auth/forgot-password', { method: 'POST', body: { email }, handleUnauthorized: false })
      setSent(true)
    } catch (e) {
      const fieldError = e instanceof ApiError ? e.fieldError('email') : undefined
      if (fieldError) setEmailError(fieldError)
      else setError(errorMessage(e))
    } finally {
      setSubmitting(false)
    }
  }

  if (sent) {
    return (
      <FormPage title="Check your email">
        <Message severity="info" style={{ width: '100%', marginBottom: '1rem' }} text="If that address has an account, we've sent a reset link." />
        <p style={{ marginBottom: 0 }}>
          The link works once and expires in 1 hour. <Link to="/login">Back to sign in</Link>
        </p>
      </FormPage>
    )
  }

  return (
    <FormPage title="Forgot your password?" subTitle="Enter your email and we'll send you a link to choose a new one.">
      <form onSubmit={handleSubmit} noValidate>
        {error && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}
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
        <Button type="submit" label="Send reset link" loading={submitting} style={{ width: '100%' }} />
        <p style={{ marginBottom: 0, textAlign: 'center' }}>
          <Link to="/login">Back to sign in</Link>
        </p>
      </form>
    </FormPage>
  )
}
