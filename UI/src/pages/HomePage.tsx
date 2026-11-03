import { useNavigate } from 'react-router'
import { Button } from 'primereact/button'
import { Card } from 'primereact/card'
import { useAuth } from '../auth/useAuth'

export function HomePage() {
  const { user } = useAuth()
  const navigate = useNavigate()

  return (
    <main style={{ maxWidth: 960, margin: '0 auto', padding: '2rem 1rem' }}>
      <Card title="Welcome to EventConnect" subTitle="Digital invitations, event management and RSVPs">
        <p style={{ marginTop: 0 }}>Create an event, design its invitation, invite your guests and track who's coming.</p>
        {user ? (
          <Button label="Go to dashboard" icon="pi pi-home" onClick={() => navigate('/dashboard')} />
        ) : (
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Button label="Create an account" icon="pi pi-user-plus" onClick={() => navigate('/register')} />
            <Button label="Sign in" icon="pi pi-sign-in" outlined onClick={() => navigate('/login')} />
          </div>
        )}
      </Card>
    </main>
  )
}
