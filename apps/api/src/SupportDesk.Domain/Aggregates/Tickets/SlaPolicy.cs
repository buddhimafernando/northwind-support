using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How long the team has to respond to a ticket. Bound from the <c>Sla</c> configuration
/// section, so every number here can be changed without touching code.
/// </summary>
public sealed class SlaPolicy
{
    public const string SectionName = "Sla";

    /// <summary>The response window per priority, before any customer-tier adjustment.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; init; } = [];

    /// <summary>What a Premium customer's window is multiplied by, e.g. 0.5 for half.</summary>
    public double PremiumCustomerMultiplier { get; init; }

    /// <summary>The shortest window a Premium adjustment may produce.</summary>
    public double MinimumWindowHours { get; init; }

    /// <summary>An open ticket with this share of its window or less remaining is at risk.</summary>
    public double AtRiskThresholdPercent { get; init; }

    /// <summary>The response window for a ticket of this priority raised by this kind of customer.</summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier)
    {
        var hours = BaseWindowHours[priority];

        if (tier == CustomerTier.Premium)
        {
            hours = Math.Max(hours * PremiumCustomerMultiplier, MinimumWindowHours);
        }

        return TimeSpan.FromHours(hours);
    }

    /// <summary>Fails fast at startup when the configuration is incomplete or nonsensical.</summary>
    /// <exception cref="InvalidOperationException">A value is missing or out of range.</exception>
    public void Validate()
    {
        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:{nameof(BaseWindowHours)}:{priority} must be a positive number of hours.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(PremiumCustomerMultiplier)} must be greater than 0 and at most 1.");
        }

        if (MinimumWindowHours <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(MinimumWindowHours)} must be a positive number of hours.");
        }

        if (AtRiskThresholdPercent is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(AtRiskThresholdPercent)} must be between 0 and 100.");
        }
    }
}
