// Shared fetch helper for the API. The session lives in an HTTP-only cookie, so no tokens are handled here.

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | undefined

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.title ?? `Request failed with status ${status}`)
    this.status = status
    this.problem = problem
  }

  /** First validation message for a field; the API's keys are PascalCase, so match without case. */
  fieldError(field: string): string | undefined {
    const errors = this.problem?.errors ?? {}
    const key = Object.keys(errors).find((k) => k.toLowerCase() === field.toLowerCase())
    return key ? errors[key][0] : undefined
  }
}

interface ApiOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** Set false where a 401 is an expected answer (sign-in, checking the session) rather than "session ended". */
  handleUnauthorized?: boolean
}

let unauthorizedHandler: (() => void) | undefined

/** Registers what happens when any request returns 401. Returns an unsubscribe function. */
export function onUnauthorized(handler: () => void): () => void {
  unauthorizedHandler = handler
  return () => {
    if (unauthorizedHandler === handler) unauthorizedHandler = undefined
  }
}

export async function apiFetch<T = void>(path: string, options: ApiOptions = {}): Promise<T> {
  const { method = 'GET', body, handleUnauthorized = true } = options
  const response = await fetch(path, {
    method,
    credentials: 'same-origin',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  const text = await response.text()
  const data: unknown = text ? JSON.parse(text) : undefined

  if (!response.ok) {
    if (response.status === 401 && handleUnauthorized) unauthorizedHandler?.()
    throw new ApiError(response.status, data as ProblemDetails | undefined)
  }
  return data as T
}

/** A sentence to show the user for a failed request. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) return 'Too many attempts. Wait a minute and try again.'
    if (error.status < 500 && error.problem?.title) return error.problem.title
  }
  return 'Something went wrong. Please try again.'
}
