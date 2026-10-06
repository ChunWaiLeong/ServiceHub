import { useState } from 'react'
import { Link, NavLink, Route, Routes } from 'react-router-dom'
import ApiStatus from './components/ApiStatus'
import HomePage from './pages/HomePage'
import PlaceholderPage from './pages/PlaceholderPage'

export default function App() {
  const [menuOpen, setMenuOpen] = useState(false)
  const closeMenu = () => setMenuOpen(false)

  return (
    <div className="app-shell">
      <a href="#main-content" className="visually-hidden-focusable skip-link">Skip to content</a>
      <header className="site-header">
        <nav className="navbar navbar-expand-md container" aria-label="Main navigation">
          <Link className="navbar-brand d-flex align-items-center gap-2" to="/" onClick={closeMenu}>
            <span className="brand-mark" aria-hidden="true">s<span>h</span></span>ServiceHub
          </Link>
          <button className="navbar-toggler" type="button" aria-label="Toggle navigation" aria-expanded={menuOpen}
            aria-controls="main-navigation" onClick={() => setMenuOpen(value => !value)}><span className="navbar-toggler-icon" /></button>
          <div className={`collapse navbar-collapse ${menuOpen ? 'show' : ''}`} id="main-navigation">
            <div className="navbar-nav ms-auto align-items-md-center gap-md-4">
              <NavLink className="nav-link" to="/browse" onClick={closeMenu}>Browse Services</NavLink>
              <NavLink className="nav-link" to="/login" onClick={closeMenu}>Login</NavLink>
              <NavLink className="btn btn-outline-primary" to="/register" onClick={closeMenu}>Register</NavLink>
            </div>
          </div>
        </nav>
      </header>
      <main className="container flex-grow-1" id="main-content">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/browse" element={<PlaceholderPage title="Discover your next favourite service." description="Business discovery and service browsing are on the way." />} />
          <Route path="/login" element={<PlaceholderPage title="Welcome back." description="Secure account access will arrive in the authentication phase." />} />
          <Route path="/register" element={<PlaceholderPage title="Your ServiceHub journey starts here." description="Customer and business owner registration is coming soon." />} />
          <Route path="*" element={<PlaceholderPage title="Page not found." description="We couldn’t find the page you’re looking for." />} />
        </Routes>
      </main>
      <footer className="site-footer"><div className="container d-flex flex-wrap justify-content-between gap-3">
        <span>ServiceHub <span className="text-secondary">· Thoughtfully connected.</span></span><ApiStatus />
      </div></footer>
    </div>
  )
}
