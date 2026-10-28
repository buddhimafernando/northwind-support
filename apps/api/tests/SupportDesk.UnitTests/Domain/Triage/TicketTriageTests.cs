using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Triage;

public class TicketTriageTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private static readonly Category General = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Outage = new("Outage", requiresSpecialist: true, forcesCriticalPriority: true);

    private readonly TicketTriage _sut = new(TestSla.Policy());

    private static AgentCandidate Agent(int id, int open, bool active = true) =>
        new(id, $"Agent {id}", active, MaxOpenTickets: 10, open, [General.Id]);

    [Fact]
    public void ForNewTicket_UsesTheRequestedPriority()
    {
        var decision = _sut.ForNewTicket(General, CustomerTier.Standard, TicketPriority.High, [Agent(1, 0)], Now);

        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
    }

    [Fact]
    public void ForNewTicket_WithoutARequest_DefaultsToMedium()
    {
        var decision = _sut.ForNewTicket(General, CustomerTier.Standard, null, [Agent(1, 0)], Now);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal(Now.AddHours(24), decision.DueAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(null)]
    public void ForNewTicket_ForcedCriticalCategory_IsAlwaysCritical(TicketPriority? requested)
    {
        var decision = _sut.ForNewTicket(Outage, CustomerTier.Standard, requested, [Agent(1, 0)], Now);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Outage", decision.PriorityReason);
    }

    [Fact]
    public void ForNewTicket_PremiumCustomer_GetsTheShorterWindow()
    {
        var decision = _sut.ForNewTicket(Outage, CustomerTier.Premium, null, [Agent(1, 0)], Now);

        Assert.Equal(Now.AddHours(2), decision.DueAtUtc);
    }

    [Fact]
    public void ForNewTicket_WithNobodyEligible_IsUnassignedWithAReason()
    {
        var decision = _sut.ForNewTicket(General, CustomerTier.Standard, null, [Agent(1, 0, active: false)], Now);

        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("unassigned", decision.AssignmentReason);
    }

    [Fact]
    public void ForEscalation_RaisesOneLevel_AndRecomputesTheDueDateFromNow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).AssignedTo(1).Build();
        var escalatedAt = Now.AddDays(2);

        var decision = _sut.ForEscalation(ticket, General, CustomerTier.Premium, [Agent(1, 3)], escalatedAt);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Equal(escalatedAt.AddHours(12), decision.DueAtUtc);
    }

    [Fact]
    public void ForEscalation_KeepsAnEligibleOwner()
    {
        var ticket = new TicketBuilder().AssignedTo(1).Build();

        var decision = _sut.ForEscalation(ticket, General, CustomerTier.Standard, [Agent(1, 9), Agent(2, 0)], Now);

        Assert.Equal(1, decision.AssignedAgentId);
    }

    [Fact]
    public void ForEscalation_ReassignsWhenTheOwnerIsNoLongerEligible()
    {
        var ticket = new TicketBuilder().AssignedTo(1).Build();

        var decision = _sut.ForEscalation(
            ticket, General, CustomerTier.Standard, [Agent(1, 0, active: false), Agent(2, 4)], Now);

        Assert.Equal(2, decision.AssignedAgentId);
    }

    [Fact]
    public void ForEscalation_OfACriticalTicket_Throws()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => _sut.ForEscalation(ticket, General, CustomerTier.Standard, [Agent(1, 0)], Now));

        Assert.Contains("already Critical", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void ForEscalation_OfAFinishedTicket_Throws(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => _sut.ForEscalation(ticket, General, CustomerTier.Standard, [Agent(1, 0)], Now));

        Assert.Contains(status.ToString(), exception.Message);
    }
}
