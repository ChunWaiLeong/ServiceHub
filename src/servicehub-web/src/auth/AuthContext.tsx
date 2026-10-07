import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth'
import { ApiError } from '../api/client'

interface AuthState {
  user: authApi.AuthUser | null
  token: string | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<authApi.AuthUser>
  logout: () => void
  refreshUser: () => Promise<authApi.AuthUser | null>
}
const AuthContext = createContext<AuthState | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  // Memory-only: no browser storage or refresh tokens in this phase.
  const [session, setSession] = useState<authApi.LoginResponse | null>(null)
  const logout = useCallback(() => setSession(null), [])
  useEffect(() => {
    if (!session) return
    const remaining = Date.parse(session.expiresAtUtc) - Date.now()
    if (remaining <= 0) { logout(); return }
    const timer = window.setTimeout(logout, remaining)
    return () => window.clearTimeout(timer)
  }, [session, logout])

  async function login(email: string, password: string) {
    const result = await authApi.login(email.trim(), password)
    setSession(result)
    return result.user
  }

  const refreshUser = useCallback(async () => {
    if (!session) return null
    try {
      const user = await authApi.getMe(session.accessToken)
      setSession(current => current?.accessToken === session.accessToken ? { ...current, user } : current)
      return user
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) logout()
      throw error
    }
  }, [session?.accessToken, logout])

  return <AuthContext.Provider value={{ user: session?.user ?? null, token: session?.accessToken ?? null,
    isAuthenticated: session !== null, login, logout, refreshUser }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}
