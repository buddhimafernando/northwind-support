import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession } from './useSession';

/**
 * Guards every route below it. Without a session the visitor is sent to sign in, carrying
 * the page they wanted so they can be returned to it. Because this follows the session, it
 * also fires when a 401 clears the session mid-visit.
 */
export function RequireAuth() {
  const session = useSession();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
}
