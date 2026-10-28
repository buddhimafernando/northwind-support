import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketDetail } from '../../../test/fixtures';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

async function fillIn(reason: string, escalatedBy = 'Team lead') {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Reason'), reason);
  await user.type(screen.getByLabelText('Escalated by'), escalatedBy);
  await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));
}

describe('EscalationPanel', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('blocks a reason that is too short without calling the API', async () => {
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={vi.fn()} />);

    // Five characters with the padding, but only four once trimmed - as the server counts.
    await fillIn('  abcd ');

    expect(screen.getByText('Reason must be between 5 and 500 characters.')).toBeInTheDocument();
    expect(screen.getByLabelText('Reason')).toHaveAttribute('aria-invalid', 'true');
    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 from the server and leaves the ticket unchanged', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
    );
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillIn('Customer is losing sales');

    expect(await screen.findByRole('alert')).toHaveTextContent('already Critical');
    expect(onEscalated).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Reason')).toHaveValue('Customer is losing sales');
  });

  it('sends the trimmed form and hands back the result', async () => {
    const result = {
      ticket: ticketDetail({ priority: 'Critical' }),
      escalation: {
        id: 1,
        fromPriority: 'High' as const,
        toPriority: 'Critical' as const,
        fromAgent: null,
        toAgent: null,
        fromDueAtUtc: null,
        toDueAtUtc: '2026-09-25T14:03:00Z',
        reason: 'Customer is losing sales',
        escalatedBy: 'Team lead',
        escalatedAtUtc: '2026-09-25T10:03:00Z',
      },
    };
    escalate.mockResolvedValue(result);
    const onEscalated = vi.fn();
    render(<EscalationPanel ticket={ticketDetail()} onEscalated={onEscalated} />);

    await fillIn('  Customer is losing sales  ');

    expect(escalate).toHaveBeenCalledWith(12, { reason: 'Customer is losing sales', escalatedBy: 'Team lead' });
    expect(onEscalated).toHaveBeenCalledWith(result);
  });

  it.each([
    [{ priority: 'Critical' as const }, 'already Critical'],
    [{ status: 'Resolved' as const }, 'resolved'],
    [{ status: 'Closed' as const }, 'closed'],
  ])('is disabled, and says why, for a ticket that cannot be escalated', (overrides, why) => {
    render(<EscalationPanel ticket={ticketDetail(overrides)} onEscalated={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeDisabled();
    expect(screen.getByText(new RegExp(why))).toBeInTheDocument();
  });
});
