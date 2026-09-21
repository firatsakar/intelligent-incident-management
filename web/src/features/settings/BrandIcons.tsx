import type { SVGProps } from 'react'

/**
 * Vendor marks, inline.
 *
 * lucide carries no logos, and a whole icon package bought for one screen is a dependency the
 * project pays for forever — so the four vendors that need a mark get one here.
 *
 * None of them carries its brand hue. On this console colour is a verdict: red means the system
 * committed, amber means it noticed and declined. Four saturated logos in a grid would be the
 * loudest thing on the page while meaning nothing at all, and Slack's green and PagerDuty's green
 * would both read as "healthy". The silhouettes are distinct enough to carry recognition on their
 * own, so the marks draw in `currentColor` and the one colour decision left is true: full ink for
 * a destination you can connect, dimmed for one you cannot yet.
 *
 * These are simplified single-colour marks for identification, not the official brand assets.
 */

type MarkProps = SVGProps<SVGSVGElement>

/** Three cascading chevrons — the Atlassian family silhouette. */
export function JiraMark(props: MarkProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden focusable="false" {...props}>
      <path d="M11.571 11.513H0a5.218 5.218 0 0 0 5.232 5.215h2.13v2.057A5.215 5.215 0 0 0 12.575 24V12.518a1.005 1.005 0 0 0-1.004-1.005Zm5.723-5.756H5.736a5.215 5.215 0 0 0 5.215 5.214h2.129v2.058a5.218 5.218 0 0 0 5.215 5.214V6.758a1.001 1.001 0 0 0-1.001-1.001ZM23.013 0H11.455a5.215 5.215 0 0 0 5.215 5.215h2.129v2.057A5.215 5.215 0 0 0 24 12.483V1.005A1.005 1.005 0 0 0 23.013 0Z" />
    </svg>
  )
}

/**
 * Four hooks around a square hole. The first attempt built it from eight rectangles at exact 90°
 * rotations, which is symmetric but wrong: the bars have to be thick enough relative to their caps
 * that the mark reads as a hash. At 2.7:1 it read as a flower. This is the real proportion.
 */
export function SlackMark(props: MarkProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden focusable="false" {...props}>
      <path d="M5.042 15.165a2.528 2.528 0 0 1-2.52 2.523A2.528 2.528 0 0 1 0 15.165a2.527 2.527 0 0 1 2.522-2.52h2.52v2.52Zm1.271 0a2.527 2.527 0 0 1 2.521-2.52 2.527 2.527 0 0 1 2.521 2.52v6.313A2.528 2.528 0 0 1 8.834 24a2.528 2.528 0 0 1-2.521-2.522v-6.313ZM8.834 5.042a2.528 2.528 0 0 1-2.521-2.52A2.528 2.528 0 0 1 8.834 0a2.528 2.528 0 0 1 2.521 2.522v2.52H8.834Zm0 1.271a2.528 2.528 0 0 1 2.521 2.521 2.528 2.528 0 0 1-2.521 2.521H2.522A2.528 2.528 0 0 1 0 8.834a2.528 2.528 0 0 1 2.522-2.521h6.312Zm10.122 2.521a2.528 2.528 0 0 1 2.522-2.521A2.528 2.528 0 0 1 24 8.834a2.528 2.528 0 0 1-2.522 2.521h-2.522V8.834Zm-1.268 0a2.528 2.528 0 0 1-2.523 2.521 2.527 2.527 0 0 1-2.52-2.521V2.522A2.527 2.527 0 0 1 15.165 0a2.528 2.528 0 0 1 2.523 2.522v6.312Zm-2.523 10.122a2.528 2.528 0 0 1 2.523 2.522A2.528 2.528 0 0 1 15.165 24a2.527 2.527 0 0 1-2.52-2.522v-2.522h2.52Zm0-1.268a2.527 2.527 0 0 1-2.52-2.523 2.526 2.526 0 0 1 2.52-2.52h6.313A2.527 2.527 0 0 1 24 15.165a2.528 2.528 0 0 1-2.522 2.523h-6.313Z" />
    </svg>
  )
}

/** The T tile with the figure beside it. */
export function TeamsMark(props: MarkProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden focusable="false" {...props}>
      <path
        fillRule="evenodd"
        clipRule="evenodd"
        d="M3 8.4A1.4 1.4 0 0 1 4.4 7h9.2A1.4 1.4 0 0 1 15 8.4v7.2A2.4 2.4 0 0 1 12.6 18H5.4A2.4 2.4 0 0 1 3 15.6V8.4Zm2.6 1h6.8v1.9H10v5.3H8v-5.3H5.6V9.4Z"
      />
      <circle cx="18.4" cy="6.2" r="2.9" />
      <path d="M16.2 10.1h4.4A2.4 2.4 0 0 1 23 12.5v3.1a3.6 3.6 0 0 1-3.6 3.6 3.6 3.6 0 0 1-2.4-.9c.2-.5.3-1 .3-1.5v-6.7Z" />
    </svg>
  )
}

/** The squared P. */
export function PagerDutyMark(props: MarkProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden focusable="false" {...props}>
      <path
        fillRule="evenodd"
        clipRule="evenodd"
        d="M5 2.4h6.4c4.5 0 7.4 2.7 7.4 6.9 0 4.2-2.9 6.9-7.5 6.9H9.4v4.8H5V2.4Zm4.4 3.9v5.9h2.1c2.3 0 3.7-1.1 3.7-3s-1.4-2.9-3.7-2.9H9.4Z"
      />
    </svg>
  )
}

/** The face. */
export function DiscordMark(props: MarkProps) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden focusable="false" {...props}>
      <path
        fillRule="evenodd"
        clipRule="evenodd"
        d="M19.5 5.6a15.9 15.9 0 0 0-4-1.2l-.3.5a14.4 14.4 0 0 1 3.6 1.3 12.5 12.5 0 0 0-13.6 0 14.4 14.4 0 0 1 3.6-1.3l-.3-.5a15.9 15.9 0 0 0-4 1.2C1.7 9.8 1 13.9 1.3 18a16.3 16.3 0 0 0 4.9 2.5l1-1.6a10.6 10.6 0 0 1-1.9-.9l.5-.4a11.7 11.7 0 0 0 10.4 0l.5.4c-.6.4-1.2.7-1.9.9l1 1.6a16.3 16.3 0 0 0 4.9-2.5c.4-4.6-.6-8.7-3.2-12.4ZM8.7 15.6c-1 0-1.8-.9-1.8-2.1s.8-2.1 1.8-2.1 1.8 1 1.8 2.1-.8 2.1-1.8 2.1Zm6.6 0c-1 0-1.8-.9-1.8-2.1s.8-2.1 1.8-2.1 1.8 1 1.8 2.1-.8 2.1-1.8 2.1Z"
      />
    </svg>
  )
}
