using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy slaPolicy) : ITicketQueries
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var normalizedQuery = query.Normalized();

        var now = Now();

        var tickets = ApplyFilters(TicketsWithLabels(), normalizedQuery, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, normalizedQuery)
            .Skip((normalizedQuery.Page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.WindowStartUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, slaPolicy.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, normalizedQuery.Page, normalizedQuery.PageSize, totalCount);
    }


    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = Now();

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.WindowStartUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, slaPolicy.AtRiskThresholdPercent)
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Set<Ticket>().AsNoTracking().AnyAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(int ticketId, CancellationToken ct) =>
        await Escalations(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task<TicketEscalationDto?> GetEscalationAsync(int escalationId, CancellationToken ct) =>
        Escalations(e => e.Id == escalationId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = Now();

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.WindowStartUtc, x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, slaPolicy.AtRiskThresholdPercent)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels
        {
            Ticket = ticket,
            Customer = customer,
            Category = category,
            Agent = agent,
            // An escalation restarts the SLA window, so the latest one is when it began.
            WindowStartUtc = db.Set<TicketEscalation>()
                .Where(e => e.TicketId == ticket.Id)
                .Max(e => (DateTime?)e.EscalatedAtUtc) ?? ticket.CreatedAtUtc
        };

    /// <summary>Escalation history rows matching <paramref name="predicate"/>, newest first.</summary>
    private IQueryable<TicketEscalationDto> Escalations(Expression<Func<TicketEscalation, bool>> predicate) =>
        from escalation in db.Set<TicketEscalation>().AsNoTracking().Where(predicate)
        from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
        from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
        orderby escalation.EscalatedAtUtc descending, escalation.Id descending
        select new TicketEscalationDto(
            escalation.Id,
            escalation.FromPriority,
            escalation.ToPriority,
            fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
            toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
            escalation.FromDueAtUtc,
            escalation.ToDueAtUtc,
            escalation.Reason,
            escalation.EscalatedBy,
            escalation.EscalatedAtUtc);

    private IQueryable<TicketWithLabels> ApplyFilters(IQueryable<TicketWithLabels> source, TicketQuery query, DateTime now)
    {
        if (query.Status.HasValue)
        {
            source = source.Where(x => x.Ticket.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(x => x.Ticket.Priority == query.Priority.Value);
        }

        if (query.CategoryId.HasValue)
        {
            source = source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            source = source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);
        }

        if (query.AssignedAgentId.HasValue)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            source = source.Where(x =>
                EF.Functions.Like(x.Ticket.Title, term) ||
                EF.Functions.Like(x.Ticket.Reference, term) ||
                EF.Functions.Like(x.Customer.Name, term));
        }

        if (query.SlaStatus.HasValue)
        {
            source = source.Where(HasSlaStatus(query.SlaStatus.Value, now));
        }

        return source;
    }

    /// <summary>
    /// <see cref="SlaEvaluator"/>'s rule as a SQL predicate, so the list can be filtered, counted
    /// and paged by SLA status in the database. Change one, change the other.
    /// </summary>
    /// <remarks>
    /// "Remaining &lt;= window x share" is rewritten as "now &gt;= the moment the ticket turns at risk",
    /// so <paramref name="now"/> is compared directly instead of through DATEDIFF, which counts
    /// boundaries rather than elapsed time. The window is measured in milliseconds, exact for
    /// datetime2(3) columns; SLA windows are hours, far below DATEDIFF's int limit (~24 days).
    /// </remarks>
    private Expression<Func<TicketWithLabels, bool>> HasSlaStatus(SlaStatus status, DateTime now)
    {
        var atRiskShare = slaPolicy.AtRiskThresholdPercent / 100;

        return status switch
        {
            SlaStatus.NotApplicable => x => x.Ticket.DueAtUtc == null,

            SlaStatus.Met => x => x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc,

            SlaStatus.Breached => x =>
                (x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < now),

            SlaStatus.AtRisk => x =>
                x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc >= now &&
                x.Ticket.DueAtUtc!.Value.AddMilliseconds(
                    -(EF.Functions.DateDiffMillisecond(x.WindowStartUtc, x.Ticket.DueAtUtc.Value) * atRiskShare)) <= now,

            SlaStatus.WithinSla => x =>
                x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc >= now &&
                x.Ticket.DueAtUtc!.Value.AddMilliseconds(
                    -(EF.Functions.DateDiffMillisecond(x.WindowStartUtc, x.Ticket.DueAtUtc.Value) * atRiskShare)) > now,

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    /// <summary>
    /// The current time cut to whole milliseconds, the precision of the stored dates, so the SQL
    /// filter and <see cref="SlaEvaluator"/> compare the same instant. SQL Server would otherwise
    /// round the parameter to the column's precision.
    /// </summary>
    private DateTime Now()
    {
        var now = clock.UtcNow;

        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }

        /// <summary>When the ticket's current SLA window began: its last escalation, or its creation.</summary>
        public required DateTime WindowStartUtc { get; init; }
    }
}
