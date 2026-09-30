import { useEffect, useState } from 'react'
import { ApiError } from './api'

interface Loaded<TData> {
  key: string
  data: TData | null
  error: ApiError | null
}

export interface LoadState<TData> {
  /** The last data that arrived. It stays while the next key loads, so the page does not flash. */
  data: TData | null
  error: ApiError | null
  loading: boolean
}

/**
 * Loads data for a key, and loads again when the key changes. `load` must change only with the key (wrap it in
 * useCallback). A response that arrives after the key has moved on is dropped.
 */
export function useLoad<TData>(key: string, load: () => Promise<TData>): LoadState<TData> {
  const [loaded, setLoaded] = useState<Loaded<TData> | null>(null)

  useEffect(() => {
    let current = true
    load()
      .then((data) => {
        if (current) {
          setLoaded({ key, data, error: null })
        }
      })
      .catch((cause: unknown) => {
        if (current) {
          const error = cause instanceof ApiError ? cause : new ApiError(String(cause), null)
          setLoaded((previous) => ({ key, data: previous?.data ?? null, error }))
        }
      })
    return () => {
      current = false
    }
  }, [key, load])

  return { data: loaded?.data ?? null, error: loaded?.error ?? null, loading: loaded?.key !== key }
}
