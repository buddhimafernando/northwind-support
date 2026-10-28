import { useState } from 'react';
import { Navigate, useLocation, type Location } from 'react-router-dom';
import { authApi } from '../../api/auth';
import { toErrorMessage } from '../../api/client';
import { setSession } from '../../auth/session';
import { useSession } from '../../auth/useSession';
import { Field } from '../../components/Field';

interface LoginForm {
  email: string;
  password: string;
}

type LoginErrors = Partial<Record<keyof LoginForm, string>>;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function LoginPage() {
  const session = useSession();
  const location = useLocation();
  const [form, setForm] = useState<LoginForm>({ email: '', password: '' });
  const [errors, setErrors] = useState<LoginErrors>({});
  const [isPending, setIsPending] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  // Signing in (or arriving already signed in) goes back to the page that sent us here.
  if (session) {
    return <Navigate to={returnPath(location)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsPending(true);

    try {
      setSession(await authApi.login({ email: form.email.trim(), password: form.password }));
    } catch (caught) {
      // The API gives the same message for a wrong email and a wrong password.
      setSubmitError(toErrorMessage(caught, 'Could not sign in.'));
      setIsPending(false);
    }
  };

  return (
    <main className="login">
      <form className="card form login__card" onSubmit={submit} noValidate aria-labelledby="login-heading">
        <h1 id="login-heading">Sign in to Northwind Support</h1>

        <Field id="login-email" label="Email" error={errors.email}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="email"
              autoComplete="username"
              value={form.email}
              disabled={isPending}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="password"
              autoComplete="current-password"
              value={form.password}
              disabled={isPending}
              onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
            />
          )}
        </Field>

        {submitError && (
          <p className="field__error" role="alert">
            {submitError}
          </p>
        )}

        <button type="submit" className="button button--primary" disabled={isPending}>
          {isPending ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}

function validate(form: LoginForm): LoginErrors {
  const errors: LoginErrors = {};

  if (!form.email.trim()) {
    errors.email = 'Enter your email address.';
  } else if (!emailPattern.test(form.email.trim())) {
    errors.email = 'Enter a valid email address.';
  }

  if (!form.password) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

/** Where RequireAuth was sending the visitor, or the ticket list. */
function returnPath(location: Location): string {
  const from = (location.state as { from?: Location } | null)?.from;

  return from && from.pathname !== '/login' ? `${from.pathname}${from.search}${from.hash}` : '/tickets';
}
