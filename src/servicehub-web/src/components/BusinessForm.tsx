import { useState, type FormEvent } from 'react'
import type { Business, BusinessInput, BusinessTimeZone, Category } from '../api/businesses'

interface Props {
  business?: Business; categories: Category[]; timeZones: BusinessTimeZone[]
  onSave: (input: BusinessInput) => Promise<void>; onCancel?: () => void
}
export default function BusinessForm({ business, categories, timeZones, onSave, onCancel }: Props) {
  const [input, setInput] = useState<BusinessInput>({
    name: business?.name ?? '', description: business?.description ?? '',
    businessCategoryId: business?.category.id ?? '', address: business?.address ?? '',
    contactPhone: business?.contactPhone ?? '', contactEmail: business?.contactEmail ?? '',
    timeZoneId: business?.timeZoneId ?? 'Australia/Sydney',
  })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const change = (field: keyof BusinessInput, value: string) => setInput(current => ({ ...current, [field]: value }))
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setBusy(true)
    try { await onSave({ ...input, contactPhone: input.contactPhone?.trim() || null }) }
    catch (err) { setError(err instanceof Error ? err.message : 'Unable to save your business.') }
    finally { setBusy(false) }
  }
  return <form onSubmit={submit} className="panel p-4">
    <h2 className="h4 mb-4">{business ? 'Edit business details' : 'Create your business'}</h2>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    {categories.length === 0 && <div className="alert alert-warning">No categories are available yet. Ask the developer to run the category seed command.</div>}
    <fieldset disabled={busy}><div className="row g-3">
      <div className="col-12"><label className="form-label" htmlFor="business-name">Business name</label><input className="form-control" id="business-name" required maxLength={200} value={input.name} onChange={e => change('name', e.target.value)} /></div>
      <div className="col-12"><label className="form-label" htmlFor="business-description">Description</label><textarea className="form-control" id="business-description" required maxLength={2000} rows={4} value={input.description} onChange={e => change('description', e.target.value)} /></div>
      <div className="col-md-6"><label className="form-label" htmlFor="business-category">Category</label><select className="form-select" id="business-category" required value={input.businessCategoryId} onChange={e => change('businessCategoryId', e.target.value)}><option value="">Choose a category</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></div>
      <div className="col-md-6"><label className="form-label" htmlFor="business-timezone">Time zone</label><select className="form-select" id="business-timezone" required value={input.timeZoneId} onChange={e => change('timeZoneId', e.target.value)}>{timeZones.map(z => <option key={z.id} value={z.id}>{z.label}</option>)}</select></div>
      <div className="col-12"><label className="form-label" htmlFor="business-address">Address</label><input className="form-control" id="business-address" required maxLength={500} value={input.address} onChange={e => change('address', e.target.value)} /></div>
      <div className="col-md-6"><label className="form-label" htmlFor="business-email">Contact email</label><input className="form-control" type="email" id="business-email" required maxLength={254} value={input.contactEmail} onChange={e => change('contactEmail', e.target.value)} /></div>
      <div className="col-md-6"><label className="form-label" htmlFor="business-phone">Contact phone <span className="text-secondary fw-normal">(optional)</span></label><input className="form-control" type="tel" id="business-phone" maxLength={30} value={input.contactPhone ?? ''} onChange={e => change('contactPhone', e.target.value)} /></div>
    </div><div className="d-flex flex-wrap gap-2 mt-4"><button className="btn btn-primary" disabled={categories.length === 0 || timeZones.length === 0}>{busy ? 'Saving…' : business ? 'Save business' : 'Create business'}</button>{onCancel && <button className="btn btn-outline-secondary" type="button" onClick={onCancel}>Cancel</button>}</div></fieldset>
  </form>
}
