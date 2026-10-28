using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class AgentSelectorTests
{
    // Unsaved categories all have id 0, so a specialist lists 0 and a non-specialist lists something else.
    private static readonly Category General = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private static readonly Category Billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
    private const int OtherCategoryId = 99;

    private static AgentCandidate Agent(
        int id, int open, int max = 10, bool active = true, int specializationId = OtherCategoryId) =>
        new(id, $"Agent {id}", active, max, open, [specializationId]);

    [Fact]
    public void Choose_PicksTheAgentWithTheFewestOpenTickets()
    {
        var (agent, reason) = AgentSelector.Choose(General, [Agent(1, open: 5), Agent(2, open: 2), Agent(3, open: 4)]);

        Assert.Equal(2, agent?.Id);
        Assert.Contains("Agent 2", reason);
    }

    [Fact]
    public void Choose_OnATie_PicksTheLowestIdWhateverTheOrder()
    {
        var (agent, _) = AgentSelector.Choose(General, [Agent(7, open: 3), Agent(4, open: 3), Agent(9, open: 3)]);

        Assert.Equal(4, agent?.Id);
    }

    [Fact]
    public void Choose_ForASpecialistCategory_IgnoresLessBusyNonSpecialists()
    {
        var (agent, _) = AgentSelector.Choose(
            Billing, [Agent(1, open: 0), Agent(2, open: 5, specializationId: Billing.Id)]);

        Assert.Equal(2, agent?.Id);
    }

    [Fact]
    public void Choose_SkipsAgentsAtTheirLimit()
    {
        var (agent, _) = AgentSelector.Choose(General, [Agent(1, open: 6, max: 6), Agent(2, open: 7, max: 8)]);

        Assert.Equal(2, agent?.Id);
    }

    [Fact]
    public void Choose_SkipsInactiveAgents()
    {
        var (agent, _) = AgentSelector.Choose(General, [Agent(1, open: 0, active: false), Agent(2, open: 3)]);

        Assert.Equal(2, agent?.Id);
    }

    [Fact]
    public void Choose_WhenSpecialistsAreAllFull_LeavesUnassignedAndSaysWhy()
    {
        var (agent, reason) = AgentSelector.Choose(
            Billing, [Agent(1, open: 0), Agent(2, open: 6, max: 6, specializationId: Billing.Id)]);

        Assert.Null(agent);
        Assert.Contains("every active Billing specialist is at their open-ticket limit", reason);
    }

    [Fact]
    public void Choose_WhenNobodyIsQualified_LeavesUnassignedAndSaysWhy()
    {
        var (agent, reason) = AgentSelector.Choose(Billing, [Agent(1, open: 0)]);

        Assert.Null(agent);
        Assert.Contains("no active Billing specialist", reason);
    }

    [Fact]
    public void KeepOrChoose_KeepsAnEligibleOwnerEvenIfOthersAreLessBusy()
    {
        var (agent, reason) = AgentSelector.KeepOrChoose(1, General, [Agent(1, open: 8), Agent(2, open: 0)]);

        Assert.Equal(1, agent?.Id);
        Assert.Contains("Kept", reason);
    }

    [Fact]
    public void KeepOrChoose_KeepsAnOwnerAtTheirLimit_BecauseTheTicketIsAlreadyCounted()
    {
        var (agent, _) = AgentSelector.KeepOrChoose(1, General, [Agent(1, open: 6, max: 6), Agent(2, open: 0)]);

        Assert.Equal(1, agent?.Id);
    }

    [Fact]
    public void KeepOrChoose_ReassignsWhenTheOwnerIsNoLongerActive()
    {
        var (agent, reason) = AgentSelector.KeepOrChoose(
            1, General, [Agent(1, open: 2, active: false), Agent(2, open: 5), Agent(3, open: 3)]);

        Assert.Equal(3, agent?.Id);
        Assert.Contains("Agent 1 is no longer eligible", reason);
    }

    [Fact]
    public void KeepOrChoose_WhenUnassigned_ChoosesAsOnCreate()
    {
        var (agent, _) = AgentSelector.KeepOrChoose(null, General, [Agent(1, open: 5), Agent(2, open: 2)]);

        Assert.Equal(2, agent?.Id);
    }
}
