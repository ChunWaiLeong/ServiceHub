import { useEffect, useState } from 'react'
import { getHealth } from '../api/health'

export default function ApiStatus() {
  const [status, setStatus] = useState<'checking' | 'connected' | 'unavailable'>('checking')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    const timeout = window.setTimeout(() => controller.abort(), 8000)
    let active = true

    getHealth(controller.signal)
      .then(() => { if (active) setStatus('connected') })
      .catch(() => { if (active) setStatus('unavailable') })
      .finally(() => window.clearTimeout(timeout))

    return () => {
      active = false
      window.clearTimeout(timeout)
      controller.abort()
    }
  }, [attempt])

  return (
    <div className="api-status d-flex flex-wrap align-items-center gap-2" role="status" aria-live="polite">
      <span className={`status-dot ${status}`} aria-hidden="true" />
      <span>{status === 'connected' ? 'API connected' : status === 'checking' ? 'Checking API connection…' : 'API unavailable'}</span>
      {status === 'unavailable' && <button className="btn btn-link btn-sm p-0" onClick={() => {
        setStatus('checking')
        setAttempt(value => value + 1)
      }}>Retry connection</button>}
    </div>
  )
}
