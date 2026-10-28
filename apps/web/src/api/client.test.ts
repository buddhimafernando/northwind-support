import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession } from '../auth/session';
import { signIn } from '../test/session';
import { ApiError, request } from './client';

const fetchMock = vi.fn<typeof fetch>();

function respond(status: number, body: unknown = {}) {
  fetchMock.mockResolvedValue(new Response(JSON.stringify(body), { status }));
}

describe('request', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    clearSession();
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('attaches the signed-in agent token to every call', async () => {
    signIn({ accessToken: 'the-token' });
    respond(200, []);

    await request('/tickets');

    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers);
    expect(headers.get('Authorization')).toBe('Bearer the-token');
  });

  it('sends no Authorization header when nobody is signed in', async () => {
    respond(200, []);

    await request('/auth/login', { method: 'POST' });

    const headers = new Headers(fetchMock.mock.calls[0][1]?.headers);
    expect(headers.has('Authorization')).toBe(false);
  });

  it('clears the session when the API answers 401', async () => {
    signIn();
    respond(401, { title: 'Unauthorized' });

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getSession()).toBeNull();
  });

  it('does not sign out a newer session when an older request comes back 401', async () => {
    signIn({ accessToken: 'old-token' });
    let answer: (response: Response) => void = () => undefined;
    fetchMock.mockReturnValue(new Promise<Response>((resolve) => (answer = resolve)));

    const pending = request('/tickets');
    const newer = signIn({ accessToken: 'new-token' });
    answer(new Response('{}', { status: 401 }));

    await expect(pending).rejects.toBeInstanceOf(ApiError);
    expect(getSession()).toBe(newer);
  });
});
