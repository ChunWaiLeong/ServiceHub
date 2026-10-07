import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { register, type RegisterRequest } from '../api/auth'
import { useAuth } from '../auth/AuthContext'

export default function RegisterPage() {
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState<RegisterRequest>({ firstName: '', lastName: '', email: '', password: '', role: 'Customer' })
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  if (isAuthenticated) return <Navigate to="/account" replace />
  const change = (key: keyof RegisterRequest, value: string) => setForm(current => ({ ...current, [key]: value }))

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('')
    if (!form.firstName.trim() || !form.lastName.trim()) { setError('Enter your first and last names.'); return }
    if (form.password !== confirm) { setError('Passwords do not match.'); return }
    if (!/[a-z]/.test(form.password) || !/[A-Z]/.test(form.password) || !/[0-9]/.test(form.password) || !/[^a-zA-Z0-9]/.test(form.password)) {
      setError('Include uppercase, lowercase, a number, and a symbol in your password.'); return
    }
    setBusy(true)
    try {
      await register({ ...form, firstName: form.firstName.trim(), lastName: form.lastName.trim(), email: form.email.trim() })
      navigate('/login', { replace: true, state: { registered: true } })
    } catch (failure) { setError(failure instanceof Error ? failure.message : 'Unable to connect. Please try again.') }
    finally { setBusy(false) }
  }

  return <section className="auth-page mx-auto">
    <p className="eyebrow">MAKE YOURSELF AT HOME</p><h1>Create your account.</h1>
    <p className="text-secondary">Join as a customer or a business owner.</p>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    <form onSubmit={submit} className="auth-form" aria-busy={busy}>
      <div className="row g-3 mb-3">{(['firstName', 'lastName'] as const).map((key, index) => <div className="col-sm-6" key={key}>
        <label className="form-label" htmlFor={key}>{index === 0 ? 'First name' : 'Last name'}</label>
        <input id={key} className="form-control" autoComplete={index === 0 ? 'given-name' : 'family-name'} required maxLength={100}
          value={form[key]} onChange={event => change(key, event.target.value)} disabled={busy} />
      </div>)}</div>
      <label className="form-label" htmlFor="register-email">Email</label>
      <input id="register-email" className="form-control mb-3" type="email" autoComplete="username" required maxLength={254}
        value={form.email} onChange={event => change('email', event.target.value)} disabled={busy} />
      <label className="form-label" htmlFor="register-password">Password</label>
      <input id="register-password" className="form-control" type="password" autoComplete="new-password" required minLength={12} maxLength={128}
        aria-describedby="password-help" value={form.password} onChange={event => change('password', event.target.value)} disabled={busy} />
      <p className="form-text mb-3" id="password-help">12–128 characters, including uppercase, lowercase, a number, and a symbol.</p>
      <label className="form-label" htmlFor="confirm-password">Confirm password</label>
      <input id="confirm-password" className="form-control mb-3" type="password" autoComplete="new-password" required minLength={12} maxLength={128}
        value={confirm} onChange={event => setConfirm(event.target.value)} disabled={busy} />
      <label className="form-label" htmlFor="account-type">Account type</label>
      <select id="account-type" className="form-select mb-4" value={form.role} onChange={event => change('role', event.target.value)} disabled={busy}>
        <option value="Customer">Customer</option><option value="BusinessOwner">Business Owner</option>
      </select>
      <button className="btn btn-primary w-100" disabled={busy}>{busy ? 'Creating account…' : 'Create account'}</button>
    </form>
    <p className="mt-4">Already have an account? <Link to="/login">Login</Link></p>
  </section>
}
