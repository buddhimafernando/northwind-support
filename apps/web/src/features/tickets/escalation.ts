import type { TicketDetail } from '../../types/api';

/** The same limits the API enforces (TicketEscalation in the domain). */
export const REASON_MIN_LENGTH = 5;
export const REASON_MAX_LENGTH = 500;

/** Checks the reason the way the server will, so invalid input never reaches the API. */
export function validateReason(reason: string): string | undefined {
  const length = reason.trim().length;

  return length < REASON_MIN_LENGTH || length > REASON_MAX_LENGTH
    ? `Reason must be between ${REASON_MIN_LENGTH} and ${REASON_MAX_LENGTH} characters.`
    : undefined;
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
