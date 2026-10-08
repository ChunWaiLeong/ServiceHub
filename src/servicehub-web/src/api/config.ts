import { resolveApiBaseUrl } from '../../api-config'

export const apiBaseUrl = resolveApiBaseUrl(import.meta.env.VITE_API_BASE_URL, import.meta.env.PROD)
