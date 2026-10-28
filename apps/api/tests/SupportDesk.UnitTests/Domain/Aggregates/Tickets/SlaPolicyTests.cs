using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    public void WindowFor_HalvesTheBaseWindowForPremiumCustomers(TicketPriority priority, CustomerTier tier, double expectedHours) =>
        Assert.Equal(TimeSpan.FromHours(expectedHours), TestSla.Policy().WindowFor(priority, tier));

    [Fact]
    public void WindowFor_PremiumNeverGoesBelowTheFloor()
    {
        var policy = TestSla.Policy();
        policy.BaseWindowHours[TicketPriority.Critical] = 1.5;

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumExactlyAtTheFloor_IsKept()
    {
        var policy = TestSla.Policy();
        policy.BaseWindowHours[TicketPriority.Critical] = 2;

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_StandardIsNotSubjectToThePremiumFloor()
    {
        var policy = TestSla.Policy();
        policy.BaseWindowHours[TicketPriority.Critical] = 0.5;

        Assert.Equal(TimeSpan.FromMinutes(30), policy.WindowFor(TicketPriority.Critical, CustomerTier.Standard));
    }

    [Fact]
    public void Validate_AcceptsTheConfiguredPolicy() => TestSla.Policy().Validate();

    [Fact]
    public void Validate_WhenAPriorityHasNoWindow_Throws()
    {
        var policy = TestSla.Policy();
        policy.BaseWindowHours.Remove(TicketPriority.Low);

        var exception = Assert.Throws<InvalidOperationException>(policy.Validate);

        Assert.Contains("Low", exception.Message);
    }
}
