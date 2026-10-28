using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private const double AtRiskPercent = 25;

    // A 4 h window: at risk from 1 h before it is due.
    private static readonly DateTime Start = FixedClock.DefaultNow;
    private static readonly DateTime Due = Start.AddHours(4);

    private static SlaStatus Evaluate(DateTime? due, DateTime? resolved, DateTime now) =>
        SlaEvaluator.Evaluate(Start, due, resolved, now, AtRiskPercent);

    [Fact]
    public void Evaluate_WithoutDueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, Evaluate(null, null, Start));

    [Fact]
    public void Evaluate_ResolvedExactlyAtDue_IsMet() =>
        Assert.Equal(SlaStatus.Met, Evaluate(Due, Due, Due.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedOneTickAfterDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(Due, Due.AddTicks(1), Due.AddDays(1)));

    [Fact]
    public void Evaluate_OpenExactlyAtDue_IsAtRiskNotBreached() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(Due, null, Due));

    [Fact]
    public void Evaluate_OpenOneTickPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(Due, null, Due.AddTicks(1)));

    [Fact]
    public void Evaluate_OpenWithExactly25PercentLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(Due, null, Due.AddHours(-1)));

    [Fact]
    public void Evaluate_OpenWithJustOver25PercentLeft_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Evaluate(Due, null, Due.AddHours(-1).AddTicks(-1)));

    [Fact]
    public void Evaluate_UsesTheConfiguredThreshold() =>
        Assert.Equal(
            SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(Start, Due, null, Due.AddHours(-2), atRiskThresholdPercent: 50));
}
