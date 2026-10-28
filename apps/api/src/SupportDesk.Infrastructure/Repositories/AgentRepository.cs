using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <remarks>
    /// A plain comparison, so SQL Server can seek UQ_Agents_Email. The database collation is
    /// case-insensitive, so "Alex.Turner@..." still finds "alex.turner@...".
    /// </remarks>
    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>().FirstOrDefaultAsync(a => a.Email == email, ct);
}
