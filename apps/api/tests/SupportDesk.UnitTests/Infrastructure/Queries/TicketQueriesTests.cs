using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Common;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly FixedClock _clock = new();
    private readonly TicketQueries _sut;

    private readonly Customer _customerContoso;
    private readonly Customer _customerFabrikam;
    private readonly Category _categoryGeneral;
    private readonly Category _categoryBilling;
    private readonly Agent _agentAlex;
    private readonly Agent _agentBen;

    public TicketQueriesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        _sut = new TicketQueries(_db, _clock, TestSla.Policy());

        _customerContoso = new Customer("Contoso", "contact@contoso.com", null, CustomerTier.Premium, _clock.UtcNow);
        _customerFabrikam = new Customer("Fabrikam", "contact@fabrikam.com", null, CustomerTier.Standard, _clock.UtcNow);
        _categoryGeneral = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        _categoryBilling = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
        _agentAlex = new Agent("Alex Turner", "alex@northwind.com", 10, _clock.UtcNow);
        _agentBen = new Agent("Ben Osei", "ben@northwind.com", 8, _clock.UtcNow);

        _db.AddRange(_customerContoso, _customerFabrikam);
        _db.AddRange(_categoryGeneral, _categoryBilling);
        _db.AddRange(_agentAlex, _agentBen);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPagedAsync_ExactMatchPriority_ReturnsMatchingTicketsOnly()
    {
        // Arrange
        var highTicket = Ticket.Raise("TCK-0001", "High issue", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.High, _clock.UtcNow);
        var lowTicket = Ticket.Raise("TCK-0002", "Low issue", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Low, _clock.UtcNow);
        _db.AddRange(highTicket, lowTicket);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { Priority = TicketPriority.High };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
        Assert.Equal(TicketPriority.High, item.Priority);
    }

    [Fact]
    public async Task GetPagedAsync_SearchOnCustomerName_MatchesTrimmedAndCaseInsensitive()
    {
        // Arrange
        var t1 = Ticket.Raise("TCK-0001", "Printer broken", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Medium, _clock.UtcNow);
        var t2 = Ticket.Raise("TCK-0002", "Network down", "Desc", _customerFabrikam.Id, _categoryGeneral.Id, TicketPriority.Medium, _clock.UtcNow);
        _db.AddRange(t1, t2);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { Search = "  contoso  " };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("Contoso", item.Customer.Name);
    }

    [Fact]
    public async Task GetPagedAsync_CombinationOfFilters_CombinesWithAnd()
    {
        // Arrange
        var matching = Ticket.Raise("TCK-0001", "Billing inquiry", "Desc", _customerContoso.Id, _categoryBilling.Id, TicketPriority.High, _clock.UtcNow);
        matching.AssignTo(_agentAlex.Id, _clock.UtcNow);
        matching.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow);

        var wrongStatus = Ticket.Raise("TCK-0002", "Billing payment", "Desc", _customerContoso.Id, _categoryBilling.Id, TicketPriority.High, _clock.UtcNow);
        wrongStatus.AssignTo(_agentAlex.Id, _clock.UtcNow);

        var wrongAgent = Ticket.Raise("TCK-0003", "Billing dispute", "Desc", _customerContoso.Id, _categoryBilling.Id, TicketPriority.High, _clock.UtcNow);
        wrongAgent.AssignTo(_agentBen.Id, _clock.UtcNow);
        wrongAgent.ChangeStatus(TicketStatus.InProgress, _clock.UtcNow);

        _db.AddRange(matching, wrongStatus, wrongAgent);
        await _db.SaveChangesAsync();

        var query = new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CategoryId = _categoryBilling.Id,
            CustomerId = _customerContoso.Id,
            AssignedAgentId = _agentAlex.Id
        };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnly_ReturnsOnlyTicketsWithoutAssignedAgent()
    {
        // Arrange
        var unassigned = Ticket.Raise("TCK-0001", "No one owns me", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Low, _clock.UtcNow);
        var assigned = Ticket.Raise("TCK-0002", "Someone owns me", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Low, _clock.UtcNow);
        assigned.AssignTo(_agentAlex.Id, _clock.UtcNow);

        _db.AddRange(unassigned, assigned);
        await _db.SaveChangesAsync();

        var query = new TicketQuery { UnassignedOnly = true };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", item.Reference);
        Assert.Null(item.AssignedAgent);
    }

    [Fact]
    public async Task GetPagedAsync_SearchOnReferenceAndTitle_MatchesBoth()
    {
        // Arrange
        var t1 = Ticket.Raise("TCK-0042", "Normal issue", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Low, _clock.UtcNow);
        var t2 = Ticket.Raise("TCK-0099", "VPN gateway reset", "Desc", _customerContoso.Id, _categoryGeneral.Id, TicketPriority.Low, _clock.UtcNow);
        _db.AddRange(t1, t2);
        await _db.SaveChangesAsync();

        var refQuery = new TicketQuery { Search = "0042" };
        var titleQuery = new TicketQuery { Search = "gateway" };

        // Act
        var refResult = await _sut.GetPagedAsync(refQuery, CancellationToken.None);
        var titleResult = await _sut.GetPagedAsync(titleQuery, CancellationToken.None);

        // Assert
        Assert.Equal("TCK-0042", Assert.Single(refResult.Items).Reference);
        Assert.Equal("TCK-0099", Assert.Single(titleResult.Items).Reference);
    }

    [Fact]
    public async Task GetPagedAsync_DatabaseFiltersBeforeCountingAndPaging()
    {
        // Arrange: 5 tickets, only 3 match filter
        for (var i = 1; i <= 5; i++)
        {
            var priority = i <= 3 ? TicketPriority.Critical : TicketPriority.Low;
            _db.Add(Ticket.Raise($"TCK-000{i}", $"Issue {i}", "Desc", _customerContoso.Id, _categoryGeneral.Id, priority, _clock.UtcNow));
        }
        await _db.SaveChangesAsync();

        var query = new TicketQuery
        {
            Priority = TicketPriority.Critical,
            Page = 1,
            PageSize = 2
        };

        // Act
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
    }
}
