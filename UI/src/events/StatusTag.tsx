import { Tag } from 'primereact/tag'
import { statusSeverity, type EventStatus } from './types'

export function StatusTag({ status }: { status: EventStatus }) {
  return <Tag value={status} severity={statusSeverity[status]} />
}
