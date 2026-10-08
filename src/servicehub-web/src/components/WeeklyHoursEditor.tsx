import { useState, type FormEvent } from 'react'
import { dayNames, type WeeklyHours, type WorkingDay } from '../api/availability'

interface Props { initialHours: WeeklyHours; timeZoneId: string; onSave: (hours: WeeklyHours) => Promise<WeeklyHours> }
const normalize = (hours: WeeklyHours) => hours.days.map(day => ({ ...day,
  intervals: day.intervals.map(i => ({ startTime: i.startTime.slice(0, 5), endTime: i.endTime.slice(0, 5) })) }))

export default function WeeklyHoursEditor({ initialHours, timeZoneId, onSave }: Props) {
  const [days, setDays] = useState<WorkingDay[]>(() => normalize(initialHours))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)
  function change(dayOfWeek: number, update: (day: WorkingDay) => WorkingDay) {
    setDays(current => current.map(day => day.dayOfWeek === dayOfWeek ? update(day) : day)); setSaved(false)
  }
  function add(day: WorkingDay) {
    const previousEnd = day.intervals.at(-1)?.endTime ?? '09:00'
    const start = previousEnd < '13:00' ? '13:00' : previousEnd
    const endHour = Math.min(Number(start.slice(0, 2)) + 1, 23)
    change(day.dayOfWeek, current => ({ ...current, intervals: [...current.intervals,
      { startTime: start, endTime: `${String(endHour).padStart(2, '0')}:${start.slice(3)}` }] }))
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setSaved(false)
    for (const day of days) {
      const ordered = [...day.intervals].sort((a, b) => a.startTime.localeCompare(b.startTime))
      if (ordered.some(i => !i.startTime || !i.endTime || i.startTime >= i.endTime)) {
        setError(`${dayNames[day.dayOfWeek]}: each interval must start before it ends. Overnight hours are not supported.`); return
      }
      if (ordered.some((i, index) => index > 0 && i.startTime < ordered[index - 1].endTime)) {
        setError(`${dayNames[day.dayOfWeek]}: working intervals must not overlap.`); return
      }
    }
    setBusy(true)
    try { setDays(normalize(await onSave({ days }))); setSaved(true) }
    catch (err) { setError(err instanceof Error ? err.message : 'Unable to save weekly hours.') }
    finally { setBusy(false) }
  }
  return <form className="panel p-4" onSubmit={submit}>
    <h3 className="h4">Weekly hours</h3>
    <p className="small text-secondary">Local times in {timeZoneId}. Add separate intervals for lunch breaks. Saving replaces the complete week.</p>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    {saved && <div className="alert alert-success" role="status">Weekly hours saved.</div>}
    <fieldset disabled={busy}>
      {days.map(day => <div className="working-day" key={day.dayOfWeek}>
        <div className="day-heading"><strong>{dayNames[day.dayOfWeek]}</strong>
          <label className="form-check"><input className="form-check-input" type="checkbox" checked={day.intervals.length > 0}
            aria-label={`${dayNames[day.dayOfWeek]} open`} onChange={e => change(day.dayOfWeek, current => ({ ...current,
              intervals: e.target.checked ? [{ startTime: '09:00', endTime: '17:00' }] : [] }))} /><span className="form-check-label">{day.intervals.length ? 'Open' : 'Closed'}</span></label>
        </div>
        <div className="day-intervals">
          {day.intervals.map((interval, index) => <div className="hours-interval" key={index}>
            <label className="small">From<input className="form-control" type="time" step={60} required value={interval.startTime}
              aria-label={`${dayNames[day.dayOfWeek]} interval ${index + 1} start`} onChange={e => change(day.dayOfWeek, current => ({ ...current,
                intervals: current.intervals.map((item, n) => n === index ? { ...item, startTime: e.target.value } : item) }))} /></label>
            <label className="small">To<input className="form-control" type="time" step={60} required value={interval.endTime}
              aria-label={`${dayNames[day.dayOfWeek]} interval ${index + 1} end`} onChange={e => change(day.dayOfWeek, current => ({ ...current,
                intervals: current.intervals.map((item, n) => n === index ? { ...item, endTime: e.target.value } : item) }))} /></label>
            <button type="button" className="btn btn-outline-secondary btn-sm" aria-label={`Remove ${dayNames[day.dayOfWeek]} interval ${index + 1}`}
              onClick={() => change(day.dayOfWeek, current => ({ ...current, intervals: current.intervals.filter((_, n) => n !== index) }))}>Remove</button>
          </div>)}
          {day.intervals.length > 0 && <button type="button" className="btn btn-link p-0 small" disabled={day.intervals.length >= 8} onClick={() => add(day)}>Add {dayNames[day.dayOfWeek]} interval</button>}
        </div>
      </div>)}
      <button className="btn btn-primary mt-4">{busy ? 'Saving…' : 'Save weekly hours'}</button>
    </fieldset>
  </form>
}
