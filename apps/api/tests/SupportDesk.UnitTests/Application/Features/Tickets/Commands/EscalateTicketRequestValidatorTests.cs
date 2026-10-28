using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Theory]
    [InlineData("12345", true)]
    [InlineData("1234", false)]
    [InlineData("  1234  ", false)]
    [InlineData("", false)]
    public void Reason_MustBeAtLeastFiveCharactersOnceTrimmed(string reason, bool valid) =>
        Assert.Equal(valid, _validator.Validate(new EscalateTicketRequest(reason, "Team lead")).IsValid);

    [Fact]
    public void Reason_MayBeExactly500Characters() =>
        Assert.True(_validator.Validate(new EscalateTicketRequest(new string('x', 500), "Team lead")).IsValid);

    [Fact]
    public void Reason_MayNotBe501Characters()
    {
        var result = _validator.Validate(new EscalateTicketRequest(new string('x', 501), "Team lead"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EscalatedBy_IsRequired(string escalatedBy)
    {
        var result = _validator.Validate(new EscalateTicketRequest("A valid reason", escalatedBy));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }
}
