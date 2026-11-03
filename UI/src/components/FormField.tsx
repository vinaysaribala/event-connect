import type { ReactNode } from 'react'

/**
 * Label, input and error/hint text. Give the input `id`, and `aria-describedby={`${id}-help`}` when there's
 * an error or hint.
 */
export function FormField({
  id,
  label,
  error,
  hint,
  children,
}: {
  id: string
  label: string
  error?: string
  hint?: string
  children: ReactNode
}) {
  const help = error ?? hint
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', marginBottom: '1.1rem' }}>
      <label htmlFor={id} style={{ fontWeight: 600 }}>
        {label}
      </label>
      {children}
      {help && (
        <small id={`${id}-help`} className={error ? 'p-error' : undefined} style={error ? undefined : { color: 'var(--text-color-secondary)' }}>
          {help}
        </small>
      )}
    </div>
  )
}
