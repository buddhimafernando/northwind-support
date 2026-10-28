import type { LoginResponse } from '../types/api';

/** The signed-in agent's access token, when it expires, and who they are. */
export type Session = LoginResponse;

/**
 * Kept in sessionStorage: it survives a page reload but not closing the tab, and is never
 * shared with other tabs. Like any storage readable from script, a cross-site scripting bug
 * could read it - see "Authentication" in the README for the trade-off.
 */
const storageKey = 'supportdesk.session';

const listeners = new Set<() => void>();

let current: Session | null = load();

/** The current session, or null when nobody is signed in. Stable between changes. */
export function getSession(): Session | null {
  return current;
}

export function setSession(session: Session): void {
  current = session;
  write(session);
  notify();
}

/** Signs out locally. Every subscriber - and so every protected route - reacts. */
export function clearSession(): void {
  if (current === null) {
    return;
  }

  current = null;
  write(null);
  notify();
}

/** For React's useSyncExternalStore. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener);

  return () => listeners.delete(listener);
}

function notify(): void {
  listeners.forEach((listener) => listener());
}

function load(): Session | null {
  try {
    const raw = window.sessionStorage.getItem(storageKey);
    const session = raw ? (JSON.parse(raw) as Session) : null;

    // An expired token would only be refused; start signed out instead.
    return session && new Date(session.expiresAtUtc).getTime() > Date.now() ? session : null;
  } catch {
    return null;
  }
}

function write(session: Session | null): void {
  try {
    if (session) {
      window.sessionStorage.setItem(storageKey, JSON.stringify(session));
    } else {
      window.sessionStorage.removeItem(storageKey);
    }
  } catch {
    // Storage can be unavailable (private mode, blocked site data); the session still lives
    // in memory until the page is reloaded.
  }
}
