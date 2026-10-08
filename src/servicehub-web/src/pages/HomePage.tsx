import { Link } from 'react-router-dom'

export default function HomePage() {
  return (
    <>
      <section className="hero row align-items-center g-5">
        <div className="col-lg-7">
          <p className="eyebrow">A little less planning. A little more living.</p>
          <h1>Your next appointment,<br /><span>simplified.</span></h1>
          <p className="hero-description">A home for the services you need and the businesses you trust. We’re building a simpler way to discover, book, and manage your appointments.</p>
          <Link className="btn btn-primary btn-lg" to="/browse">Explore ServiceHub <span aria-hidden="true">↗</span></Link>
          <p className="mt-3 small text-secondary">Discover businesses · Book your next appointment</p>
        </div>
        <div className="col-lg-5">
          <div className="intro-panel">
            <span className="panel-label">MADE FOR EVERYDAY LIFE</span>
            <div className="illustration" aria-hidden="true"><span>SH</span></div>
            <h2>Make room for what matters.</h2>
            <p>Less back-and-forth. More time for you.</p>
            <span className="badge rounded-pill">One place. More possibilities.</span>
          </div>
        </div>
      </section>
      <section className="features" aria-labelledby="features-heading">
        <div className="d-flex flex-wrap justify-content-between align-items-end gap-2 mb-4">
          <div><p className="eyebrow">THE VISION</p><h2 id="features-heading">Every step, a little easier.</h2></div>
          <span className="small text-secondary">Discover, book and manage appointments</span>
        </div>
        <div className="row g-4">
          {[
            ['01', 'Find your fit', 'Discover local businesses and explore the services they offer.'],
            ['02', 'Choose your moment', 'See available times and find an appointment that fits your day.'],
            ['03', 'Stay organised', 'Keep your upcoming appointments together in one place.'],
          ].map(([number, title, description]) => (
            <div className="col-md-4" key={number}><article className="feature-card h-100">
              <span className="feature-number">{number}</span><h3>{title}</h3><p>{description}</p>
            </article></div>
          ))}
        </div>
      </section>
    </>
  )
}
