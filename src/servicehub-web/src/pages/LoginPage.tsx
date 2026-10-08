import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export default function LoginPage() {
  const { login, isAuthenticated, user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  function destination(role: string) {
    const from = (location.state as { from?: string } | null)?.from
    return from && (/^\/businesses\/[0-9a-f-]{36}$/i.test(from) || from === '/account'
      || (from === '/business/dashboard' && role === 'BusinessOwner')) ? from : '/account'
  }
  if (isAuthenticated && user) return <Navigate to={destination(user.role)} replace />

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(''); setBusy(true)
    try {
      const user = await login(email, password)
      setPassword('')
      navigate(destination(user.role), { replace: true })
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to connect. Please try again.')
    } finally { setBusy(false) }
  }

  return <section className="auth-page mx-auto">
    <p className="eyebrow">YOUR SERVICEHUB ACCOUNT</p><h1>Welcome back.</h1>
    <p className="text-secondary">Sign in to your account.</p>
    {location.state?.registered && <div className="alert alert-success" role="status">Account created. Please log in.</div>}
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    <form onSubmit={submit} className="auth-form" aria-busy={busy}>
      <label className="form-label" htmlFor="login-email">Email</label>
      <input id="login-email" className="form-control mb-3" type="email" autoComplete="username" required maxLength={254}
        value={email} onChange={event => setEmail(event.target.value)} disabled={busy} />
      <label className="form-label" htmlFor="login-password">Password</label>
      <input id="login-password" className="form-control mb-4" type="password" autoComplete="current-password" required maxLength={128}
        value={password} onChange={event => setPassword(event.target.value)} disabled={busy} />
      <button className="btn btn-primary w-100" disabled={busy}>{busy ? 'Signing in…' : 'Login'}</button>
    </form>
    <p className="mt-4">New to ServiceHub? <Link to="/register">Create an account</Link></p>
  </section>
}
