import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { browseBusinesses, getCategories, type BusinessPage, type Category } from '../api/businesses'

export default function BrowsePage() {
  const [params, setParams] = useSearchParams()
  const search = params.get('search') ?? ''
  const categoryId = params.get('categoryId') ?? ''
  const requestedPage = Number(params.get('page') ?? '1')
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 100000 ? requestedPage : 1
  const [draft, setDraft] = useState(search)
  const [categoryDraft, setCategoryDraft] = useState(categoryId)
  const [categories, setCategories] = useState<Category[]>([])
  const [result, setResult] = useState<BusinessPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  useEffect(() => { setDraft(search); setCategoryDraft(categoryId) }, [search, categoryId])
  useEffect(() => {
    let active = true
    setLoading(true); setError(''); setResult(null)
    const query = new URLSearchParams({ page: String(page), pageSize: '12' })
    if (search) query.set('search', search)
    if (categoryId) query.set('categoryId', categoryId)
    Promise.all([browseBusinesses(query), getCategories()]).then(([businesses, cats]) => {
      if (active) { setResult(businesses); setCategories(cats) }
    }).catch(err => { if (active) setError(err instanceof Error ? err.message : 'Unable to load businesses.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [search, categoryId, page, retry])
  function filter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const query = new URLSearchParams()
    if (draft.trim()) query.set('search', draft.trim())
    if (categoryDraft) query.set('categoryId', categoryDraft)
    setParams(query)
  }
  function goToPage(next: number) { const query = new URLSearchParams(params); query.set('page', String(next)); setParams(query) }
  const pageCount = result ? Math.ceil(result.totalCount / result.pageSize) : 0
  return <section className="workspace-page">
    <div className="discovery-intro"><p className="eyebrow">DISCOVER SERVICEHUB</p><h1>Find your next<br /><span>local favourite.</span></h1><p className="text-secondary">Explore independent businesses and the services they offer.</p><span className="small text-secondary">Choose a service and book an available time.</span></div>
    <form className="panel search-panel row g-3 align-items-end" onSubmit={filter}>
      <div className="col-md-6"><label htmlFor="business-search" className="form-label">Search businesses</label><input id="business-search" className="form-control" type="search" placeholder="Name, description or address" maxLength={100} value={draft} onChange={e => setDraft(e.target.value)} /></div>
      <div className="col-md-4"><label htmlFor="category-filter" className="form-label">Category</label><select id="category-filter" className="form-select" value={categoryDraft} onChange={e => setCategoryDraft(e.target.value)}><option value="">All categories</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></div>
      <div className="col-md-2"><button className="btn btn-primary w-100">Search</button></div>
    </form>
    {loading ? <p role="status" className="mt-4">Finding businesses…</p> : error ? <div className="alert alert-danger mt-4" role="alert">{error}<button className="btn btn-link" onClick={() => setRetry(value => value + 1)}>Retry</button></div> : result && <>
      <div className="d-flex flex-wrap justify-content-between gap-2 my-4"><p className="mb-0 text-secondary" role="status">{result.totalCount} {result.totalCount === 1 ? 'business' : 'businesses'} found</p>{(search || categoryId || page > 1) && <button className="btn btn-link p-0" onClick={() => setParams({})}>Clear filters</button>}</div>
      {result.items.length === 0 ? <div className="empty-state"><h2 className="h4">No businesses to show yet.</h2><p className="text-secondary mb-0">Try a different search or check back as more businesses join ServiceHub.</p></div> : <div className="row g-4">{result.items.map(business => <div className="col-md-6 col-lg-4" key={business.id}><article className="business-card h-100">
        <div className="business-card-top"><span className="business-monogram" aria-hidden="true">{business.name.charAt(0).toUpperCase()}</span><span className="badge">{business.category.name}</span></div>
        <h2 className="h4">{business.name}</h2><p className="text-secondary business-summary">{business.description}</p><p className="small text-secondary">{business.address}</p><p className="small">{business.activeServiceCount} active {business.activeServiceCount === 1 ? 'service' : 'services'}</p><Link className="btn btn-outline-primary mt-auto" to={`/businesses/${business.id}`} aria-label={`View ${business.name}`}>View business <span aria-hidden="true">↗</span></Link>
      </article></div>)}</div>}
      {pageCount > 1 && <nav aria-label="Business results pages" className="d-flex justify-content-center align-items-center gap-3 mt-4"><button className="btn btn-outline-primary" disabled={page <= 1} onClick={() => goToPage(page - 1)}>Previous</button><span className="small">Page {page} of {pageCount}</span><button className="btn btn-outline-primary" disabled={page >= pageCount} onClick={() => goToPage(page + 1)}>Next</button></nav>}
    </>}
  </section>
}
