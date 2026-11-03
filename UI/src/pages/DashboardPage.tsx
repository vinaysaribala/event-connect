import { Card } from 'primereact/card'
import { useAuth } from '../auth/useAuth'

// Placeholder until the organizer dashboard (docs/requirements/08-dashboard.md) is built.
export function DashboardPage() {
  const { user } = useAuth()

  return (
    <main style={{ maxWidth: 960, margin: '0 auto', padding: '2rem 1rem' }}>
      <Card title="Dashboard" subTitle={`Signed in as ${user?.displayName}`}>
        <p style={{ margin: 0 }}>Your events will appear here.</p>
      </Card>
    </main>
  )
}
