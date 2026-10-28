using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "alex.turner@northwind-support.example";
    private const string Password = "Correct-Horse-1";

    // The real hasher, so these tests prove hashing and verification, not a mock of them.
    private readonly PasswordHasher<Agent> _hasher = new();
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IAccessTokenIssuer> _tokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<Agent>()))
            .Returns(new AccessToken("signed.jwt.token", FixedClock.DefaultNow.AddHours(1)));

        _sut = new LoginCommandHandler(
            _agents.Object, _hasher, _tokens.Object, _unitOfWork.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    private Agent ExistingAgent(bool isActive = true)
    {
        var agent = new Agent("Alex Turner", Email, 10, FixedClock.DefaultNow);
        agent.SetPasswordHash(_hasher.HashPassword(agent, Password));

        if (!isActive)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return agent;
    }

    [Fact]
    public void StoredHash_IsSaltedAndNotThePassword()
    {
        var agent = ExistingAgent();
        var sameAgentAgain = new Agent("Alex Turner", Email, 10, FixedClock.DefaultNow);

        Assert.DoesNotContain(Password, agent.PasswordHash);
        Assert.NotEqual(agent.PasswordHash, _hasher.HashPassword(sameAgentAgain, Password));
    }

    [Fact]
    public async Task HandleAsync_WithTheRightPassword_IssuesAToken()
    {
        ExistingAgent();

        var response = await _sut.HandleAsync(new LoginRequest($"  {Email}  ", Password), CancellationToken.None);

        Assert.Equal("signed.jwt.token", response.AccessToken);
        Assert.Equal("Alex Turner", response.Agent.FullName);
    }

    [Fact]
    public async Task HandleAsync_WrongPasswordAndUnknownEmail_FailIdenticallyWithoutAToken()
    {
        ExistingAgent();

        var wrongPassword = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _sut.HandleAsync(new LoginRequest(Email, "Wrong-Password-1"), CancellationToken.None));
        var unknownEmail = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _sut.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentials, wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never());
    }

    [Fact]
    public async Task HandleAsync_InactiveAgentWithTheRightPassword_IsRefused()
    {
        ExistingAgent(isActive: false);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _sut.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentials, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>()), Times.Never());
    }
}
