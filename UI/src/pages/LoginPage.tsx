import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router'
import { Button } from 'primereact/button'
import { InputText } from 'primereact/inputtext'
import { Message } from 'primereact/message'
import { Password } from 'primereact/password'
import { errorMessage } from '../api'
import { safeReturnTo } from '../auth/returnTo'
import { useAuth } from '../auth/useAuth'
import { FormField } from '../components/FormField'
import { FormPage } from '../components/FormPage'

export function LoginPage() {
  const { user, loading, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const returnTo = safeReturnTo(searchParams.get('returnTo'))
  const notice = (location.state as { notice?: string } | null)?.notice
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setError(null)
    try {
      await login(email, password)
      navigate(returnTo, { replace: true })
    } catch (e) {
      setError(errorMessage(e))
      setSubmitting(false)
    }
  }

  if (!loading && user && !submitting) return <Navigate to={returnTo} replace />

  return (
    <FormPage title="Sign in">
      <form onSubmit={handleSubmit}>
        {notice && !error && <Message severity="success" text={notice} style={{ width: '100%', marginBottom: '1rem' }} />}
        {error && <Message severity="error" text={error} style={{ width: '100%', marginBottom: '1rem' }} />}
        <FormField id="email" label="Email">
          <InputText id="email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </FormField>
        <FormField id="password" label="Password">
          <Password
            inputId="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            feedback={false}
            toggleMask
            style={{ width: '100%' }}
            inputStyle={{ width: '100%' }}
          />
        </FormField>
        <p style={{ marginTop: '-0.5rem', textAlign: 'right' }}>
          <Link to="/forgot-password">Forgot password?</Link>
        </p>
        <Button type="submit" label="Sign in" loading={submitting} style={{ width: '100%' }} />
        <p style={{ marginBottom: 0, textAlign: 'center' }}>
          New to EventConnect? <Link to="/register">Create an account</Link>
        </p>
      </form>
    </FormPage>
  )
}
