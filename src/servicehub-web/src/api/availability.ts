import { apiRequest } from './client'

export interface WorkingInterval { startTime: string; endTime: string }
export interface WorkingDay { dayOfWeek: number; intervals: WorkingInterval[] }
export interface WeeklyHours { days: WorkingDay[] }
export interface BlockedPeriod { id: string; startUtc: string; endUtc: string; reason: string | null }
export interface BlockedPeriodInput { startLocal: string; endLocal: string; reason: string | null }
export interface AvailableSlot { startUtc: string; endUtc: string }
export interface Availability {
  businessId: string; serviceId: string; date: string; timeZoneId: string; durationMinutes: number; slots: AvailableSlot[]
}
export const getWeeklyHours = (token: string) => apiRequest<WeeklyHours>('/api/owner/business/working-hours', {}, token)
export const saveWeeklyHours = (hours: WeeklyHours, token: string) => apiRequest<WeeklyHours>('/api/owner/business/working-hours', {
  method: 'PUT', body: JSON.stringify(hours),
}, token)
export const getBlockedPeriods = (token: string) => apiRequest<BlockedPeriod[]>('/api/owner/business/blocked-periods', {}, token)
export const createBlockedPeriod = (input: BlockedPeriodInput, token: string) => apiRequest<BlockedPeriod>('/api/owner/business/blocked-periods', {
  method: 'POST', body: JSON.stringify(input),
}, token)
export const deleteBlockedPeriod = (id: string, token: string) => apiRequest<void>(`/api/owner/business/blocked-periods/${id}`, { method: 'DELETE' }, token)
export const getAvailability = (businessId: string, serviceId: string, date: string) => {
  const query = new URLSearchParams({ serviceId, date })
  return apiRequest<Availability>(`/api/businesses/${businessId}/availability?${query}`)
}
export const dayNames: Record<number, string> = { 0: 'Sunday', 1: 'Monday', 2: 'Tuesday', 3: 'Wednesday', 4: 'Thursday', 5: 'Friday', 6: 'Saturday' }
