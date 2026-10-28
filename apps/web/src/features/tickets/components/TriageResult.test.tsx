import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ticketDetail } from '../../../test/fixtures';
import { TriageResult } from './TriageResult';

const triage = {
  priorityReason: 'Critical because every Outage ticket is treated as critical.',
  slaReason: '2 h window for a Critical ticket from a Premium customer.',
  assignmentReason: 'Assigned to Ben Osei, who had the fewest open tickets (3 of 8) among eligible agents.',
};

describe('TriageResult', () => {
  it('shows the applied priority, due date and owner with the reasons', () => {
    render(
      <TriageResult
        ticket={ticketDetail({ priority: 'Critical', assignedAgent: { id: 2, fullName: 'Ben Osei' }, triage })}
      />,
    );

    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText(/every Outage ticket is treated as critical/)).toBeInTheDocument();
    expect(screen.getByText(/2 h window/)).toBeInTheDocument();
    expect(screen.getByText(/Ben Osei\. Assigned to Ben Osei/)).toBeInTheDocument();
  });

  it('explains an unassigned ticket', () => {
    const unassignedTriage = {
      ...triage,
      assignmentReason: 'Left unassigned because every active Billing specialist is at their open-ticket limit.',
    };

    render(<TriageResult ticket={ticketDetail({ assignedAgent: null, triage: unassignedTriage })} />);

    expect(
      screen.getByText('Unassigned. Left unassigned because every active Billing specialist is at their open-ticket limit.'),
    ).toBeInTheDocument();
  });
});
