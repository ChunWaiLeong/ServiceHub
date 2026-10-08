export function resolveApiBaseUrl(value: string | undefined, production: boolean): string {
  const configured = value?.trim()
  if (production) {
    let url: URL
    try { url = new URL(configured ?? '') }
    catch { throw new Error('VITE_API_BASE_URL is required for production and must be an absolute HTTPS API origin.') }
    if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash || url.pathname !== '/') {
      throw new Error('VITE_API_BASE_URL must be an HTTPS API origin without credentials, paths, query strings or fragments.')
    }
    return url.origin
  }
  return (configured || 'http://localhost:5080').replace(/\/$/, '')
}
