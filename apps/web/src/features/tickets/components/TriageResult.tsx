import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

/** What triage decided for a ticket that was just raised, and why. */
export function TriageResult({ ticket }: { ticket: TicketDetail }) {
  const { triage } = ticket;

  return (
    <dl className="triage">
      <div>
        <dt>Priority</dt>
        <dd>
          <PriorityBadge priority={ticket.priority} /> {triage?.priorityReason}
        </dd>
      </div>

      <div>
        <dt>Due</dt>
        <dd>
          {formatDateTime(ticket.dueAtUtc)}. {triage?.slaReason}
        </dd>
      </div>

      <div>
        <dt>Assigned to</dt>
        <dd>
          {ticket.assignedAgent ? (
            <>
              {ticket.assignedAgent.fullName}. {triage?.assignmentReason}
            </>
          ) : (
            <>Unassigned. {triage?.assignmentReason}</>
          )}
        </dd>
      </div>
    </dl>
  );
}
