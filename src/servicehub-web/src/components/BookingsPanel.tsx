import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import * as api from '../api/bookings'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function BookingsPanel({ owner = false }: { owner?: boolean }) {
  const { token, logout } = useAuth()
  const [items, setItems] = useState<api.Booking[]>([])
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    if (!token) return
    let active = true
    setLoading(true); setError('')
    const request = owner ? api.getOwnerBookings(token, status) : api.getMyBookings(token)
    request.then(result => { if (active) setItems(result) }).catch(err => {
      if (!active) return
      if (err instanceof ApiError && err.status === 401) logout()
      else setError(err instanceof Error ? err.message : 'Unable to load bookings.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [token, owner, status, retry, logout])
  async function transition(booking: api.Booking, action: 'cancel' | 'complete') {
    if (!token) return
    setPending(booking.id); setError(''); setNotice('')
    try {
      await (action === 'cancel' ? api.cancelBooking(booking.id, owner, token) : api.completeBooking(booking.id, token))
      setNotice(action === 'cancel' ? 'Appointment cancelled. Its time is available again if the business is open.' : 'Appointment marked completed.')
      setRetry(n => n + 1)
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) logout()
      else setError(err instanceof Error ? err.message : 'Unable to update booking.')
    } finally { setPending(null) }
  }
  const now = Date.now()
  const upcoming = items.filter(b => b.status === 'Confirmed' && Date.parse(b.startUtc) > now)
  const history = items.filter(b => !upcoming.includes(b))
  function cards(bookings: api.Booking[]) {
    return bookings.length === 0 ? <p className="text-secondary">No appointments here yet.</p> : <div className="d-grid gap-3">{bookings.map(b => {
      const future = Date.parse(b.startUtc) > now
      const canCancel = b.status === 'Confirmed' && (owner || future)
      const canComplete = owner && b.status === 'Confirmed' && Date.parse(b.endUtc) <= now
      return <article className="panel p-4 booking-card" key={b.id}>
        <div className="d-flex flex-wrap justify-content-between gap-2"><div><h4 className="h5 mb-1">{b.serviceName}</h4><Link to={`/businesses/${b.businessId}`}>{b.businessName}</Link></div><span className={`badge align-self-start booking-status-${b.status.toLowerCase()}`}>{b.status}</span></div>
        <p className="mt-3 mb-1 fw-semibold">{api.bookingTime(b)}</p><p className="small text-secondary">Ends {api.bookingTime(b, b.endUtc)} · {b.timeZoneId}</p>
        <p className="mb-2">{b.serviceDurationMinutes} minutes · {b.servicePrice.toLocaleString('en-AU', { style: 'currency', currency: b.serviceCurrency })} {b.serviceCurrency}</p>
        {!owner && <p className="small text-secondary">{b.businessAddress}</p>}
        {owner && b.customer && <div className="booking-customer small"><strong>{b.customer.name}</strong><span className="d-block">{b.customer.email}</span></div>}
        {b.cancelledAtUtc && <p className="small text-secondary mb-2">Cancelled {api.bookingTime(b, b.cancelledAtUtc)}</p>}
        {(canCancel || canComplete) && <div className="d-flex flex-wrap gap-2 mt-3">{canCancel && <button className="btn btn-outline-secondary btn-sm" disabled={pending !== null || loading} onClick={() => transition(b, 'cancel')}>Cancel appointment</button>}{canComplete && <button className="btn btn-primary btn-sm" disabled={pending !== null || loading} onClick={() => transition(b, 'complete')}>Mark completed</button>}{pending === b.id && <span role="status">Saving…</span>}</div>}
      </article>
    })}</div>
  }
  return <section className="mt-5" aria-label={owner ? 'Business bookings' : 'My bookings'}>
    <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-3"><div><p className="eyebrow mb-2">YOUR APPOINTMENTS</p><h2>{owner ? 'Business bookings' : 'My bookings'}</h2></div><button className="btn btn-outline-primary btn-sm" disabled={loading || pending !== null} onClick={() => setRetry(n => n + 1)}>Refresh bookings</button></div>
    {owner && <div className="mb-3"><label className="form-label" htmlFor="booking-status">Filter by status</label><select id="booking-status" className="form-select" value={status} disabled={pending !== null} onChange={e => setStatus(e.target.value)}><option value="">All statuses</option>{['Confirmed','Cancelled','Completed'].map(s => <option key={s}>{s}</option>)}</select></div>}
    {!owner && <p className="text-secondary">Cancel any Confirmed appointment before it starts. All times use the business time zone.</p>}
    {notice && <div className="alert alert-success" role="status">{notice}</div>}
    {error && <div className="alert alert-danger" role="alert">{error}<button className="btn btn-link" onClick={() => setRetry(n => n + 1)}>Retry</button></div>}
    {loading ? <p role="status">Loading appointments…</p> : <><h3 className="h5 mt-4">Upcoming</h3>{cards(upcoming)}<h3 className="h5 mt-4">{owner ? 'Past appointments and history' : 'History'}</h3>{cards(history)}</>}
  </section>
}
