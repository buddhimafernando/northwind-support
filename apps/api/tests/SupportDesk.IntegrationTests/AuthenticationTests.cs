using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportDesk.IntegrationTests;

/// <summary>Sign-in and access control, end to end over HTTP.</summary>
public sealed class AuthenticationTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsATokenItsExpiryAndTheAgent()
    {
        var response = await api.LoginAsync(api.CreateClient(), ApiFactory.ActiveEmail, ApiFactory.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.True(body.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow);
        Assert.Equal("Alex Turner", body.GetProperty("agent").GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Login_WrongPasswordUnknownEmailAndInactiveAgent_AllGetTheSame401AndNoToken()
    {
        var client = api.CreateClient();

        var responses = new[]
        {
            await api.LoginAsync(client, ApiFactory.ActiveEmail, "Wrong_password"),
            await api.LoginAsync(client, "nobody@example.test", ApiFactory.Password),
            await api.LoginAsync(client, ApiFactory.InactiveEmail, ApiFactory.Password)
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await JsonAsync(response);
            Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
            Assert.False(body.TryGetProperty("accessToken", out _));
        }
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/agents")]
    [InlineData("/api/customers")]
    [InlineData("/api/categories")]
    [InlineData("/api/auth/me")]
    public async Task ProtectedEndpoint_Is401WithoutAToken_And200WithOne(string path)
    {
        var anonymous = await api.CreateClient().GetAsync(path);
        var signedIn = await (await api.SignedInClientAsync()).GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_RejectsATamperedToken()
    {
        var client = await api.SignedInClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^2] + "xx");

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheSignedInAgent()
    {
        var response = await (await api.SignedInClientAsync()).GetAsync("/api/auth/me");

        var body = await JsonAsync(response);
        Assert.Equal(ApiFactory.ActiveEmail, body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Escalate_RecordsTheAgentFromTheToken_NotTheRequestBody()
    {
        var client = await api.SignedInClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{api.TicketId}/escalate",
            new { reason = "Whole office cannot print", escalatedBy = "Someone Else" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("Alex Turner", body.GetProperty("escalation").GetProperty("escalatedBy").GetString());
    }
}
