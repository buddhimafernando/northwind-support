namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <param name="windowStartUtc">When the current window began: creation, or the last escalation.</param>
    /// <param name="atRiskThresholdPercent">See <see cref="SlaPolicy.AtRiskThresholdPercent"/>.</param>
    public static SlaStatus Evaluate(
        DateTime windowStartUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        double atRiskThresholdPercent)
    {
        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        var remaining = due - nowUtc;
        var atRiskFrom = (due - windowStartUtc) * (atRiskThresholdPercent / 100);

        return remaining <= atRiskFrom ? SlaStatus.AtRisk : SlaStatus.WithinSla;
    }
}
