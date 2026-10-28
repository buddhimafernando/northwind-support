import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

const expected: [SlaStatus, string][] = [
  ['WithinSla', 'Within SLA'],
  ['AtRisk', 'At risk'],
  ['Breached', 'Breached'],
  ['Met', 'Met'],
  ['NotApplicable', 'No SLA'],
];

describe('SlaIndicator', () => {
  it.each(expected)('names the %s state in words', (status, label) => {
    render(<SlaIndicator slaStatus={status} dueAtUtc="2026-09-24T16:12:00Z" />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('gives every state its own text, symbol and colour', () => {
    const rendered = expected.map(([status]) => {
      const { container, unmount } = render(<SlaIndicator slaStatus={status} dueAtUtc={null} />);
      const badge = container.querySelector('.badge');
      const result = { text: badge?.textContent, className: badge?.className };
      unmount();
      return result;
    });

    // Distinct even with colour ignored: the text (label and symbol) differs for every state.
    expect(new Set(rendered.map((r) => r.text)).size).toBe(expected.length);
    expect(new Set(rendered.map((r) => r.className)).size).toBe(expected.length);
  });
});
