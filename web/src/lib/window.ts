// The time window two screens share. Kept here rather than in either feature so the signal map
// and the evidence explorer cannot drift apart on what "last 2 hours" means.

export const windowPresets = [
  { value: '30m', label: 'Last 30 minutes', minutes: 30 },
  { value: '2h', label: 'Last 2 hours', minutes: 120 },
  { value: '24h', label: 'Last 24 hours', minutes: 1440 },
  { value: '7d', label: 'Last 7 days', minutes: 10080 },
] as const

export type WindowPreset = (typeof windowPresets)[number]['value']

export const defaultWindow: WindowPreset = '2h'

export function resolveWindow(preset: string | null): { from: string; to: string } {
  const match =
    windowPresets.find((candidate) => candidate.value === preset) ??
    windowPresets.find((candidate) => candidate.value === defaultWindow)!

  const to = new Date()
  const from = new Date(to.getTime() - match.minutes * 60_000)

  return { from: from.toISOString(), to: to.toISOString() }
}

export function windowLabel(preset: string | null): string {
  return (
    windowPresets.find((candidate) => candidate.value === preset)?.label ??
    windowPresets.find((candidate) => candidate.value === defaultWindow)!.label
  )
}
