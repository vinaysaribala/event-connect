/** Where to go after sign-in: the requested page if it's a path on this site, else the dashboard. */
export function safeReturnTo(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : '/dashboard'
}
