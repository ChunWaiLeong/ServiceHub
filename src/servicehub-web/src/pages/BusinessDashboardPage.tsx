import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import * as businesses from '../api/businesses'
import * as services from '../api/services'
import { ApiError } from '../api/client'
import BusinessForm from '../components/BusinessForm'
import ServiceForm from '../components/ServiceForm'
import BookingsPanel from '../components/BookingsPanel'
import OwnerAvailabilityPanel from '../components/OwnerAvailabilityPanel'

export default function BusinessDashboardPage() {
  const { token, logout } = useAuth()
  const [business, setBusiness] = useState<businesses.Business | null>(null)
  const [categories, setCategories] = useState<businesses.Category[]>([])
  const [timeZones, setTimeZones] = useState<businesses.BusinessTimeZone[]>([])
  const [items, setItems] = useState<services.BusinessService[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [retry, setRetry] = useState(0)
  const [editBusiness, setEditBusiness] = useState(false)
  // undefined = closed, null = create, existing service = edit.
  const [serviceEditor, setServiceEditor] = useState<services.BusinessService | null | undefined>(undefined)
  const [notice, setNotice] = useState('')
  const [actionError, setActionError] = useState('')
  const [statusPending, setStatusPending] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    if (!token) return
    setLoading(true); setLoadError('')
    async function load() {
      const [cats, zones, owned] = await Promise.all([businesses.getCategories(), businesses.getTimeZones(),
        businesses.getOwnerBusiness(token!).catch(error => {
          if (error instanceof ApiError && error.status === 404) return null
          throw error
        })])
      const ownedServices = owned ? await services.getOwnerServices(token!) : []
      if (active) { setCategories(cats); setTimeZones(zones); setBusiness(owned); setItems(ownedServices) }
    }
    load().catch(error => {
      if (!active) return
      if (error instanceof ApiError && error.status === 401) logout()
      else setLoadError(error instanceof Error ? error.message : 'Unable to load your dashboard.')
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [token, logout, retry])

  async function authenticatedAction<T>(action: () => Promise<T>): Promise<T> {
    try { return await action() }
    catch (error) { if (error instanceof ApiError && error.status === 401) logout(); throw error }
  }
  async function saveBusiness(input: businesses.BusinessInput) {
    if (!token) return
    const result = await authenticatedAction(() => business
      ? businesses.updateBusiness(business.id, input, token) : businesses.createBusiness(input, token))
    setBusiness(result); setEditBusiness(false); setNotice(business ? 'Business details updated.' : 'Your business profile is ready. Add your first service below.')
  }
  async function saveService(input: services.ServiceInput) {
    if (!token || !business) return
    const result = await authenticatedAction(() => serviceEditor
      ? services.updateService(business.id, serviceEditor.id, input, token) : services.createService(business.id, input, token))
    setItems(current => [...current.filter(s => s.id !== result.id), result].sort((a, b) => a.name.localeCompare(b.name)))
    setServiceEditor(undefined); setNotice(serviceEditor ? 'Service updated.' : 'Service created.'); setActionError('')
  }
  async function toggleStatus(service: services.BusinessService) {
    if (!token || !business) return
    setStatusPending(service.id); setActionError(''); setNotice('')
    try {
      const result = await authenticatedAction(() => services.setServiceStatus(business.id, service.id, !service.isActive, token))
      setItems(current => current.map(s => s.id === result.id ? result : s))
      setNotice(result.isActive ? 'Service activated.' : 'Service deactivated and hidden from your public profile.')
    } catch (error) { setActionError(error instanceof Error ? error.message : 'Unable to change service status.') }
    finally { setStatusPending(null) }
  }
  return <section className="workspace-page">
    <div className="page-heading"><div><p className="eyebrow">YOUR BUSINESS</p><h1>Business dashboard.</h1><p className="text-secondary">Build your profile. Make your services easy to discover.</p></div>{business && <Link className="btn btn-outline-primary" to={`/businesses/${business.id}`}>View public profile</Link>}</div>
    {loading ? <p role="status">Loading your business…</p> : loadError ? <div className="alert alert-danger" role="alert">{loadError}<button className="btn btn-link" onClick={() => setRetry(value => value + 1)}>Retry</button></div> : <>
      {notice && <div className="alert alert-success" role="status">{notice}</div>}
      {actionError && <div className="alert alert-danger" role="alert">{actionError}</div>}
      {!business || editBusiness ? <BusinessForm business={business ?? undefined} categories={categories} timeZones={timeZones} onSave={saveBusiness} onCancel={business ? () => setEditBusiness(false) : undefined} /> : <article className="panel p-4 mb-4">
        <div className="d-flex flex-wrap justify-content-between gap-3 mb-3"><div><span className="badge mb-2">{business.category.name}</span><h2>{business.name}</h2></div><button className="btn btn-outline-primary align-self-start" onClick={() => setEditBusiness(true)}>Edit business</button></div>
        <p className="preserve-lines">{business.description}</p><div className="row g-3 small text-secondary"><div className="col-md-6"><strong className="d-block text-body">Address</strong>{business.address}</div><div className="col-md-6"><strong className="d-block text-body">Contact</strong>{business.contactEmail}{business.contactPhone && <span className="d-block">{business.contactPhone}</span>}</div><div className="col-12">Time zone: {business.timeZoneId}</div></div>
      </article>}
      {business && <section aria-labelledby="services-heading" className="mt-5">
        <div className="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4"><div><p className="eyebrow mb-2">YOUR OFFERING</p><h2 id="services-heading">Services</h2><p className="text-secondary mb-0">Inactive services stay here and are hidden publicly.</p></div><button className="btn btn-primary" disabled={serviceEditor !== undefined} onClick={() => { setServiceEditor(null); setNotice('') }}>Add service</button></div>
        {serviceEditor !== undefined && <ServiceForm key={serviceEditor?.id ?? 'new'} service={serviceEditor ?? undefined} onSave={saveService} onCancel={() => setServiceEditor(undefined)} />}
        {items.length === 0 ? <div className="empty-state"><h3 className="h5">Your first service starts here.</h3><p className="text-secondary mb-0">Add a clear description, price and duration so customers know what you offer.</p></div> : <div className="row g-3">{items.map(service => <div className="col-lg-6" key={service.id}><article className="panel service-card h-100">
          <div className="d-flex justify-content-between align-items-start gap-3"><h3 className="h5">{service.name}</h3><span className={`badge ${service.isActive ? '' : 'inactive-badge'}`}>{service.isActive ? 'Active' : 'Inactive'}</span></div>
          <p className="text-secondary preserve-lines">{service.description}</p><p className="service-meta">{service.price.toLocaleString('en-AU', { style: 'currency', currency: 'AUD' })} AUD <span>·</span> {service.durationMinutes} minutes</p>
          <div className="d-flex flex-wrap gap-2"><button className="btn btn-outline-primary btn-sm" disabled={serviceEditor !== undefined || statusPending !== null} onClick={() => { setServiceEditor(service); setNotice('') }}>Edit {service.name}</button><button className="btn btn-outline-secondary btn-sm" disabled={statusPending !== null || serviceEditor !== undefined} onClick={() => toggleStatus(service)}>{statusPending === service.id ? 'Saving…' : `${service.isActive ? 'Deactivate' : 'Activate'} ${service.name}`}</button></div>
        </article></div>)}</div>}
      </section>}
      {business && <BookingsPanel owner />}
      {business && <OwnerAvailabilityPanel timeZoneId={business.timeZoneId} />}
    </>}
  </section>
}
