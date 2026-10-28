import { useEffect } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { authApi } from './api/auth';
import { clearSession } from './auth/session';
import { useSession } from './auth/useSession';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const session = useSession();

  // Confirms the stored session with the server once per visit. A 401 (expired token, or an
  // agent deactivated since signing in) has already signed the user out in the API client.
  useEffect(() => {
    authApi.me().catch(() => undefined);
  }, []);

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {session && (
          <div className="app__user">
            <span>
              Signed in as <strong>{session.agent.fullName}</strong>
            </span>
            <button type="button" className="button button--ghost" onClick={clearSession}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
