import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { TicketEscalation } from '../../../types/api';
import { EscalationHistory } from './EscalationHistory';

const escalation: TicketEscalation = {
  id: 1,
  fromPriority: 'Medium',
  toPriority: 'High',
  fromAgent: { id: 6, fullName: 'Yuki Tanaka' },
  toAgent: { id: 2, fullName: 'Ben Osei' },
  fromDueAtUtc: '2026-09-25T09:00:00Z',
  toDueAtUtc: '2026-09-24T18:00:00Z',
  reason: 'Customer is losing sales',
  escalatedBy: 'Team lead',
  escalatedAtUtc: '2026-09-24T10:00:00Z',
};

describe('EscalationHistory', () => {
  it('shows what changed, why, who and when', () => {
    render(<EscalationHistory escalations={[escalation]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Medium → High')).toBeInTheDocument();
    expect(screen.getByText('Yuki Tanaka → Ben Osei')).toBeInTheDocument();
    expect(screen.getByText('Customer is losing sales')).toBeInTheDocument();
    expect(screen.getByText('Team lead')).toBeInTheDocument();
    expect(screen.getByText('Due').nextElementSibling).toHaveTextContent(/^.+ → .+$/);
  });

  it('says when an owner was kept rather than showing a change', () => {
    render(
      <EscalationHistory
        escalations={[{ ...escalation, toAgent: escalation.fromAgent }]}
        isLoading={false}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByText('Yuki Tanaka (unchanged)')).toBeInTheDocument();
  });

  it('says so when the ticket was never escalated', () => {
    render(<EscalationHistory escalations={[]} isLoading={false} onRetry={vi.fn()} />);

    expect(screen.getByText('Never escalated')).toBeInTheDocument();
  });
});
