import { apiRequest } from './client'

export interface Booking {
  id: string; businessId: string; businessName: string; businessAddress: string; timeZoneId: string
  serviceId: string; serviceName: string; servicePrice: number; serviceCurrency: string; serviceDurationMinutes: number
  startUtc: string; endUtc: string; status: 'Confirmed' | 'Cancelled' | 'Completed'; createdAtUtc: string; cancelledAtUtc: string | null
  customer: { name: string; email: string } | null
}
export const createBooking = (serviceId: string, startUtc: string, token: string) => apiRequest<Booking>('/api/bookings', { method: 'POST', body: JSON.stringify({ serviceId, startUtc }) }, token)
export const getMyBookings = (token: string) => apiRequest<Booking[]>('/api/me/bookings', {}, token)
export const getOwnerBookings = (token: string, status = '') => apiRequest<Booking[]>(`/api/owner/business/bookings${status ? `?status=${encodeURIComponent(status)}` : ''}`, {}, token)
export const cancelBooking = (id: string, owner: boolean, token: string) => apiRequest<Booking>(`${owner ? '/api/owner/business/bookings' : '/api/bookings'}/${id}/cancel`, { method: 'POST' }, token)
export const completeBooking = (id: string, token: string) => apiRequest<Booking>(`/api/owner/business/bookings/${id}/complete`, { method: 'POST' }, token)
export const bookingTime = (booking: Booking, utc = booking.startUtc) => new Intl.DateTimeFormat('en-AU', {
  timeZone: booking.timeZoneId, dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(utc))
