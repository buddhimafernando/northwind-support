import { useSyncExternalStore } from 'react';
import { getSession, subscribe, type Session } from './session';

/** The current session; re-renders whenever someone signs in or out. */
export function useSession(): Session | null {
  return useSyncExternalStore(subscribe, getSession);
}
