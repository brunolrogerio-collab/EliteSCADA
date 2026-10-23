using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Scadivo.Portal.Tests;

public sealed class PortalIntegrationTests(ITestOutputHelper output)
{
    private const string Password = "PortalTest-LongPassword22!";

    [Fact]
    public async Task CompletePilotEnforcesIdentityCsrfIsolationLockoutAndPersistence()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-portal-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "owner");
        await File.WriteAllTextAsync(passwordFile, Password);
        var connection = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
            ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!";
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = connection,
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Logging:LogLevel:Default"] = "Warning"
        };
        var marker = Guid.NewGuid().ToString("N")[..8];
        Guid ticketId;
        try
        {
            await using (var factory = Factory(configuration))
            {
                using var anonymous = Client(factory);
                Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/support")).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("api/auth/login", new { username = "scadivo.owner", password = Password })).StatusCode);
                var owner = await Login(factory, "scadivo.owner");
                Assert.True((await owner.Get("/admin/overview")).GetProperty("accounts").GetInt32() >= 1);
                var sessionResponse = await owner.Client.GetAsync("api/session");
                Assert.Contains("no-store", sessionResponse.Headers.CacheControl!.ToString());
                Assert.Contains("frame-ancestors 'none'", sessionResponse.Headers.GetValues("Content-Security-Policy").Single());

                var invitation = await owner.Send("/admin/invitations", HttpMethod.Post, new { username = "member-" + marker, displayName = "Alex", organization = "Example Integration" });
                Assert.False(invitation.GetProperty("emailDeliveryConfigured").GetBoolean());
                var payload = Invitation(invitation);
                var invited = new Session(anonymous, await Csrf(anonymous));
                await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password });
                Assert.Equal(HttpStatusCode.BadRequest, (await invited.Raw("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password })).StatusCode);
                var member = await Login(factory, "member-" + marker);
                Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("api/admin/accounts")).StatusCode);
                Assert.False((await member.Get("/profile")).GetProperty("owner").GetBoolean());
                await member.Send("/profile", HttpMethod.Put, new { displayName = "Alex · atualizado", locale = "en" });
                Assert.Equal("Alex · atualizado", (await member.Get("/profile")).GetProperty("displayName").GetString());
                var faq = await member.Get("/faq?q=200&lang=en");
                Assert.Contains(faq.EnumerateArray(), article => article.GetProperty("title").GetString()!.Contains("200"));
                Assert.Equal(0, (await member.Get("/downloads")).GetArrayLength());
                var ticket = await member.Send("/support", HttpMethod.Post, new { subject = "Test " + marker, category = "technical", body = "<script>alert('xss')</script> textual support example" });
                ticketId = ticket.GetProperty("id").GetGuid();

                var otherInvite = await owner.Send("/admin/invitations", HttpMethod.Post, new { username = "other-" + marker, displayName = "Taylor" });
                var otherPayload = Invitation(otherInvite);
                await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { otherPayload.UserId, otherPayload.Token, password = Password });
                var other = await Login(factory, "other-" + marker);
                Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync("api/support/" + ticketId)).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await other.Raw("/support/" + ticketId + "/messages", HttpMethod.Post, new { body = "Not my ticket" })).StatusCode);
                Assert.Equal(0, (await other.Get("/support")).GetArrayLength());
                await owner.Send("/support/" + ticketId + "/messages", HttpMethod.Post, new { body = "Private staff response" });
                var conversation = await member.Get("/support/" + ticketId);
                Assert.Equal(2, conversation.GetProperty("messages").GetArrayLength());
                Assert.True(conversation.GetProperty("messages")[1].GetProperty("staffReply").GetBoolean());
                var template = (await owner.Get("/admin/templates"))[0];
                await owner.Send("/admin/templates/" + template.GetProperty("key").GetString(), HttpMethod.Put, new
                {
                    subjectPt = "Olá {{name}}", subjectEn = "Hello {{name}}", bodyPt = "Referência {{reference}}", bodyEn = "Reference {{reference}}"
                });
                var article = await owner.Send("/admin/faq", HttpMethod.Post, new { slug = "qa-" + marker, category = "test", titlePt = "Pergunta teste", titleEn = "Test question", bodyPt = "Resposta teste", bodyEn = "Test answer", published = true });
                Assert.Equal("qa-" + marker, article.GetProperty("slug").GetString());
                Assert.False((await owner.Get("/admin/overview")).GetProperty("licenseIntegrationEnabled").GetBoolean());
                Assert.Contains((await owner.Get("/admin/audit")).EnumerateArray(), entry => entry.GetProperty("action").GetString() == "support.replied");

                // Five wrong passwords trigger Identity lockout; correct password must still be denied.
                using var lockoutClient = Client(factory);
                var lockedSession = new Session(lockoutClient, await Csrf(lockoutClient));
                for (var attempt = 0; attempt < 5; attempt++)
                    Assert.Equal(HttpStatusCode.Unauthorized, (await lockedSession.Raw("/auth/login", HttpMethod.Post, new { username = "other-" + marker, password = "Incorrect-Password22!" })).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await lockedSession.Raw("/auth/login", HttpMethod.Post, new { username = "other-" + marker, password = Password })).StatusCode);
            }
            // A fresh host reuses persisted database and key ring, with no reseeding or data loss.
            await using (var restarted = Factory(configuration))
            {
                var member = await Login(restarted, "member-" + marker);
                Assert.Equal("Alex · atualizado", (await member.Get("/profile")).GetProperty("displayName").GetString());
                Assert.Equal(2, (await member.Get("/support/" + ticketId)).GetProperty("messages").GetArrayLength());
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?> configuration) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Testing").UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../portal")))
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration))
            .ConfigureLogging(logging => logging.AddProvider(new TestLogProvider(output))));
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost/portal/"), AllowAutoRedirect = false, HandleCookies = true });
    private static async Task<string> Csrf(HttpClient client) => (await client.GetFromJsonAsync<JsonElement>("api/session")).GetProperty("csrfToken").GetString()!;
    private static async Task<Session> Login(WebApplicationFactory<Program> factory, string username)
    {
        var client = Client(factory);
        var session = new Session(client, await Csrf(client));
        var response = await session.Raw("/auth/login", HttpMethod.Post, new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Secure-scadivo.portal="));
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        return new Session(client, await Csrf(client));
    }
    private static (string UserId, string Token) Invitation(JsonElement invitation)
    {
        var url = new Uri(invitation.GetProperty("invitationUrl").GetString()!);
        using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(url.Fragment[8..])));
        return (payload.RootElement.GetProperty("userId").GetString()!, payload.RootElement.GetProperty("token").GetString()!);
    }
    private sealed record Session(HttpClient Client, string Token)
    {
        public async Task<JsonElement> Get(string path) => await Client.GetFromJsonAsync<JsonElement>("api" + path);
        public Task<HttpResponseMessage> Raw(string path, HttpMethod method, object body)
        {
            var request = new HttpRequestMessage(method, "api" + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-CSRF-TOKEN", Token);
            return Client.SendAsync(request);
        }
        public async Task<JsonElement> Send(string path, HttpMethod method, object body)
        {
            using var response = await Raw(path, method, body);
            Assert.True(response.IsSuccessStatusCode, $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
    }

    private sealed class TestLogProvider(ITestOutputHelper output) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(output, categoryName);
        public void Dispose() { }
    }
    private sealed class TestLogger(ITestOutputHelper output, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) output.WriteLine($"{level}: {category}: {formatter(state, exception)} {exception}");
        }
    }
}
