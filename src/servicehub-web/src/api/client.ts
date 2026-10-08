const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080').replace(/\/$/, '')

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message) }
}

export async function apiRequest<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')
  if (options.body) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  let response: Response
  try { response = await fetch(`${baseUrl}${path}`, { ...options, headers, cache: 'no-store' }) }
  catch { throw new ApiError(0, 'Unable to connect to ServiceHub. Please try again.') }
  if (!response.ok) {
    let message = 'The request could not be completed. Please try again.'
    try {
      const problem = await response.json() as { title?: string; errors?: Record<string, string[]> }
      const errors = Object.values(problem.errors ?? {}).flat()
      message = errors.length > 0 ? errors.join(' ') : problem.title || message
    } catch { /* A non-JSON response uses the safe fallback message. */ }
    throw new ApiError(response.status, message)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}
