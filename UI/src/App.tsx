import { Button } from 'primereact/button'
import { Card } from 'primereact/card'
import { Menubar } from 'primereact/menubar'
import type { MenuItem } from 'primereact/menuitem'

const menuItems: MenuItem[] = [
  { label: 'Dashboard', icon: 'pi pi-home' },
  { label: 'Events', icon: 'pi pi-calendar' },
  { label: 'Templates', icon: 'pi pi-palette' },
]

function App() {
  return (
    <>
      <Menubar
        model={menuItems}
        start={<span style={{ fontWeight: 700, fontSize: '1.25rem', marginRight: '1.5rem' }}>EventConnect</span>}
        end={<Button label="Sign in" icon="pi pi-sign-in" text />}
        style={{ borderRadius: 0 }}
      />
      <main style={{ maxWidth: 960, margin: '0 auto', padding: '2rem 1rem' }}>
        <Card title="Welcome to EventConnect" subTitle="Digital invitations, event management and RSVPs">
          <p style={{ marginTop: 0 }}>Create an event, design its invitation, invite your guests and track who's coming.</p>
          <Button label="Create event" icon="pi pi-plus" />
        </Card>
      </main>
    </>
  )
}

export default App
