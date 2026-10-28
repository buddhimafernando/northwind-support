using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_AppliesTheDecision_AndRecordsBeforeAndAfter()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(1).Build();
        var dueBefore = ticket.DueAtUtc;
        var decision = new TriageDecision(TicketPriority.High, "", Later.AddHours(8), "", 2, "");

        var escalation = ticket.Escalate(decision, "  Customer is losing sales  ", "Team lead", Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(Later.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Equal(dueBefore, escalation.FromDueAtUtc);
        Assert.Equal(Later.AddHours(8), escalation.ToDueAtUtc);
        Assert.Equal("Customer is losing sales", escalation.Reason);
        Assert.Equal("Team lead", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_ThrowsAndChangesNothing()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0099").WithPriority(TicketPriority.Critical).Build();
        var decision = new TriageDecision(TicketPriority.Critical, "", Later, "", 2, "");

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.Escalate(decision, "A valid reason", "Team lead", Later));

        Assert.Contains("TCK-0099 is already Critical", exception.Message);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_WhenFinished_SaysWhichStatusBlocksIt(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithReference("TCK-0010").WithStatus(status).Build();
        var decision = new TriageDecision(TicketPriority.High, "", Later, "", null, "");

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.Escalate(decision, "A valid reason", "Team lead", Later));

        Assert.Contains($"TCK-0010 is {status}", exception.Message);
    }
}
