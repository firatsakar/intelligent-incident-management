#!/usr/bin/env node
// Organisation isolation probe — development only.
//
// Fırat's rule (Adım 16.5): every piece of data and every setting belongs to one organisation, and
// nothing of one organisation may reach another. That is not a thing to measure once. This script
// asks the question again, against the running platform, and exits non-zero on any answer but "no".
//
// It talks to the gateway as two organisations at once: the canary (a test organisation with real
// Admin, Engineer and Viewer users, see NEXT_SESSION.md) and a second one — Acme by default. Tokens
// are minted with the development signing key, read from IdentityService's user secrets and never
// printed. It checks, in both directions:
//
//   - every list answers only with its own organisation's rows;
//   - the other organisation's ids answer 404, not 403 — confirming they exist is itself a leak;
//   - the organisation's configuration (integrations, telemetry sources) is 403 to Engineers and
//     Viewers, reads included, and 200 to Admins;
//   - a socket hears its own organisation's broadcasts and nobody else's, and a non-Admin socket
//     hears no configuration broadcasts at all.
//
// Writes are attempted in one direction only: with the second organisation's token against the
// canary's rows. If isolation were broken, the one thing that changed would be canary test data.
// The second organisation is only ever read.
//
//   node tools/dev/org-isolation-probe.mjs
//   IIM_GATEWAY=http://localhost:5100 IIM_ORG_B=<uuid> node tools/dev/org-isolation-probe.mjs

import { execFileSync } from 'node:child_process'
import { createHmac, randomUUID } from 'node:crypto'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')

// The console's own SignalR client, so the socket checks speak exactly what the browser speaks.
const signalr = createRequire(resolve(root, 'web', 'package.json'))('@microsoft/signalr')

const gateway = process.env.IIM_GATEWAY ?? 'http://localhost:5100'

const canary = {
  name: 'canary',
  org: '11111111-1111-1111-1111-111111111111',
  users: {
    Admin: 'c1a0de00-0000-4000-8000-00000000ad01',
    Engineer: 'c1a0de00-0000-4000-8000-00000000e603',
    Viewer: 'c1a0de00-0000-4000-8000-00000000ee02',
  },
}

const other = {
  name: 'acme',
  org: process.env.IIM_ORG_B ?? '33957f4b-4a66-4ab6-a168-ee9c09893bf9',
  users: {},
}

// ---- tokens --------------------------------------------------------------------------------

function secrets() {
  const output = execFileSync(
    'dotnet',
    ['user-secrets', 'list', '--project', resolve(root, 'src/Services/IdentityService/IdentityService.API')],
    { encoding: 'utf8' },
  )

  return Object.fromEntries(
    output
      .split(/\r?\n/)
      .filter((line) => line.includes(' = '))
      .map((line) => line.split(' = ').map((part) => part.trim())),
  )
}

const values = secrets()
const signingKey = values['Jwt:SigningKey']

if (!signingKey) throw new Error('Jwt:SigningKey not found in IdentityService user secrets.')

const b64 = (value) => Buffer.from(value).toString('base64url')

