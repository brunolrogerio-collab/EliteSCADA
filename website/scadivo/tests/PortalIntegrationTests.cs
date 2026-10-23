using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Scadivo.Portal.Data;
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
                Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "keys"), "key-*.xml"));
                Assert.True((await owner.Get("/admin/overview")).GetProperty("accounts").GetInt32() >= 1);
                var sessionResponse = await owner.Client.GetAsync("api/session");
                Assert.Contains("no-store", sessionResponse.Headers.CacheControl!.ToString());
                Assert.Contains("frame-ancestors 'none'", sessionResponse.Headers.GetValues("Content-Security-Policy").Single());

                var invitation = await owner.Send("/admin/invitations", HttpMethod.Post, new { username = "member-" + marker, displayName = "Alex", organization = "Example Integration" });
                Assert.False(invitation.GetProperty("emailDeliveryConfigured").GetBoolean());
                var payload = Invitation(invitation);
                var invited = new Session(anonymous, await Csrf(anonymous));
                await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password,
                    profileType = "professional", activityArea = "industrial-automation", activityAreaOther = (string?)null,
                    organizationOrSchool = "Example Integration", phoneNumber = (string?)null, address = (string?)null,
                    dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false });
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
                await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { otherPayload.UserId, otherPayload.Token, password = Password,
                    profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                    organizationOrSchool = "Example School", phoneNumber = (string?)null, address = (string?)null,
                    dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false });
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

    [Fact]
    public async Task EmailQueueRetriesWithoutLeakingTokensAndSkipsLockedOrHistoricalNotifications()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-email-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "test-credential");
        await File.WriteAllTextAsync(passwordFile, Password);
        var transport = new RecordingEmailTransport { Failures = 1 };
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
                ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!",
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Portal:PublicUrl"] = "https://localhost/portal/",
            ["Portal:PublicRegistrationEnabled"] = "true",
            ["Portal:Email:Enabled"] = "true",
            ["Portal:Email:Host"] = "localhost",
            ["Portal:Email:Username"] = "noreply@scadivo.com",
            ["Portal:Email:PasswordFile"] = passwordFile,
            ["Logging:LogLevel:Default"] = "Warning"
        };
        try
        {
            await using var factory = Factory(configuration).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPortalEmailTransport>();
                services.AddSingleton<IPortalEmailTransport>(transport);
                var worker = services.Single(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(PortalEmailWorker));
                services.Remove(worker); // Pump deterministically; production keeps the hosted worker.
            }));
            var owner = await Login(factory, "scadivo.owner");
            var marker = Guid.NewGuid().ToString("N")[..8];
            var invitation = await owner.Send("/admin/invitations", HttpMethod.Post,
                new { username = "mail-" + marker, displayName = "Alex", email = $"qa-{marker}@example.test" });
            Assert.True(invitation.GetProperty("emailDeliveryConfigured").GetBoolean());
            Assert.True(invitation.GetProperty("emailQueued").GetBoolean());
            var userId = invitation.GetProperty("id").GetString()!;
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
            var dispatcher = scope.ServiceProvider.GetRequiredService<PortalEmailDispatcher>();
            (await db.Users.SingleAsync(x => x.Id == userId)).Locale = "en";
            var template = await db.EmailTemplates.SingleAsync(x => x.Key == "account.invitation");
            template.SubjectEn = "Welcome {{name}}";
            template.BodyEn = "Hello {{name}}, your invitation is ready."; // Legacy template omits {{link}}.
            var historical = new EmailOutbox { UserId = userId, TemplateKey = "account.invitation", Reference = userId, CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
            db.EmailOutbox.Add(historical);
            await db.SaveChangesAsync();

            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            var queued = await db.EmailOutbox.SingleAsync(x => x.UserId == userId && x.Id != historical.Id);
            Assert.Equal("retry", queued.Status);
            Assert.Equal(1, queued.Attempts);
            Assert.Equal("smtp-delivery", queued.LastErrorCode);
            Assert.Null(queued.SentAtUtc);
            Assert.Empty(transport.Messages);
            Assert.False(await dispatcher.ProcessNextAsync(CancellationToken.None));
            queued.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();

            transport.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var delivery = dispatcher.ProcessNextAsync(CancellationToken.None);
            await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using (var other = factory.Services.CreateAsyncScope())
                Assert.False(await other.ServiceProvider.GetRequiredService<PortalEmailDispatcher>().ProcessNextAsync(CancellationToken.None));
            transport.Release.SetResult();
            Assert.True(await delivery);
            transport.Entered = transport.Release = null;
            Assert.Equal("sent", queued.Status);
            Assert.Equal(2, queued.Attempts);
            Assert.NotNull(queued.SentAtUtc);
            Assert.Null(queued.LastErrorCode);
            Assert.Equal("awaiting-transport", historical.Status);
            Assert.Equal(userId, queued.Reference); // No invitation token persisted in the outbox.

            using var email = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Single())));
            Assert.Equal("Welcome Alex", email.Subject);
            Assert.Equal("noreply@scadivo.com", email.From.Mailboxes.Single().Address);
            Assert.Equal("support@scadivo.com", email.ReplyTo.Mailboxes.Single().Address);
            Assert.Equal($"{queued.Id:N}@scadivo.com", email.MessageId);
            Assert.Contains("<table role=\"presentation\"", email.HtmlBody);
            Assert.Contains("lang=\"en\"", email.HtmlBody);
            var textBody = Assert.IsType<string>(email.TextBody);
            var link = textBody.Split('\n').Select(x => x.Trim()).Single(x => x.StartsWith("https://localhost/portal/#invite=", StringComparison.Ordinal));
            Assert.Contains($"<a href=\"{link}\"", email.HtmlBody);
            var payload = Invitation(JsonSerializer.SerializeToElement(new { invitationUrl = link }));
            using var anonymous = Client(factory);
            var invited = new Session(anonymous, await Csrf(anonymous));
            await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password,
                profileType = "professional", activityArea = "industrial-automation", activityAreaOther = (string?)null,
                organizationOrSchool = "Email Test Integration", phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false });
            Assert.Equal(HttpStatusCode.BadRequest, (await invited.Raw("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password })).StatusCode);
            Assert.False(await dispatcher.ProcessNextAsync(CancellationToken.None));

            var member = await Login(factory, "mail-" + marker);
            var ticket = await member.Send("/support", HttpMethod.Post, new { subject = "Email test", category = "technical", body = "A private support request for delivery verification." });
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            await owner.Send("/support/" + ticket.GetProperty("id").GetGuid() + "/messages", HttpMethod.Post, new { body = "Private staff reply is not copied into email." });
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(3, transport.Messages.Count);
            Assert.DoesNotContain("Private staff reply", transport.Messages.Last());
            Assert.True((await owner.Get("/admin/overview")).GetProperty("sentMessages").GetInt32() >= 3);

            var usersBeforeContact = await db.Users.CountAsync();
            using var contactClient = Client(factory);
            var publicContact = new Session(contactClient, await Csrf(contactClient));
            using var contactResponse = await publicContact.Raw("/public/contact", HttpMethod.Post, new
            {
                name = "Demo Visitor", email = "visitor@example.test", category = "support", subjectKey = "technical-problem",
                message = "Please review this example: <script>alert('x')</script>", locale = "en", website = ""
            });
            Assert.Equal(HttpStatusCode.Accepted, contactResponse.StatusCode);
            var contactResult = await contactResponse.Content.ReadFromJsonAsync<JsonElement>();
            var reference = contactResult.GetProperty("reference").GetString()!;
            Assert.StartsWith("SC-", reference);
            Assert.Equal(usersBeforeContact, await db.Users.CountAsync()); // A public contact does not register an account.
            var contactOutbox = await db.EmailOutbox.Where(x => x.Reference == reference).ToListAsync();
            Assert.Equal(2, contactOutbox.Count);
            Assert.All(contactOutbox, item => Assert.Null(item.UserId));
            Assert.Contains(contactOutbox, item => item.TemplateKey == "contact.notification" && item.RecipientAddress == "support@scadivo.com");
            Assert.Contains(contactOutbox, item => item.TemplateKey == "contact.received" && item.RecipientAddress == "visitor@example.test");
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(5, transport.Messages.Count);
            var contactMessages = transport.Messages.Skip(3)
                .Select(raw => MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)))).ToArray();
            var staffNotice = Assert.Single(contactMessages, message => message.To.Mailboxes.Single().Address == "support@scadivo.com");
            Assert.Equal("visitor@example.test", staffNotice.ReplyTo.Mailboxes.Single().Address);
            Assert.Contains("Please review this example", staffNotice.TextBody);
            Assert.Contains("&lt;script&gt;", staffNotice.HtmlBody);
            Assert.DoesNotContain("<script>", staffNotice.HtmlBody);
            var receipt = Assert.Single(contactMessages, message => message.To.Mailboxes.Single().Address == "visitor@example.test");
            Assert.Equal("support@scadivo.com", receipt.ReplyTo.Mailboxes.Single().Address);
            Assert.Contains(reference, receipt.TextBody);
            Assert.Contains("SCADIVO", receipt.HtmlBody);
            Assert.All(await db.EmailOutbox.Where(x => x.Reference == reference).ToListAsync(), item =>
            {
                Assert.Equal("sent", item.Status);
                Assert.Null(item.TemplateDataJson);
                Assert.Null(item.RecipientAddress);
                Assert.Null(item.ReplyToAddress);
            });

            using var honeypotClient = Client(factory);
            var honeypot = new Session(honeypotClient, await Csrf(honeypotClient));
            using var ignoredContact = await honeypot.Raw("/public/contact", HttpMethod.Post, new
            {
                name = "Bot", email = "bot@example.test", category = "contact", subjectKey = "general-info",
                message = "Automated spam", locale = "en", website = "http://spam.invalid"
            });
            Assert.Equal(HttpStatusCode.Accepted, ignoredContact.StatusCode);
            Assert.Equal(2, await db.EmailOutbox.CountAsync(x => x.Reference == reference));

            using var registrationClient = Client(factory);
            var registrationSession = new Session(registrationClient, await Csrf(registrationClient));
            var registrationEmail = $"signup-{Guid.NewGuid():N}@example.test";
            using var registrationResponse = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                displayName = "New Portal User", email = registrationEmail, password = Password,
                profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                organizationOrSchool = "Technical University", phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false
            });
            Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);
            var pendingAccount = await db.Users.SingleAsync(x => x.Email == registrationEmail);
            Assert.False(pendingAccount.EmailConfirmed);
            Assert.False(pendingAccount.InvitationAccepted);
            Assert.False(pendingAccount.MarketingEmailOptIn);
            Assert.Null(pendingAccount.DateOfBirth); // Optional birth date does not block registration.
            pendingAccount.Locale = "en";
            await db.SaveChangesAsync();
            var pendingConfirmation = await db.EmailOutbox.SingleAsync(x => x.UserId == pendingAccount.Id && x.TemplateKey == "account.confirmation");
            Assert.Null(pendingConfirmation.TemplateDataJson); // The one-time token is generated only during delivery.
            using (var blockedLogin = await registrationSession.Raw("/auth/login", HttpMethod.Post, new { username = registrationEmail, password = Password }))
                Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(6, transport.Messages.Count);
            using var confirmationMessage = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Last())));
            var confirmationUrl = Assert.IsType<string>(confirmationMessage.TextBody).Split('\n').Select(x => x.Trim())
                .Single(x => x.StartsWith("https://localhost/portal/#confirm-email=", StringComparison.Ordinal));
            using var confirmationPayload = Confirmation(confirmationUrl);
            using var confirmationClient = Client(factory);
            var confirmationSession = new Session(confirmationClient, await Csrf(confirmationClient));
            var confirmationData = new { userId = confirmationPayload.RootElement.GetProperty("userId").GetString(), token = confirmationPayload.RootElement.GetProperty("token").GetString() };
            await confirmationSession.Send("/auth/confirm-email", HttpMethod.Post, confirmationData);
            await db.Entry(pendingAccount).ReloadAsync();
            Assert.True((await db.Users.SingleAsync(x => x.Id == pendingAccount.Id)).EmailConfirmed);
            using (var repeatedConfirmation = await confirmationSession.Raw("/auth/confirm-email", HttpMethod.Post, confirmationData))
                Assert.Equal(HttpStatusCode.BadRequest, repeatedConfirmation.StatusCode);
            var registered = await Login(factory, registrationEmail);
            var registeredProfile = await registered.Get("/profile");
            Assert.True(registeredProfile.GetProperty("profileComplete").GetBoolean());
            Assert.Equal("student", registeredProfile.GetProperty("profileType").GetString());
            Assert.Equal("Technical University", registeredProfile.GetProperty("organizationOrSchool").GetString());
            Assert.False(registeredProfile.GetProperty("marketingEmailOptIn").GetBoolean());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class RecordingEmailTransport : IPortalEmailTransport
    {
        public int Failures { get; set; }
        public List<string> Messages { get; } = [];
        public TaskCompletionSource? Entered { get; set; }
        public TaskCompletionSource? Release { get; set; }
        public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            if (Failures-- > 0) throw new IOException("Transient SMTP test failure.");
            Entered?.TrySetResult();
            if (Release is not null) await Release.Task.WaitAsync(cancellationToken);
            Messages.Add(message.ToString());
        }
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
    private static JsonDocument Confirmation(string url)
    {
        var uri = new Uri(url);
        return JsonDocument.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(uri.Fragment[15..])));
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
