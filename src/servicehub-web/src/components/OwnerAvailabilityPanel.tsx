import { useEffect, useState, type FormEvent } from 'react'
import * as api from '../api/availability'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import WeeklyHoursEditor from './WeeklyHoursEditor'

export default function OwnerAvailabilityPanel({ timeZoneId }: { timeZoneId: string }) {
  const { token, logout } = useAuth()
  const [hours, setHours] = useState<api.WeeklyHours | null>(null)
  const [periods, setPeriods] = useState<api.BlockedPeriod[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [retry, setRetry] = useState(0)
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    if (!token) return
    let active = true
    setLoading(true); setLoadError('')
    Promise.all([api.getWeeklyHours(token), api.getBlockedPeriods(token)]).then(([week, closures]) => {
      if (active) { setHours(week); setPeriods(closures) }
    }).catch(err => {
      if (!active) return
      if (err instanceof ApiError && err.status === 401) logout()
      else setLoadError(err instanceof Error ? err.message : 'Unable to load availability.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [token, logout, retry])
  async function authenticated<T>(action: () => Promise<T>) {
    try { return await action() }
    catch (err) { if (err instanceof ApiError && err.status === 401) logout(); throw err }
  }
  async function saveHours(week: api.WeeklyHours) {
    const saved = await authenticated(() => api.saveWeeklyHours(week, token!))
    setHours(saved); return saved
  }
  async function addClosure(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setNotice('')
    if (!start || !end || start >= end) { setError('Closure end must be after its start.'); return }
    setBusy(true)
    try {
      // The input explicitly asks for UTC: never interpret it in the browser's local zone.
      const created = await authenticated(() => api.createBlockedPeriod({
        startUtc: new Date(`${start}:00Z`).toISOString(), endUtc: new Date(`${end}:00Z`).toISOString(), reason: reason.trim() || null,
      }, token!))
      setPeriods(current => [...current, created].sort((a, b) => a.startUtc.localeCompare(b.startUtc)))
      setStart(''); setEnd(''); setReason(''); setNotice('Closure added.')
    } catch (err) { setError(err instanceof Error ? err.message : 'Unable to add closure.') }
    finally { setBusy(false) }
  }
  async function removeClosure(period: api.BlockedPeriod) {
    setBusy(true); setError(''); setNotice('')
    try {
      await authenticated(() => api.deleteBlockedPeriod(period.id, token!))
      setPeriods(current => current.filter(p => p.id !== period.id)); setNotice('Closure removed.')
    } catch (err) { setError(err instanceof Error ? err.message : 'Unable to remove closure.') }
    finally { setBusy(false) }
  }
  const localTime = (utc: string) => new Intl.DateTimeFormat('en-AU', { timeZone: timeZoneId, dateStyle: 'medium', timeStyle: 'short' }).format(new Date(utc))
  return <section className="mt-5" aria-labelledby="availability-heading">
    <p className="eyebrow mb-2">WHEN YOU ARE OPEN</p><h2 id="availability-heading">Availability</h2>
    <p className="text-secondary mb-4">Set your regular week, then make room for holidays and closures.</p>
    {loading ? <p role="status">Loading availability…</p> : loadError ? <div className="alert alert-danger" role="alert">{loadError}<button className="btn btn-link" onClick={() => setRetry(n => n + 1)}>Retry</button></div> : hours && <>
      <WeeklyHoursEditor initialHours={hours} timeZoneId={timeZoneId} onSave={saveHours} />
      <div className="panel p-4 mt-4">
        <h3 className="h4">Temporary closures</h3><p className="small text-secondary">Enter times in UTC. Your business-local times ({timeZoneId}) are shown below after saving. Closures may span up to 366 days.</p>
        {error && <div className="alert alert-danger" role="alert">{error}</div>}
        {notice && <div className="alert alert-success" role="status">{notice}</div>}
        <form onSubmit={addClosure}><fieldset disabled={busy}><div className="row g-3">
          <div className="col-md-6"><label className="form-label" htmlFor="closure-start">Closure start (UTC)</label><input id="closure-start" className="form-control" type="datetime-local" step={60} required min="2000-01-01T00:00" max="2100-12-31T23:59" value={start} onChange={e => setStart(e.target.value)} /></div>
          <div className="col-md-6"><label className="form-label" htmlFor="closure-end">Closure end (UTC)</label><input id="closure-end" className="form-control" type="datetime-local" step={60} required min="2000-01-01T00:00" max="2100-12-31T23:59" value={end} onChange={e => setEnd(e.target.value)} /></div>
          <div className="col-12"><label className="form-label" htmlFor="closure-reason">Reason (optional)</label><input id="closure-reason" className="form-control" maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></div>
        </div><button className="btn btn-outline-primary mt-3">{busy ? 'Saving…' : 'Add closure'}</button></fieldset></form>
        <h4 className="h6 mt-4">Upcoming and ongoing closures</h4>
        {periods.length === 0 ? <p className="small text-secondary mb-0">No upcoming closures.</p> : <ul className="list-unstyled mb-0">{periods.map(period => <li className="closure-item" key={period.id}>
          <div><strong>{period.reason || 'Temporary closure'}</strong><p className="small mb-1">{localTime(period.startUtc)} – {localTime(period.endUtc)}</p><p className="small text-secondary mb-0">UTC: {period.startUtc} – {period.endUtc}</p></div>
          <button type="button" className="btn btn-outline-secondary btn-sm" disabled={busy} aria-label={`Remove closure ${period.reason || 'Temporary closure'}`} onClick={() => removeClosure(period)}>Remove</button>
        </li>)}</ul>}
      </div>
    </>}
  </section>
}
