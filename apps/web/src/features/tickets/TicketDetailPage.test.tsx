import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { agentsApi } from '../../api/agents';
import { categoriesApi } from '../../api/categories';
import { ApiError } from '../../api/client';
import { customersApi } from '../../api/customers';
import { ticketsApi } from '../../api/tickets';
import { ticketDetail } from '../../test/fixtures';
import { TicketDetailPage } from './TicketDetailPage';

vi.mock('../../api/tickets', () => ({
  ticketsApi: { getTicket: vi.fn(), getEscalations: vi.fn(), escalate: vi.fn() },
}));
vi.mock('../../api/agents', () => ({ agentsApi: { getAgents: vi.fn() } }));
vi.mock('../../api/categories', () => ({ categoriesApi: { getCategories: vi.fn() } }));
vi.mock('../../api/customers', () => ({ customersApi: { getCustomers: vi.fn() } }));

describe('TicketDetailPage', () => {
  beforeEach(() => {
    vi.mocked(ticketsApi.getTicket).mockResolvedValue(ticketDetail({ priority: 'High' }));
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
    vi.mocked(agentsApi.getAgents).mockResolvedValue([]);
    vi.mocked(categoriesApi.getCategories).mockResolvedValue([]);
    vi.mocked(customersApi.getCustomers).mockResolvedValue([]);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('shows a 409 from the server and keeps the ticket as it was', async () => {
    vi.mocked(ticketsApi.escalate).mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0012 is already Critical and cannot be escalated further.'),
    );
    const user = userEvent.setup();

    render(
      <MemoryRouter initialEntries={['/tickets/12']}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await user.type(await screen.findByLabelText('Reason'), 'Customer is losing sales');
    await user.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('already Critical');

    // Still the ticket that was loaded: same priority, not re-fetched, history untouched.
    const priority = screen.getByText('Priority', { selector: 'dt' }).nextElementSibling as HTMLElement;
    expect(within(priority).getByText('High')).toBeInTheDocument();
    expect(ticketsApi.getTicket).toHaveBeenCalledTimes(1);
    expect(ticketsApi.getEscalations).toHaveBeenCalledTimes(1);
  });
});
