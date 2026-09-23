// The time window two screens share. Kept here rather than in either feature so the signal map
// and the evidence explorer cannot drift apart on what "last 2 hours" means.

// No labels here any more. A preset is a span of minutes and an id; what it is called is text,
// and text lives in the dictionary (`window.option` for the picker, `window.scope` for the same
// window read inside a sentence).
export const windowPresets = [
  { value: '30m', minutes: 30 },
  { value: '2h', minutes: 120 },
  { value: '24h', minutes: 1440 },
  { value: '7d', minutes: 10080 },
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

/**
 * The URL is the source of truth and anybody can type into it, so an unknown value is the default
 * rather than an error. Callers get a `WindowPreset` back, which is what indexes the dictionary.
 */
export function resolveWindowPreset(value: string | null): WindowPreset {
  return windowPresets.find((candidate) => candidate.value === value)?.value ?? defaultWindow
}
