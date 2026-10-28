import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../api/auth';
import { LoginPage } from '../features/auth/LoginPage';
import { signIn } from '../test/session';
import { RequireAuth } from './RequireAuth';
import { clearSession } from './session';

vi.mock('../api/auth', () => ({ authApi: { login: vi.fn() } }));

function renderAt(path: string) {
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RequireAuth />,
        children: [
          { path: '/tickets', element: <h1>Ticket list</h1> },
          { path: '/tickets/:id', element: <h1>Ticket 12</h1> },
        ],
      },
    ],
    { initialEntries: [path] },
  );

  render(<RouterProvider router={router} />);

  return router;
}

describe('RequireAuth', () => {
  afterEach(() => {
    clearSession();
    vi.clearAllMocks();
  });

  it('sends a visitor to sign in, then back to the page they wanted', async () => {
    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 'token',
      expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
      agent: { id: 3, fullName: 'Priya Nair', email: 'priya.nair@northwind-support.example' },
    });
    const router = renderAt('/tickets/12?tab=history');
    const user = userEvent.setup();

    expect(await screen.findByRole('heading', { name: 'Sign in to Northwind Support' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');

    await user.type(screen.getByLabelText('Email'), 'priya.nair@northwind-support.example');
    await user.type(screen.getByLabelText('Password'), 'Test_password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Ticket 12' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets/12');
    expect(router.state.location.search).toBe('?tab=history');
  });

  it('lets a signed-in agent straight through', async () => {
    signIn();

    renderAt('/tickets');

    expect(await screen.findByRole('heading', { name: 'Ticket list' })).toBeInTheDocument();
  });

  it('sends the agent to sign in as soon as the session is cleared, e.g. by a 401', async () => {
    signIn();
    const router = renderAt('/tickets/12');
    await screen.findByRole('heading', { name: 'Ticket 12' });

    act(() => clearSession());

    expect(await screen.findByRole('heading', { name: 'Sign in to Northwind Support' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });
});
