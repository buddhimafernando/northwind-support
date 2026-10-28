import { EmptyState } from '../../../components/EmptyState';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { AgentSummary, TicketEscalation } from '../../../types/api';

interface EscalationHistoryProps {
  escalations?: TicketEscalation[];
  isLoading: boolean;
  error?: string;
  onRetry: () => void;
}

/** Every escalation of a ticket, newest first: what changed, who did it and why. */
export function EscalationHistory({ escalations, isLoading, error, onRetry }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation history</h2>

      {error && <ErrorMessage message={error} onRetry={onRetry} />}

      {isLoading && !escalations && <Spinner label="Loading escalation history" />}

      {escalations && escalations.length === 0 && <EmptyState title="Never escalated" />}

      {escalations && escalations.length > 0 && (
        <ol className="history" aria-busy={isLoading}>
          {escalations.map((escalation) => (
            <li key={escalation.id} className="history__item">
              <p className="history__meta">
                <strong>{escalation.escalatedBy}</strong> on {formatDateTime(escalation.escalatedAtUtc)}
              </p>
              <p className="history__reason">{escalation.reason}</p>
              <dl className="history__changes">
                <Change label="Priority" from={escalation.fromPriority} to={escalation.toPriority} />
                <Change label="Agent" from={agentName(escalation.fromAgent)} to={agentName(escalation.toAgent)} />
                <Change
                  label="Due"
                  from={formatDateTime(escalation.fromDueAtUtc)}
                  to={formatDateTime(escalation.toDueAtUtc)}
                />
              </dl>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

function Change({ label, from, to }: { label: string; from: string; to: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{from === to ? `${to} (unchanged)` : `${from} → ${to}`}</dd>
    </div>
  );
}

function agentName(agent: AgentSummary | null): string {
  return agent?.fullName ?? 'Unassigned';
}
