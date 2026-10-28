import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import {
  ESCALATED_BY_MAX_LENGTH,
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  validateEscalation,
  whyNotEscalatable,
  type EscalationErrors,
  type EscalationForm,
} from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

const emptyForm: EscalationForm = { reason: '', escalatedBy: '' };

/** Raises the ticket's priority one level, with a reason. */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [form, setForm] = useState<EscalationForm>(emptyForm);
  const [errors, setErrors] = useState<EscalationErrors>({});
  const [isPending, setIsPending] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const blocked = whyNotEscalatable(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validateEscalation(form);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsPending(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, {
        reason: form.reason.trim(),
        escalatedBy: form.escalatedBy.trim(),
      });
      setForm((current) => ({ ...current, reason: '' }));
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setErrors(fromApiErrors(caught.fieldErrors));
      }

      setSubmitError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsPending(false);
    }
  };

  return (
    <form className="card form" onSubmit={submit} noValidate aria-labelledby="escalate-heading">
      <h2 id="escalate-heading">Escalate</h2>

      {blocked ? (
        <p className="field__hint">{blocked}</p>
      ) : (
        <p className="field__hint">
          Raises the priority from {ticket.priority} by one level and restarts the SLA from now.
        </p>
      )}

      <Field
        id="escalate-reason"
        label="Reason"
        error={errors.reason}
        hint={`${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            maxLength={REASON_MAX_LENGTH}
            value={form.reason}
            disabled={Boolean(blocked) || isPending}
            onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))}
          />
        )}
      </Field>

      <Field id="escalate-by" label="Escalated by" error={errors.escalatedBy}>
        {(fieldProps) => (
          <input
            {...fieldProps}
            type="text"
            maxLength={ESCALATED_BY_MAX_LENGTH}
            value={form.escalatedBy}
            disabled={Boolean(blocked) || isPending}
            onChange={(event) => setForm((current) => ({ ...current, escalatedBy: event.target.value }))}
          />
        )}
      </Field>

      {submitError && (
        <p className="field__error" role="alert">
          {submitError}
        </p>
      )}

      <div className="button-row">
        <button type="submit" className="button button--primary" disabled={Boolean(blocked) || isPending}>
          {isPending ? 'Escalating...' : 'Escalate ticket'}
        </button>
      </div>
    </form>
  );
}

/** The API names fields in PascalCase ("Reason"); the form uses camelCase. */
function fromApiErrors(fieldErrors: Record<string, string[]>): EscalationErrors {
  const errors: EscalationErrors = {};

  for (const [field, messages] of Object.entries(fieldErrors)) {
    const key = field.charAt(0).toLowerCase() + field.slice(1);

    if (key === 'reason' || key === 'escalatedBy') {
      errors[key] = messages[0];
    }
  }

  return errors;
}
