import { useEffect, useState } from 'react'
import BookingsPanel from '../components/BookingsPanel'
import { useAuth } from '../auth/AuthContext'

export default function AccountPage() {
  const { user, refreshUser } = useAuth()
  const [status, setStatus] = useState('Checking your account…')
  useEffect(() => {
    let active = true
    refreshUser().then(() => { if (active) setStatus('Account verified with the API.') })
      .catch(() => { if (active) setStatus('Unable to verify your account. Please try again later.') })
    return () => { active = false }
  }, [refreshUser])
  return <section className="placeholder-page">
    <p className="eyebrow">YOUR ACCOUNT</p><h1>Hello, {user?.firstName}.</h1>
    <p className="lead">You’re signed in to ServiceHub.</p>
    <dl><dt>Name</dt><dd>{user?.firstName} {user?.lastName}</dd><dt>Email</dt><dd>{user?.email}</dd><dt>Account type</dt><dd>{user?.role}</dd></dl>
    <p role="status" className="text-secondary">{status}</p>
    {user?.role === 'Customer' && <BookingsPanel />}
  </section>
}
