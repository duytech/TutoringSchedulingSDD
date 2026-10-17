import { vi } from 'vitest'

// A fake `fetch` for component tests. Answers by method and URL, and keeps every request it saw.

export interface FakeAnswer {
  status: number
  body?: unknown
}

export interface SeenRequest {
  method: string
  url: string
  /** The parsed JSON body, or undefined for a request without one. */
  body: unknown
}

/** `"GET /api/rooms"` → its answer. A function answers each request on its own (for a body that changes). */
export type FakeRoutes = Record<string, FakeAnswer | ((request: SeenRequest) => FakeAnswer)>

/**
 * Stubs the global `fetch` with `routes`. A request with no route rejects, naming its method and URL, so the test
 * fails. Undo it with `vi.unstubAllGlobals()`.
 */
export function fakeFetch(routes: FakeRoutes): SeenRequest[] {
  const seen: SeenRequest[] = []
  vi.stubGlobal('fetch', (url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    const request: SeenRequest = {
      method,
      url,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    }
    seen.push(request)
    const key = `${method} ${url}`
    if (!(key in routes)) {
      return Promise.reject(new Error(`No fake answer for ${method} ${url}`))
    }
    const route = routes[key]
    const answer = typeof route === 'function' ? route(request) : route
    return Promise.resolve(
      new Response(answer.body === undefined ? null : JSON.stringify(answer.body), {
        status: answer.status,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
  })
  return seen
}

/** The last request with this method and URL. Throws when there was none, so the test fails with a clear message. */
export function lastRequest(seen: SeenRequest[], method: string, url: string): SeenRequest {
  const request = seen.findLast((candidate) => candidate.method === method && candidate.url === url)
  if (!request) {
    throw new Error(`No ${method} ${url} was sent. Sent: ${seen.map((sent) => `${sent.method} ${sent.url}`).join(', ')}`)
  }
  return request
}
