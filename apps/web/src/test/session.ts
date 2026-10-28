import { setSession } from '../auth/session';
import type { LoginResponse } from '../types/api';

/** Signs a test in as Priya Nair, with a token that is valid for an hour. */
export function signIn(overrides: Partial<LoginResponse> = {}): LoginResponse {
  const session: LoginResponse = {
    accessToken: 'header.payload.signature',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    agent: { id: 3, fullName: 'Priya Nair', email: 'priya.nair@northwind-support.example' },
    ...overrides,
  };

  setSession(session);

  return session;
}
