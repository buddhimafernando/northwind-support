using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Why triage chose the priority, due date and owner of a new ticket.</summary>
public sealed record TriageDto(string PriorityReason, string SlaReason, string AssignmentReason);

/// <summary>One row of a ticket's escalation history.</summary>
public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>Response of POST /api/tickets/{id}/escalate.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
