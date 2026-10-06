import { Link } from 'react-router-dom'

export default function PlaceholderPage({ title, description }: { title: string; description: string }) {
  return (
    <section className="placeholder-page mx-auto">
      <span className="badge rounded-pill mb-4">Coming in a later phase</span>
      <h1>{title}</h1><p className="lead text-secondary mt-3">{description}</p>
      <p>This page is a preview of what’s next. This feature is not available yet.</p>
      <Link className="btn btn-primary mt-3" to="/">Back to home</Link>
    </section>
  )
}
