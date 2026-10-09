import { useEffect, useState, type FormEvent } from 'react'
import * as api from '../api/availability'
import { ApiError } from '../api/client'
import { closureInput } from '../api/closureTimes'
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
  const [startDate, setStartDate] = useState('')
  const [startTime, setStartTime] = useState('')
  const [endDate, setEndDate] = useState('')
  const [endTime, setEndTime] = useState('')
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
    setBusy(true)
    try {
      const input = closureInput(startDate, startTime, endDate, endTime, reason)
      const created = await authenticated(() => api.createBlockedPeriod(input, token!))
      setPeriods(current => [...current, created].sort((a, b) => a.startUtc.localeCompare(b.startUtc)))
      setStartDate(''); setStartTime(''); setEndDate(''); setEndTime(''); setReason(''); setNotice('Closure added.')
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
        <h3 className="h4">Temporary closures</h3><p className="small text-secondary">Enter local times in your business time zone ({timeZoneId}). Saved times are shown in the same zone. Skipped or repeated daylight-saving hours are not accepted. Closures may span up to 366 days.</p>
        {error && <div className="alert alert-danger" role="alert">{error}</div>}
        {notice && <div className="alert alert-success" role="status">{notice}</div>}
        <form onSubmit={addClosure}><fieldset disabled={busy}><div className="row g-3">
          <div className="col-12 col-md-6"><label className="form-label" htmlFor="closure-start-date">Start date (business local)</label><input id="closure-start-date" className="form-control" type="date" required min="2000-01-01" max="2100-12-31" value={startDate} onChange={e => setStartDate(e.target.value)} /></div>
          <div className="col-12 col-md-6"><label className="form-label" htmlFor="closure-start-time">Start time (business local)</label><input id="closure-start-time" className="form-control" type="time" step={60} required value={startTime} onChange={e => setStartTime(e.target.value)} /></div>
          <div className="col-12 col-md-6"><label className="form-label" htmlFor="closure-end-date">End date (business local)</label><input id="closure-end-date" className="form-control" type="date" required min="2000-01-01" max="2100-12-31" value={endDate} onChange={e => setEndDate(e.target.value)} /></div>
          <div className="col-12 col-md-6"><label className="form-label" htmlFor="closure-end-time">End time (business local)</label><input id="closure-end-time" className="form-control" type="time" step={60} required value={endTime} onChange={e => setEndTime(e.target.value)} /></div>
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
