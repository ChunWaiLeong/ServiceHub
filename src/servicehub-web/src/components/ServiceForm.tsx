import { useState, type FormEvent } from 'react'
import type { BusinessService, ServiceInput } from '../api/services'

interface Props { service?: BusinessService; onSave: (input: ServiceInput) => Promise<void>; onCancel: () => void }
export default function ServiceForm({ service, onSave, onCancel }: Props) {
  const [name, setName] = useState(service?.name ?? '')
  const [description, setDescription] = useState(service?.description ?? '')
  const [price, setPrice] = useState(service ? String(service.price) : '')
  const [duration, setDuration] = useState(service ? String(service.durationMinutes) : '30')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('')
    try { await onSave({ name, description, price: Number(price), durationMinutes: Number(duration), currency: 'AUD' }) }
    catch (err) { setError(err instanceof Error ? err.message : 'Unable to save this service.') }
    finally { setBusy(false) }
  }
  return <form className="panel p-4 mb-4" onSubmit={submit}>
    <h3 className="h4 mb-3">{service ? 'Edit service' : 'Create a service'}</h3>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    <fieldset disabled={busy}><div className="row g-3">
      <div className="col-12"><label className="form-label" htmlFor="service-name">Service name</label><input className="form-control" id="service-name" required maxLength={200} value={name} onChange={e => setName(e.target.value)} /></div>
      <div className="col-12"><label className="form-label" htmlFor="service-description">Service description</label><textarea className="form-control" id="service-description" required rows={3} maxLength={2000} value={description} onChange={e => setDescription(e.target.value)} /></div>
      <div className="col-md-6"><label className="form-label" htmlFor="service-price">Price (AUD)</label><input className="form-control" type="number" id="service-price" required min="0.01" max="9999999999.99" step="0.01" value={price} onChange={e => setPrice(e.target.value)} /></div>
      <div className="col-md-6"><label className="form-label" htmlFor="service-duration">Duration (minutes)</label><input className="form-control" type="number" id="service-duration" required min={1} max={480} step={1} value={duration} onChange={e => setDuration(e.target.value)} /></div>
    </div><p className="small text-secondary mt-3">AUD only. New services are active and visible on your public profile.</p><div className="d-flex gap-2 mt-3"><button className="btn btn-primary">{busy ? 'Saving…' : 'Save service'}</button><button type="button" className="btn btn-outline-secondary" onClick={onCancel}>Cancel</button></div></fieldset>
  </form>
}
