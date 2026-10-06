export interface HealthResponse {
  application: string
  status: string
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080').replace(/\/$/, '')

export async function getHealth(signal: AbortSignal): Promise<HealthResponse> {
  const response = await fetch(`${apiBaseUrl}/api/health`, { signal })
  if (!response.ok) throw new Error(`Health request failed (${response.status})`)

  const data: unknown = await response.json()
  if (typeof data !== 'object' || data === null ||
      !('application' in data) || data.application !== 'ServiceHub API' ||
      !('status' in data) || data.status !== 'Healthy') {
    throw new Error('Unexpected health response')
  }
  return { application: data.application, status: data.status }
}
