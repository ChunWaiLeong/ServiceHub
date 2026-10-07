import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getBusiness, type Business } from '../api/businesses'
import { getPublicServices, type BusinessService } from '../api/services'
import { ApiError } from '../api/client'

export default function BusinessDetailPage() {
  const { id } = useParams()
  const [business, setBusiness] = useState<Business | null>(null)
  const [services, setServices] = useState<BusinessService[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let active = true
    setLoading(true); setError(''); setBusiness(null)
    if (!id) return
    Promise.all([getBusiness(id), getPublicServices(id)]).then(([profile, items]) => {
      if (active) { setBusiness(profile); setServices(items) }
    }).catch(err => {
      if (active) setError(err instanceof ApiError && err.status === 404 ? 'This business is unavailable.' : err instanceof Error ? err.message : 'Unable to load this business.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [id, retry])
  return <section className="workspace-page">
    <Link className="back-link" to="/browse">← All businesses</Link>
    {loading ? <p role="status" className="mt-4">Loading business…</p> : error ? <div className="alert alert-danger mt-4" role="alert">{error}<button className="btn btn-link" onClick={() => setRetry(value => value + 1)}>Retry</button></div> : business && <>
      <div className="profile-heading"><p className="eyebrow">{business.category.name}</p><h1>{business.name}</h1><p className="profile-description preserve-lines">{business.description}</p></div>
      <div className="row g-4"><div className="col-lg-8"><div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-4"><h2 className="mb-0">Our services</h2><span className="badge">Online booking coming soon</span></div>
        {services.length === 0 ? <div className="empty-state"><h3 className="h5">Services are on their way.</h3><p className="text-secondary mb-0">This business hasn't published any services yet.</p></div> : <div className="d-grid gap-3">{services.map(service => <article className="panel service-card" key={service.id}><div className="d-flex flex-wrap justify-content-between gap-2"><h3 className="h5">{service.name}</h3><strong>{service.price.toLocaleString('en-AU', { style: 'currency', currency: 'AUD' })} AUD</strong></div><p className="text-secondary preserve-lines">{service.description}</p><span className="small text-secondary">{service.durationMinutes} minutes</span></article>)}</div>}
      </div><aside className="col-lg-4"><div className="panel p-4 contact-panel"><h2 className="h5 mb-4">Visit & contact</h2><dl className="mb-0"><dt>Address</dt><dd>{business.address}</dd><dt>Email</dt><dd>{business.contactEmail}</dd>{business.contactPhone && <><dt>Phone</dt><dd>{business.contactPhone}</dd></>}<dt>Time zone</dt><dd className="mb-0">{business.timeZoneId}</dd></dl></div></aside></div>
    </>}
  </section>
}
