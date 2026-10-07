import { apiRequest } from './client'

export interface ServiceInput { name: string; description: string; price: number; currency: 'AUD'; durationMinutes: number }
export interface BusinessService extends ServiceInput {
  id: string; businessId: string; isActive: boolean; createdAtUtc: string; updatedAtUtc: string
}
export const getPublicServices = (businessId: string) => apiRequest<BusinessService[]>(`/api/businesses/${businessId}/services`)
export const getOwnerServices = (token: string) => apiRequest<BusinessService[]>('/api/owner/business/services', {}, token)
export const createService = (businessId: string, input: ServiceInput, token: string) => apiRequest<BusinessService>(`/api/businesses/${businessId}/services`, {
  method: 'POST', body: JSON.stringify(input),
}, token)
export const updateService = (businessId: string, id: string, input: ServiceInput, token: string) => apiRequest<BusinessService>(`/api/businesses/${businessId}/services/${id}`, {
  method: 'PUT', body: JSON.stringify(input),
}, token)
export const setServiceStatus = (businessId: string, id: string, isActive: boolean, token: string) => apiRequest<BusinessService>(`/api/businesses/${businessId}/services/${id}/status`, {
  method: 'PATCH', body: JSON.stringify({ isActive }),
}, token)
