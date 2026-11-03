import { Link, Outlet, useNavigate } from 'react-router'
import { Button } from 'primereact/button'
import { Menubar } from 'primereact/menubar'
import { Message } from 'primereact/message'
import type { MenuItem } from 'primereact/menuitem'
import { useAuth } from '../auth/useAuth'

export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const menuItems: MenuItem[] = [
    { label: 'Dashboard', icon: 'pi pi-home', command: () => navigate('/dashboard') },
    { label: 'Events', icon: 'pi pi-calendar' },
    { label: 'Templates', icon: 'pi pi-palette' },
  ]

  const end = user ? (
    <div style={{ display: 'flex', gap: '0.25rem' }}>
      <Button label={user.displayName} icon="pi pi-user" text onClick={() => navigate('/profile')} />
      <Button label="Sign out" icon="pi pi-sign-out" text onClick={() => void logout()} />
    </div>
  ) : (
    <div style={{ display: 'flex', gap: '0.25rem' }}>
      <Button label="Sign in" icon="pi pi-sign-in" text onClick={() => navigate('/login')} />
      <Button label="Register" icon="pi pi-user-plus" outlined onClick={() => navigate('/register')} />
    </div>
  )

  return (
    <>
      <Menubar
        model={menuItems}
        start={
          <Link to="/" style={{ fontWeight: 700, fontSize: '1.25rem', marginRight: '1.5rem', color: 'inherit', textDecoration: 'none' }}>
            EventConnect
          </Link>
        }
        end={end}
        style={{ borderRadius: 0 }}
      />
      {user && !user.emailVerified && (
        <Message
          severity="warn"
          style={{ width: '100%', justifyContent: 'center', borderRadius: 0 }}
          content={
            <span>
              Verify your email to publish events. <Link to="/profile">Resend the link</Link>
            </span>
          }
        />
      )}
      <Outlet />
    </>
  )
}
