import { createContext } from 'react'

/** The signed-in user, as returned by GET /api/me. */
export interface CurrentUser {
  id: number
  email: string
  displayName: string
  emailVerified: boolean
  roles: string[]
  timeZone: string
}

export interface AuthContextValue {
  user: CurrentUser | null
  /** True until the first GET /api/me has answered. */
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  /** Reloads the user from GET /api/me (after register, verify, etc.). */
  refresh: () => Promise<void>
  /** Replaces the user with a fresh copy from the API (e.g. the PUT /api/me response). */
  updateUser: (user: CurrentUser) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
