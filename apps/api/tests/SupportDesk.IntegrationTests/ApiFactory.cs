using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests;

/// <summary>
/// The real API, hosted in memory: the same Program, middleware, authentication and fallback
/// policy as production. Only the database (in-memory Sqlite) and the configuration differ.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test_password";
    public const string ActiveEmail = "alex.turner@example.test";
    public const string InactiveEmail = "yuki.tanaka@example.test";

    // Kept open for the factory's lifetime: an in-memory Sqlite database lives as long as its connection.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>A ticket the tests may escalate.</summary>
    public int TicketId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, so Program skips the SQL Server migration and the demo seed.
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Default", "unused - replaced by Sqlite below");
        builder.UseSetting("Jwt:Issuer", "SupportDesk");
        builder.UseSetting("Jwt:Audience", "SupportDesk.Api");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        // A fresh random key every run: no key, real or test, is committed.
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Sla:BaseWindowHours:Low", "72");
        builder.UseSetting("Sla:BaseWindowHours:Medium", "24");
        builder.UseSetting("Sla:BaseWindowHours:High", "8");
        builder.UseSetting("Sla:BaseWindowHours:Critical", "4");
        builder.UseSetting("Sla:PremiumCustomerMultiplier", "0.5");
        builder.UseSetting("Sla:MinimumWindowHours", "1");
        builder.UseSetting("Sla:AtRiskThresholdPercent", "25");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Agent>>();

        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var customer = new Customer("Fabrikam Inc", "helpdesk@fabrikam.example", null, CustomerTier.Standard, now);

        var active = new Agent("Alex Turner", ActiveEmail, 10, now);
        active.SetPasswordHash(hasher.HashPassword(active, Password));

        var inactive = new Agent("Yuki Tanaka", InactiveEmail, 8, now);
        inactive.SetPasswordHash(hasher.HashPassword(inactive, Password));
        inactive.Deactivate();

        db.AddRange(category, customer, active, inactive);
        await db.SaveChangesAsync();

        var ticket = Ticket.Raise(
            "TCK-0001", "Printer offline", "The office printer is offline.", customer.Id, category.Id,
            TicketPriority.Low, now, now.AddHours(72));
        db.Add(ticket);
        await db.SaveChangesAsync();

        TicketId = ticket.Id;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    public Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    /// <summary>A client carrying a token for the active agent, obtained through the real login.</summary>
    public async Task<HttpClient> SignedInClientAsync()
    {
        var client = CreateClient();
        var response = await LoginAsync(client, ActiveEmail, Password);
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());

        return client;
    }
}
