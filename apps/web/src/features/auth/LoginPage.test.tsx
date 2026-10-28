import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { clearSession, getSession } from '../../auth/session';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));

const login = vi.mocked(authApi.login);

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/login']}>
      <LoginPage />
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  afterEach(() => {
    clearSession();
    vi.clearAllMocks();
  });

  it('labels its fields, and blocks invalid input without calling the API', async () => {
    renderPage();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Email'), 'not-an-email');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
    expect(login).not.toHaveBeenCalled();
  });

  it('shows the error for rejected credentials and stays signed out', async () => {
    login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    renderPage();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Email'), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
    expect(getSession()).toBeNull();
  });

  it('shows a pending state while signing in', async () => {
    login.mockReturnValue(new Promise(() => undefined));
    renderPage();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Email'), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText('Password'), 'Test_password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();
  });
});
