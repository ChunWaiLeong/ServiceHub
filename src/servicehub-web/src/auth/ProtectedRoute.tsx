import { Navigate, Outlet, useLocation, Link } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { Role } from '../api/auth'

export default function ProtectedRoute({ role }: { role?: Role }) {
  const { user, isAuthenticated } = useAuth()
  const location = useLocation()
  if (!isAuthenticated) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (role && user?.role !== role) return <section className="placeholder-page">
    <h1>Access unavailable.</h1><p>This page requires a {role} account.</p><Link to="/account">Go to your account</Link>
  </section>
  return <Outlet />
}