function mint(org, role, sub = randomUUID()) {
  const now = Math.floor(Date.now() / 1000)
  const header = b64(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = b64(
    JSON.stringify({
      sub,
      org,
      role,
      name: `probe (${role.toLowerCase()})`,
      iss: values['Jwt:Issuer'] ?? 'iim',
      aud: values['Jwt:Audience'] ?? 'iim',
      iat: now,
      nbf: now,
      exp: now + 15 * 60,
    }),
  )
  const signature = createHmac('sha256', signingKey).update(`${header}.${payload}`).digest('base64url')

  return `${header}.${payload}.${signature}`
}

const token = {
  canaryAdmin: mint(canary.org, 'Admin', canary.users.Admin),
  canaryEngineer: mint(canary.org, 'Engineer', canary.users.Engineer),
  canaryViewer: mint(canary.org, 'Viewer', canary.users.Viewer),
  // The data endpoints read the organisation from the token and never look the user up, so the
  // second organisation needs no real account to be read as.
  otherAdmin: mint(other.org, 'Admin'),
}

// ---- http ----------------------------------------------------------------------------------

async function call(method, path, auth, body) {
  const response = await fetch(gateway + path, {
    method,
    headers: {
      Cookie: `iim.access=${auth}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  const text = await response.text()
  let json = null

  try {
    json = text ? JSON.parse(text) : null
  } catch {
    // Not every answer is JSON; the status is what the checks read.
  }

  return { status: response.status, json }
}

// ---- reporting -----------------------------------------------------------------------------

const failures = []
let passed = 0

function check(label, ok, detail = '') {
  if (ok) {
    passed++
  } else {
    failures.push(`${label}${detail ? ` — ${detail}` : ''}`)
  }

  console.log(`${ok ? '  ok ' : ' FAIL'}  ${label}${!ok && detail ? ` — ${detail}` : ''}`)
}

const ids = (rows) => new Set((rows ?? []).map((row) => row.id))
const overlap = (a, b) => [...a].filter((id) => b.has(id))
const sample = (set, n = 5) => [...set].slice(0, n)

// ---- 1. lists and ids ------------------------------------------------------------------------

async function lists() {
  console.log('\nLists answer with their own organisation only; the other\'s ids answer 404')

  // A week, so the telemetry lists have rows to compare. Their defaults are narrow — the evidence
  // screen's is an hour — and a check that compares two empty lists proves nothing.
  const to = new Date()
  const from = new Date(to.getTime() - 7 * 24 * 3600 * 1000)
  const week = `from=${from.toISOString()}&to=${to.toISOString()}`

  const collections = [
    {
      name: 'incidents',
      list: '/api/incidents?pageSize=100',
      rows: (json) => json?.items,
      byId: (id) => `/api/incidents/${id}`,
    },
    // With a window the signal list answers every status, not only the weak ones it shows by default.
    { name: 'signals', list: `/api/telemetry/signals?limit=200&${week}`, rows: (json) => json?.items },
    { name: 'evidence log records', list: `/api/telemetry/evidence?${week}`, rows: (json) => json?.logRecords },
    { name: 'evidence signatures', list: `/api/telemetry/evidence?${week}`, rows: (json) => json?.signatures },
    {
      name: 'telemetry sources',
      list: '/api/telemetry-sources',
      rows: (json) => json,
      byId: (id) => `/api/telemetry-sources/${id}`,
    },
    {
      name: 'integrations',
      list: '/api/integrations',
      rows: (json) => json,
      byId: (id) => `/api/integrations/${id}`,
    },
  ]

  const found = {}

  for (const collection of collections) {
    const mine = await call('GET', collection.list, token.canaryAdmin)
    const theirs = await call('GET', collection.list, token.otherAdmin)

    check(`${collection.name}: both lists answer`, mine.status === 200 && theirs.status === 200, `${mine.status} / ${theirs.status}`)

    const a = ids(collection.rows(mine.json))
    const b = ids(collection.rows(theirs.json))
    const shared = overlap(a, b)

    check(`${collection.name}: no row in both (${a.size} / ${b.size})`, shared.length === 0, shared.slice(0, 3).join(', '))

    // Two empty lists agree about nothing; say so rather than count it as isolation.
    if (a.size === 0 || b.size === 0)
      console.log(`       (${collection.name}: ${a.size === 0 ? 'canary' : other.name} has no rows — nothing compared)`)

    found[collection.name] = { canary: a, other: b }

    if (!collection.byId) continue

    for (const id of sample(b)) {
      const read = await call('GET', collection.byId(id), token.canaryAdmin)
      check(`${collection.name}: canary reading ${other.name}'s ${id.slice(0, 8)} → 404`, read.status === 404, `got ${read.status}`)
    }

    for (const id of sample(a)) {
      const read = await call('GET', collection.byId(id), token.otherAdmin)
      check(`${collection.name}: ${other.name} reading canary's ${id.slice(0, 8)} → 404`, read.status === 404, `got ${read.status}`)
    }
  }

  // Deliveries are looked up by incident; another organisation's incident has none of mine.
  for (const id of sample(found.incidents.other, 3)) {
    const deliveries = await call('GET', `/api/notifications/incident/${id}`, token.canaryAdmin)
    check(
      `deliveries: canary asking about ${other.name}'s incident ${id.slice(0, 8)} → nothing`,
      deliveries.status === 404 || (deliveries.status === 200 && deliveries.json?.length === 0),
      `got ${deliveries.status} with ${deliveries.json?.length ?? '?'} row(s)`,
    )
  }

  return found
}

// ---- 2. writes across the boundary -----------------------------------------------------------

async function writes(found) {
  console.log(`\nWrites with ${other.name}'s token against the canary's rows answer 404`)

  const incident = sample(found.incidents.canary, 1)[0]
  const source = sample(found['telemetry sources'].canary, 1)[0]
  const integration = sample(found.integrations.canary, 1)[0]

  const attempts = [
    incident && ['PATCH', `/api/incidents/${incident}/team`, { team: 'isolation-probe' }],
    incident && ['PATCH', `/api/incidents/${incident}/status`, { newStatus: 'InProgress' }],
    source && ['PATCH', `/api/telemetry-sources/${source}/enabled`, { isEnabled: true }],
    source && ['POST', `/api/telemetry-sources/${source}/rotate-key`],
    integration && ['PATCH', `/api/integrations/${integration}/enabled`, { isEnabled: true }],
    integration && ['POST', `/api/integrations/${integration}/test`],
  ].filter(Boolean)

  for (const [method, path, body] of attempts) {
    const response = await call(method, path, token.otherAdmin, body)
    check(`${method} ${path.replace(/[0-9a-f-]{36}/, (id) => id.slice(0, 8))} → 404`, response.status === 404, `got ${response.status}`)
  }
}

// ---- 3. who may see the organisation's configuration -----------------------------------------

async function roles() {
  console.log('\nThe organisation\'s configuration is its Admins\' alone, reads included')

  const endpoints = [
    ['GET', '/api/integrations'],
    ['GET', '/api/telemetry-sources'],
    ['POST', '/api/integrations', {}],
    ['POST', '/api/telemetry-sources', {}],
  ]

  for (const [method, path, body] of endpoints) {
    for (const role of ['Engineer', 'Viewer']) {
      const response = await call(method, path, token[`canary${role}`], body)
      check(`${role} ${method} ${path} → 403`, response.status === 403, `got ${response.status}`)
    }
  }

  for (const path of ['/api/integrations', '/api/telemetry-sources']) {
    const response = await call('GET', path, token.canaryAdmin)
    check(`Admin GET ${path} → 200`, response.status === 200, `got ${response.status}`)
  }

  const incident = (await call('GET', '/api/incidents?pageSize=1', token.canaryViewer)).json?.items?.[0]

  if (incident) {
    const response = await call('PATCH', `/api/incidents/${incident.id}/team`, token.canaryViewer, { team: 'viewer' })
    check('Viewer cannot work an incident → 403', response.status === 403, `got ${response.status}`)
  }
}

// ---- 4. sockets ------------------------------------------------------------------------------

async function listen(hub, auth) {
  const heard = []
  const connection = new signalr.HubConnectionBuilder()
    .withUrl(`${gateway}${hub}`, {
      headers: { Cookie: `iim.access=${auth}` },
      transport: signalr.HttpTransportType.WebSockets,
      skipNegotiation: false,
    })
    .configureLogging(signalr.LogLevel.None)
    .build()

  for (const event of [
    'incidentCreated',
    'incidentChanged',
    'sourceChanged',
    'sourceDeleted',
    'integrationChanged',
    'integrationDeleted',
    'deliveryRecorded',
    'signalRecorded',
    'signatureChanged',
    'ingestionCompleted',
  ]) {
    connection.on(event, (payload) => heard.push({ event, id: payload?.id ?? payload }))
  }

  await connection.start()

  return { connection, heard }
}

async function sockets(found) {
  console.log('\nSockets hear their own organisation only; non-Admins hear no configuration')

  const incident = sample(found.incidents.canary, 1)[0]
  const source = sample(found['telemetry sources'].canary, 1)[0]

  if (!incident || !source) {
    check('sockets: the canary has an incident and a telemetry source to change', false)
    return
  }

  const ears = {
    canaryAdminIncidents: await listen('/hubs/incidents', token.canaryAdmin),
    canaryViewerIncidents: await listen('/hubs/incidents', token.canaryViewer),
    otherIncidents: await listen('/hubs/incidents', token.otherAdmin),
    canaryAdminSignals: await listen('/hubs/signals', token.canaryAdmin),
    canaryViewerSignals: await listen('/hubs/signals', token.canaryViewer),
    otherSignals: await listen('/hubs/signals', token.otherAdmin),
  }

  // One operational change and one configuration change, both on canary test data.
  await call('PATCH', `/api/incidents/${incident}/team`, token.canaryAdmin, { team: 'isolation-probe' })
  const current = await call('GET', `/api/telemetry-sources/${source}`, token.canaryAdmin)
  await call('PATCH', `/api/telemetry-sources/${source}/enabled`, token.canaryAdmin, { isEnabled: current.json?.isEnabled ?? true })

  await new Promise((resolve) => setTimeout(resolve, 2500))

  const heardEvent = (ear, event) => ears[ear].heard.some((item) => item.event === event)

  check('canary Admin hears the canary incident change', heardEvent('canaryAdminIncidents', 'incidentChanged'))
  check('canary Viewer hears the canary incident change', heardEvent('canaryViewerIncidents', 'incidentChanged'))
  check(`${other.name} hears no canary incident change`, !heardEvent('otherIncidents', 'incidentChanged'))
  check('canary Admin hears the source change', heardEvent('canaryAdminSignals', 'sourceChanged'))
  check('canary Viewer hears no source change (configuration)', !heardEvent('canaryViewerSignals', 'sourceChanged'))
  check(`${other.name} hears no canary source change`, !heardEvent('otherSignals', 'sourceChanged'))

  // Anything the other organisation's sockets heard at all during the window is canary traffic
  // unless that organisation happened to be busy; its ids must not be canary ids.
  const canaryIds = new Set([...found.incidents.canary, ...found['telemetry sources'].canary])
  const leaked = [...ears.otherIncidents.heard, ...ears.otherSignals.heard].filter((item) => canaryIds.has(item.id))
  check(`${other.name}'s sockets heard no canary id`, leaked.length === 0, leaked.map((item) => item.event).join(', '))

  await Promise.all(Object.values(ears).map((ear) => ear.connection.stop()))
}

// ---- run -------------------------------------------------------------------------------------

console.log(`Organisation isolation probe — ${gateway} — canary ${canary.org.slice(0, 8)} vs ${other.name} ${other.org.slice(0, 8)}`)

const found = await lists()
await writes(found)
await roles()
await sockets(found)

console.log(`\n${passed} passed, ${failures.length} failed`)

if (failures.length > 0) {
  console.log('\nFailures:')
  for (const failure of failures) console.log(`  - ${failure}`)
  process.exit(1)
}
