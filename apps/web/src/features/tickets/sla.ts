import type { BadgeTone } from '../../components/Badge';
import type { SlaStatus } from '../../types/api';

/**
 * How each SLA state is shown. Every state has its own words and symbol as well as its own
 * colour, so none of them relies on colour alone.
 */
export const slaStates: Record<SlaStatus, { label: string; symbol: string; tone: BadgeTone }> = {
  WithinSla: { label: 'Within SLA', symbol: '◷', tone: 'info' },
  AtRisk: { label: 'At risk', symbol: '!', tone: 'warning' },
  Breached: { label: 'Breached', symbol: '✕', tone: 'danger' },
  Met: { label: 'Met', symbol: '✓', tone: 'success' },
  NotApplicable: { label: 'No SLA', symbol: '–', tone: 'neutral' },
};
