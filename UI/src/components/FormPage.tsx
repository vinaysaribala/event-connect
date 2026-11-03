import type { ReactNode } from 'react'
import { Card } from 'primereact/card'

/** Narrow, centered card used by the sign-in and account screens. */
export function FormPage({ title, subTitle, children }: { title: string; subTitle?: string; children: ReactNode }) {
  return (
    <main style={{ maxWidth: 460, margin: '0 auto', padding: '2rem 1rem' }}>
      <Card title={<h1 style={{ fontSize: '1.5rem', margin: 0 }}>{title}</h1>} subTitle={subTitle}>
        {children}
      </Card>
    </main>
  )
}
