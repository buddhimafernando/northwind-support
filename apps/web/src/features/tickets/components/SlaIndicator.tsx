import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaStates } from '../sla';

interface SlaIndicatorProps {
  slaStatus: SlaStatus;
  dueAtUtc: string | null;
}

/** Where a ticket stands against its due date. Used by the ticket list and the ticket page. */
export function SlaIndicator({ slaStatus, dueAtUtc }: SlaIndicatorProps) {
  const state = slaStates[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={state.tone}>
        <span aria-hidden="true">{state.symbol} </span>
        {state.label}
      </Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
