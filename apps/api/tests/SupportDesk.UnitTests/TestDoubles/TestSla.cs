using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA policy from the assignment (and appsettings.Development.json), for tests that need
/// one. Tests about configurability build their own.
/// </summary>
public static class TestSla
{
    public static SlaPolicy Policy() => new()
    {
        BaseWindowHours = new()
        {
            [TicketPriority.Critical] = 4,
            [TicketPriority.High] = 8,
            [TicketPriority.Medium] = 24,
            [TicketPriority.Low] = 72
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };
}
