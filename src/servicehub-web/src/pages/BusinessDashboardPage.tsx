import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { getBusinessOwnerAccess } from '../api/auth'
import { ApiError } from '../api/client'

export default function BusinessDashboardPage() {
  const { token, logout } = useAuth()
  const [status, setStatus] = useState('Checking business owner access…')
  useEffect(() => {
    let active = true
    if (token) getBusinessOwnerAccess(token)
      .then(result => { if (active) setStatus(result.message) })
      .catch(error => {
        if (!active) return
        if (error instanceof ApiError && error.status === 401) logout()
        else setStatus('Unable to confirm business owner access.')
      })
    return () => { active = false }
  }, [token, logout])
  return <section className="placeholder-page">
    <p className="eyebrow">BUSINESS OWNER</p><h1>Your business dashboard.</h1>
    <p role="status">{status}</p><p>Business profiles and service management will arrive in Phase 4.</p>
  </section>
}
