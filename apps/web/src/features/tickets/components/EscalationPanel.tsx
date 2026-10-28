import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { useSession } from '../../../auth/useSession';
import { Field } from '../../../components/Field';
import type { EscalationResult, TicketDetail } from '../../../types/api';
import { REASON_MAX_LENGTH, REASON_MIN_LENGTH, validateReason, whyNotEscalatable } from '../escalation';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (result: EscalationResult) => void;
}

/** Raises the ticket's priority one level, with a reason. Recorded against the signed-in agent. */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const session = useSession();
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState<string>();
  const [isPending, setIsPending] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  const blocked = whyNotEscalatable(ticket);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationError = validateReason(reason);
    setReasonError(validationError);
    setSubmitError(undefined);

    if (validationError) {
      return;
    }

    setIsPending(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, { reason: reason.trim() });
      setReason('');
      onEscalated(result);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setReasonError(caught.fieldErrors.Reason?.[0]);
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
          {session && ` Recorded as escalated by ${session.agent.fullName}.`}
        </p>
      )}

      <Field
        id="escalate-reason"
        label="Reason"
        error={reasonError}
        hint={`${REASON_MIN_LENGTH} to ${REASON_MAX_LENGTH} characters.`}
      >
        {(fieldProps) => (
          <textarea
            {...fieldProps}
            rows={3}
            maxLength={REASON_MAX_LENGTH}
            value={reason}
            disabled={Boolean(blocked) || isPending}
            onChange={(event) => setReason(event.target.value)}
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
