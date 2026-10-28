using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentAgentQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();

    [Fact]
    public async Task HandleAsync_ReturnsTheSignedInAgent()
    {
        _agents.Setup(a => a.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Agent("Priya Nair", "priya@example.com", 8, FixedClock.DefaultNow));

        var agent = await new GetCurrentAgentQueryHandler(_agents.Object).HandleAsync(3, CancellationToken.None);

        Assert.Equal("Priya Nair", agent.FullName);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAgentWasDeactivatedSinceSigningIn_Throws()
    {
        var agent = new Agent("Yuki Tanaka", "yuki@example.com", 8, FixedClock.DefaultNow);
        agent.Deactivate();
        _agents.Setup(a => a.GetByIdAsync(6, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => new GetCurrentAgentQueryHandler(_agents.Object).HandleAsync(6, CancellationToken.None));
    }
}
