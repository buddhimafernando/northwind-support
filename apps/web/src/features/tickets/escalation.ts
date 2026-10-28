import type { TicketDetail } from '../../types/api';

/** The same limits the API enforces (TicketEscalation in the domain). */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;
export const ESCALATED_BY_MAX_LENGTH = 100;

export interface EscalationForm {
  reason: string;
  escalatedBy: string;
}

export type EscalationErrors = Partial<Record<keyof EscalationForm, string>>;

/** Checks the form the way the server will, so invalid input never reaches the API. */
export function validateEscalation(form: EscalationForm): EscalationErrors {
  const errors: EscalationErrors = {};
  const reason = form.reason.trim();
  const escalatedBy = form.escalatedBy.trim();

  if (reason.length < REASON_MIN_LENGTH || reason.length > REASON_MAX_LENGTH) {
    errors.reason = `Reason must be between ${REASON_MIN_LENGTH} and ${REASON_MAX_LENGTH} characters.`;
  }

  if (escalatedBy.length === 0) {
    errors.escalatedBy = 'Say who is escalating the ticket.';
  } else if (escalatedBy.length > ESCALATED_BY_MAX_LENGTH) {
    errors.escalatedBy = `Name must be ${ESCALATED_BY_MAX_LENGTH} characters or fewer.`;
  }

  return errors;
}

/** Why this ticket cannot be escalated, or undefined when it can. */
export function whyNotEscalatable(ticket: TicketDetail): string | undefined {
  if (ticket.status === 'Resolved' || ticket.status === 'Closed') {
    return `This ticket is ${ticket.status.toLowerCase()}, so it cannot be escalated.`;
  }

  if (ticket.priority === 'Critical') {
    return 'This ticket is already Critical, the highest priority.';
  }

  return undefined;
}
