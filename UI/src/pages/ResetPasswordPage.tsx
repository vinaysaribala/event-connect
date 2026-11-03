import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { Button } from 'primereact/button'
import { Message } from 'primereact/message'
import { Password } from 'primereact/password'
import { ApiError, apiFetch, errorMessage } from '../api'
import { FormField } from '../components/FormField'
import { FormPage } from '../components/FormPage'

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const navigate = useNavigate()
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [passwordError, setPasswordError] = useState<string>()
  const [confirmError, setConfirmError] = useState<string>()
  const [linkInvalid, setLinkInvalid] = useState(!token)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setPasswordError(undefined)
    setConfirmError(undefined)
    setError(null)
    if (newPassword !== confirmPassword) {
      setConfirmError("The passwords don't match.")
      return
    }

    setSubmitting(true)
    try {
      await apiFetch('/api/auth/reset-password', { method: 'POST', body: { token, newPassword }, handleUnauthorized: false })
      navigate('/login', { replace: true, state: { notice: 'Your password has been changed. Sign in with your new password.' } })
    } catch (e) {
      if (e instanceof ApiError && e.fieldError('token')) setLinkInvalid(true)
      else if (e instanceof ApiError && e.fieldError('newPassword')) setPasswordError(e.fieldError('newPassword'))
      else setError(errorMessage(e))
      setSubmitting(false)
    }
  }

  if (linkInvalid) {
    return (
      <FormPage title="Reset link not valid">
        <Message severity="error" style={{ width: '100%', marginBottom: '1rem' }} text="This link is invalid, has already been used, or has expired." />
        <p style={{ marginBottom: 0 }}>
          <Link to="/forgot-password">Request a new reset link</Link>
        </p>
      </FormPage>
    )
  }

  return (
    <FormPage title="Choose a new password">
      <form onSubmit={handleSubmit} noValidate>
        {error && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}
        <FormField id="newPassword" label="New password" error={passwordError} hint="At least 10 characters.">
          <Password
            inputId="newPassword"
            autoComplete="new-password"
            required
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            feedback={false}
            toggleMask
            invalid={!!passwordError}
            aria-describedby="newPassword-help"
            style={{ width: '100%' }}
            inputStyle={{ width: '100%' }}
          />
        </FormField>
        <FormField id="confirmPassword" label="Confirm new password" error={confirmError}>
          <Password
            inputId="confirmPassword"
            autoComplete="new-password"
            required
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            feedback={false}
            toggleMask
            invalid={!!confirmError}
            aria-describedby={confirmError ? 'confirmPassword-help' : undefined}
            style={{ width: '100%' }}
            inputStyle={{ width: '100%' }}
          />
        </FormField>
        <Button type="submit" label="Change password" loading={submitting} style={{ width: '100%' }} />
      </form>
    </FormPage>
  )
}
