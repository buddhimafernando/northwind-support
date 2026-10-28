using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        // Measured trimmed, because that is what gets stored.
        RuleFor(x => (x.Reason ?? string.Empty).Trim())
            .Length(TicketEscalation.ReasonMinLength, TicketEscalation.ReasonMaxLength)
            .OverridePropertyName(nameof(EscalateTicketRequest.Reason))
            .WithMessage(
                $"Reason must be between {TicketEscalation.ReasonMinLength} and " +
                $"{TicketEscalation.ReasonMaxLength} characters.");
    }
}
