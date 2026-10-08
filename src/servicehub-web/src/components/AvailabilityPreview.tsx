import { useEffect, useRef, useState, type FormEvent } from 'react'
import { getAvailability, type Availability } from '../api/availability'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { createBooking } from '../api/bookings'
import { ApiError } from '../api/client'
import type { BusinessService } from '../api/services'

interface Props { businessId: string; timeZoneId: string; services: BusinessService[] }
function businessToday(timeZoneId: string) {
  const parts = new Intl.DateTimeFormat('en-AU', { timeZone: timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const part = (type: string) => parts.find(p => p.type === type)!.value
  return `${part('year')}-${part('month')}-${part('day')}`
}
export default function AvailabilityPreview({ businessId, timeZoneId, services }: Props) {
  const { user, token, logout } = useAuth()
  const location = useLocation()
  const [selected, setSelected] = useState('')
  const [bookingBusy, setBookingBusy] = useState(false)
  const [success, setSuccess] = useState('')
  const [serviceId, setServiceId] = useState(services[0]?.id ?? '')
  const [date, setDate] = useState(() => businessToday(timeZoneId))
  const [result, setResult] = useState<Availability | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const requestVersion = useRef(0)
  useEffect(() => () => { requestVersion.current++ }, [])
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setSuccess(''); await refreshSlots()
  }
  async function refreshSlots() {
    setBusy(true); setResult(null); setSelected('')
    const version = ++requestVersion.current
    try { const response = await getAvailability(businessId, serviceId, date); if (version === requestVersion.current) setResult(response) }
    catch (err) { if (version === requestVersion.current) setError(err instanceof Error ? err.message : 'Unable to retrieve available times.') }
    finally { if (version === requestVersion.current) setBusy(false) }
  }
  async function book() {
    if (!token || user?.role !== 'Customer' || !selected) return
    setBookingBusy(true); setError(''); setSuccess('')
    try {
      await createBooking(serviceId, selected, token)
      setSuccess('Your appointment is confirmed. View it in My Bookings.')
      await refreshSlots()
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) logout()
      else if (err instanceof ApiError && err.status === 409) { setError('That time is no longer available. Please choose another time.'); await refreshSlots() }
      else setError(err instanceof Error ? err.message : 'Unable to book this appointment.')
    } finally { setBookingBusy(false) }
  }
  if (services.length === 0) return null
  const displayTime = (utc: string) => new Intl.DateTimeFormat('en-AU', { timeZone: timeZoneId, hour: 'numeric', minute: '2-digit' }).format(new Date(utc))
  return <section className="panel p-4 mt-4" aria-labelledby="preview-heading">
    <p className="eyebrow mb-2">PLAN YOUR VISIT</p><h2 className="h4" id="preview-heading">Available times</h2>
    <p className="small text-secondary">Times are shown in {timeZoneId}. Choose a start time, then confirm your appointment. Availability is checked again when you book.</p>
    <form onSubmit={submit}><fieldset disabled={busy || bookingBusy}><div className="row g-3">
      <div className="col-md-7"><label className="form-label" htmlFor="availability-service">Choose a service</label><select id="availability-service" className="form-select" required value={serviceId} onChange={e => { setServiceId(e.target.value); setResult(null); setSelected(''); setError(''); setSuccess('') }}>{services.map(s => <option key={s.id} value={s.id}>{s.name} · {s.durationMinutes} min</option>)}</select></div>
      <div className="col-md-5"><label className="form-label" htmlFor="availability-date">Business-local date</label><input id="availability-date" className="form-control" type="date" required min={businessToday(timeZoneId)} max="2100-12-31" value={date} onChange={e => { setDate(e.target.value); setResult(null); setSelected(''); setError(''); setSuccess('') }} /></div>
    </div><button className="btn btn-primary mt-3">{busy ? 'Checking…' : 'Show available times'}</button></fieldset></form>
    {success && <div className="alert alert-success mt-3" role="status">{success} <Link to="/account">My Bookings</Link></div>}
    {error && <div className="alert alert-danger mt-3 mb-0" role="alert">{error}</div>}
    {result && <div className="mt-4" role="status"><p className="small text-secondary">{result.slots.length} start times · {result.durationMinutes}-minute appointments · {result.date}</p>
      {result.slots.length === 0 ? <p className="mb-0">No available times for this service and date. Try another date.</p> : <ul className="slot-grid">{result.slots.map(slot => <li key={slot.startUtc}><button type="button" className={`slot-button ${selected === slot.startUtc ? 'selected' : ''}`} aria-pressed={selected === slot.startUtc} disabled={bookingBusy || busy} onClick={() => { setSelected(slot.startUtc); setError(''); setSuccess('') }}><time dateTime={slot.startUtc}>{displayTime(slot.startUtc)}</time></button></li>)}</ul>}
    </div>}
    {selected && <div className="booking-confirmation mt-3 p-3"><p className="fw-semibold mb-2">Selected: {displayTime(selected)} on {date}</p>
      {!user ? <p className="mb-0"><Link to="/login" state={{ from: location.pathname }}>Log in to book this appointment</Link>. You can select your time again after signing in.</p>
        : user.role !== 'Customer' ? <p className="mb-0">Only Customer accounts can book appointments. BusinessOwner accounts manage their own business.</p>
        : <button className="btn btn-primary" disabled={bookingBusy || busy} onClick={book}>{bookingBusy ? 'Confirming…' : 'Confirm booking'}</button>}
    </div>}
  </section>
}
