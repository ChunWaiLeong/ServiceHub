import { useEffect, useRef, useState, type FormEvent } from 'react'
import { getAvailability, type Availability } from '../api/availability'
import type { BusinessService } from '../api/services'

interface Props { businessId: string; timeZoneId: string; services: BusinessService[] }
function businessToday(timeZoneId: string) {
  const parts = new Intl.DateTimeFormat('en-AU', { timeZone: timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const part = (type: string) => parts.find(p => p.type === type)!.value
  return `${part('year')}-${part('month')}-${part('day')}`
}
export default function AvailabilityPreview({ businessId, timeZoneId, services }: Props) {
  const [serviceId, setServiceId] = useState(services[0]?.id ?? '')
  const [date, setDate] = useState(() => businessToday(timeZoneId))
  const [result, setResult] = useState<Availability | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const requestVersion = useRef(0)
  useEffect(() => () => { requestVersion.current++ }, [])
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(''); setResult(null)
    const version = ++requestVersion.current
    try { const response = await getAvailability(businessId, serviceId, date); if (version === requestVersion.current) setResult(response) }
    catch (err) { if (version === requestVersion.current) setError(err instanceof Error ? err.message : 'Unable to retrieve available times.') }
    finally { if (version === requestVersion.current) setBusy(false) }
  }
  if (services.length === 0) return null
  const displayTime = (utc: string) => new Intl.DateTimeFormat('en-AU', { timeZone: timeZoneId, hour: 'numeric', minute: '2-digit' }).format(new Date(utc))
  return <section className="panel p-4 mt-4" aria-labelledby="preview-heading">
    <p className="eyebrow mb-2">PLAN YOUR VISIT</p><h2 className="h4" id="preview-heading">Available times</h2>
    <p className="small text-secondary">Times are shown in {timeZoneId}. This is a preview; booking is coming in the next release.</p>
    <form onSubmit={submit}><fieldset disabled={busy}><div className="row g-3">
      <div className="col-md-7"><label className="form-label" htmlFor="availability-service">Choose a service</label><select id="availability-service" className="form-select" required value={serviceId} onChange={e => { setServiceId(e.target.value); setResult(null); setError('') }}>{services.map(s => <option key={s.id} value={s.id}>{s.name} · {s.durationMinutes} min</option>)}</select></div>
      <div className="col-md-5"><label className="form-label" htmlFor="availability-date">Business-local date</label><input id="availability-date" className="form-control" type="date" required min={businessToday(timeZoneId)} max="2100-12-31" value={date} onChange={e => { setDate(e.target.value); setResult(null); setError('') }} /></div>
    </div><button className="btn btn-primary mt-3">{busy ? 'Checking…' : 'Show available times'}</button></fieldset></form>
    {error && <div className="alert alert-danger mt-3 mb-0" role="alert">{error}</div>}
    {result && <div className="mt-4" role="status"><p className="small text-secondary">{result.slots.length} start times · {result.durationMinutes}-minute appointments · {result.date}</p>
      {result.slots.length === 0 ? <p className="mb-0">No available times for this service and date. Try another date.</p> : <ul className="slot-grid">{result.slots.map(slot => <li key={slot.startUtc}><time dateTime={slot.startUtc}>{displayTime(slot.startUtc)}</time></li>)}</ul>}
    </div>}
  </section>
}
