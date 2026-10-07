import { apiRequest } from './client'

export type Role = 'Customer' | 'BusinessOwner' | 'Admin'
export interface AuthUser { id: string; firstName: string; lastName: string; email: string; role: Role }
export interface LoginResponse { accessToken: string; expiresAtUtc: string; user: AuthUser }
export interface RegisterRequest {
  firstName: string; lastName: string; email: string; password: string; role: 'Customer' | 'BusinessOwner'
}

export const register = (request: RegisterRequest) => apiRequest<{ message: string }>('/api/auth/register', {
  method: 'POST', body: JSON.stringify(request),
})
export const login = (email: string, password: string) => apiRequest<LoginResponse>('/api/auth/login', {
  method: 'POST', body: JSON.stringify({ email, password }),
})
export const getMe = (token: string) => apiRequest<AuthUser>('/api/auth/me', {}, token)
export const getBusinessOwnerAccess = (token: string) => apiRequest<{ message: string }>('/api/auth/business-owner', {}, token)
