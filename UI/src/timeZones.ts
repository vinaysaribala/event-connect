/** IANA time zones known to the browser, plus UTC and `current` in case either is missing. */
export function timeZoneOptions(current?: string | null): string[] {
  const zones = new Set(Intl.supportedValuesOf('timeZone'))
  zones.add('UTC')
  if (current) zones.add(current)
  return [...zones].sort()
}
