import { apiRequest } from './client'

export interface Category { id: string; name: string }
export interface BusinessTimeZone { id: string; label: string }
export interface BusinessInput {
  name: string; description: string; businessCategoryId: string; address: string
  contactPhone: string | null; contactEmail: string; timeZoneId: string
}
export interface Business {
  id: string; name: string; description: string; category: Category; address: string
  contactPhone: string | null; contactEmail: string; timeZoneId: string; isActive: boolean
  createdAtUtc: string; updatedAtUtc: string
}
export interface BusinessSummary {
  id: string; name: string; description: string; category: Category; address: string; activeServiceCount: number
}
export interface BusinessPage { items: BusinessSummary[]; totalCount: number; page: number; pageSize: number }
export const getCategories = () => apiRequest<Category[]>('/api/categories')
export const getTimeZones = () => apiRequest<BusinessTimeZone[]>('/api/time-zones')
export const browseBusinesses = (query: URLSearchParams) => apiRequest<BusinessPage>(`/api/businesses?${query}`)
export const getBusiness = (id: string) => apiRequest<Business>(`/api/businesses/${id}`)
export const getOwnerBusiness = (token: string) => apiRequest<Business>('/api/owner/business', {}, token)
export const createBusiness = (input: BusinessInput, token: string) => apiRequest<Business>('/api/businesses', {
  method: 'POST', body: JSON.stringify(input),
}, token)
export const updateBusiness = (id: string, input: BusinessInput, token: string) => apiRequest<Business>(`/api/businesses/${id}`, {
  method: 'PUT', body: JSON.stringify(input),
}, token)
