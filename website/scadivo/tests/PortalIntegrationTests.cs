using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
    private const string Password = "Scad1vo!";

    [Fact]
    public void AutomaticEmailsDirectCustomersToSupportWithoutReplyingToNoReply()
    {
        var settings = new PortalEmailOptions();
        using var portuguese = PortalEmailDispatcher.CreateMessage(settings, Guid.NewGuid(), "visitor@example.test",
            "Confirme seu e-mail SCADIVO", "Olá, Bruno.");
        Assert.Contains("Não responda a este e-mail.", portuguese.TextBody);
        Assert.Equal(1, portuguese.TextBody!.Split("Não responda a este e-mail.", StringSplitOptions.None).Length - 1);
        Assert.Contains("https://scadivo.com/contato?area=suporte", portuguese.TextBody);
        var decodedHtml = WebUtility.HtmlDecode(portuguese.HtmlBody!);
        Assert.Equal(1, decodedHtml.Split("Não responda a este e-mail.", StringSplitOptions.None).Length - 1);
        Assert.Contains("href=\"https://scadivo.com/contato?area=suporte\"", portuguese.HtmlBody);
        Assert.Contains("<!--[if mso]>", portuguese.HtmlBody);
        Assert.Contains("width=\"640\"", portuguese.HtmlBody);
        Assert.Contains("max-width:640px", portuguese.HtmlBody);
        Assert.Empty(portuguese.ReplyTo);

        using var staffNotification = PortalEmailDispatcher.CreateMessage(settings, Guid.NewGuid(), "support@scadivo.com",
            "Novo contato", "Responda a este e-mail para falar com a pessoa.", "visitor@example.test", replyToSubmitter: true);
        Assert.Equal("visitor@example.test", staffNotification.ReplyTo.Mailboxes.Single().Address);
        Assert.DoesNotContain("Não responda a este e-mail", staffNotification.TextBody);
        Assert.DoesNotContain("/contato?area=suporte", staffNotification.HtmlBody);
    }

    [Fact]
    public void AutomaticEmailLayoutKeepsLongAccountLinksReadableAndConstrained()
    {
        var settings = new PortalEmailOptions();
        var resetLink = "https://scadivo.com/portal/#reset-password=" + new string('a', 220);
        using var reset = PortalEmailDispatcher.CreateMessage(settings, Guid.NewGuid(), "visitor@example.test",
            "Recuperação de acesso SCADIVO", $"Olá. Para redefinir sua senha, acesse:\n{resetLink}", locale: "pt-BR");
        using var confirmation = PortalEmailDispatcher.CreateMessage(settings, Guid.NewGuid(), "visitor@example.test",
            "Confirm your SCADIVO email", $"Confirm your email using this link:\n{resetLink}", locale: "en");

        Assert.Contains(resetLink, reset.TextBody);
        Assert.Contains($"href=\"{resetLink}\"", reset.HtmlBody);
        Assert.DoesNotContain($">{resetLink}</a>", reset.HtmlBody);
        Assert.Contains("Redefinir minha senha", reset.HtmlBody);
        Assert.Contains("Confirm my email", confirmation.HtmlBody);
        Assert.Contains("width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;max-width:640px", reset.HtmlBody);
        Assert.Contains("width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;max-width:640px", confirmation.HtmlBody);

        using var spanish = PortalEmailDispatcher.CreateMessage(settings, Guid.NewGuid(), "visitante@example.test",
            "Recuperación de acceso a SCADIVO", $"Hola. Restablece tu contraseña aquí:\n{resetLink}", locale: "es");
        Assert.Contains("No respondas a este correo electrónico.", spanish.TextBody);
        Assert.Contains("https://scadivo.com/contact?area=support&amp;lang=es", spanish.HtmlBody);
        Assert.Contains("Restablecer mi contraseña", WebUtility.HtmlDecode(spanish.HtmlBody));
        Assert.Contains("SCADA Y AUTOMATIZACIÓN INDUSTRIAL", WebUtility.HtmlDecode(spanish.HtmlBody));
        Assert.Contains("lang=\"es\"", spanish.HtmlBody);
    }

    [Fact]
    public async Task DiscourseConnectUsesConfirmedPortalIdentityAndDoesNotExposeProfileOrRoles()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-community-sso-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "owner");
        var secretFile = Path.Combine(root, "community-secret");
        const string password = Password;
        const string secret = "test-only-discourse-connect-secret-with-more-than-32-bytes";
        await File.WriteAllTextAsync(passwordFile, password);
        await File.WriteAllTextAsync(secretFile, secret);
        var connection = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
            ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!";
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = connection,
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Portal:Community:Enabled"] = "true",
            ["Portal:Community:DiscourseUrl"] = "https://community.example.test",
            ["Portal:Community:SharedSecretFile"] = secretFile,
            ["Logging:LogLevel:Default"] = "Warning"
        };

        try
        {
            await using var factory = Factory(configuration);
            var marker = Guid.NewGuid().ToString("N")[..8];
            var email = $"community-{marker}@example.test";
            string userId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                var user = new PortalUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    InvitationAccepted = true,
                    DisplayName = "Private Legal Name",
                    Locale = "es",
                    ProfileType = "student",
                    ActivityArea = "education-research",
                    OrganizationOrSchool = "Private School Name"
                };
                var created = await users.CreateAsync(user, password);
                Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Code)));
                var memberRole = await users.AddToRoleAsync(user, "Member");
                Assert.True(memberRole.Succeeded, string.Join(", ", memberRole.Errors.Select(error => error.Code)));
                userId = user.Id;
            }

            using var anonymous = Client(factory);
            using (var anonymousResponse = await anonymous.GetAsync("api/community/sso"))
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            var member = await Login(factory, email);
            var session = await member.Client.GetFromJsonAsync<JsonElement>("api/session");
            Assert.True(session.GetProperty("communityEnabled").GetBoolean());
            Assert.Equal("https://community.example.test/session/sso", session.GetProperty("communityStartUrl").GetString());

            var nonce = "nonce-" + marker;
            var callback = "https://community.example.test/session/sso_login";
            var challenge = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"nonce={Uri.EscapeDataString(nonce)}&return_sso_url={Uri.EscapeDataString(callback)}"));
            var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(challenge))).ToLowerInvariant();
            using var response = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = signature
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var redirect = response.Headers.Location!.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(new Uri("https://community.example.test"), response.Headers.Location);
            Assert.Equal(callback, redirect.GetLeftPart(UriPartial.Path));

            var returned = QueryHelpers.ParseQuery(redirect.Query);
            var returnedSso = returned["sso"].ToString();
            var returnedSignature = returned["sig"].ToString();
            var expectedSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(returnedSso))).ToLowerInvariant();
            Assert.Equal(expectedSignature, returnedSignature);

            var fields = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(Convert.FromBase64String(returnedSso)));
            Assert.Equal(nonce, fields["nonce"].ToString());
            Assert.Equal(email, fields["email"].ToString());
            Assert.Equal(userId, fields["external_id"].ToString());
            Assert.StartsWith("scadivo_", fields["username"].ToString());
            Assert.Equal("es", fields["locale"].ToString());
            Assert.False(fields.ContainsKey("name"));
            Assert.False(fields.ContainsKey("admin"));
            Assert.False(fields.ContainsKey("moderator"));
            Assert.False(fields.ContainsKey("groups"));
            Assert.DoesNotContain("Private Legal Name", returnedSso);
            Assert.DoesNotContain("Private School Name", returnedSso);

            using var invalidSignature = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = new string('0', 64)
            }));
            Assert.Equal(HttpStatusCode.BadRequest, invalidSignature.StatusCode);

            var externalCallback = "https://attacker.example/session/sso_login";
            var externalChallenge = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"nonce={Uri.EscapeDataString(nonce)}&return_sso_url={Uri.EscapeDataString(externalCallback)}"));
            var externalSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(externalChallenge))).ToLowerInvariant();
            using var externalRedirect = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = externalChallenge,
                ["sig"] = externalSignature
            }));
            Assert.Equal(HttpStatusCode.BadRequest, externalRedirect.StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                var account = await users.FindByIdAsync(userId);
                Assert.NotNull(account);
                account.EmailConfirmed = false;
                Assert.True((await users.UpdateAsync(account)).Succeeded);
            }
            using (var unconfirmedEmail = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = signature
            })))
                Assert.Equal(HttpStatusCode.Forbidden, unconfirmedEmail.StatusCode);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

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
                var anonymousSession = await anonymous.GetFromJsonAsync<JsonElement>("api/session");
                Assert.False(anonymousSession.GetProperty("publicRegistrationEnabled").GetBoolean());
                Assert.False(anonymousSession.GetProperty("communityEnabled").GetBoolean());
                Assert.Equal(JsonValueKind.Null, anonymousSession.GetProperty("communityStartUrl").ValueKind);
                var closedSignup = new Session(anonymous, await Csrf(anonymous));
                using (var registrationClosed = await closedSignup.Raw("/auth/register", HttpMethod.Post, new { email = "closed@example.test" }))
                    Assert.Equal(HttpStatusCode.NotFound, registrationClosed.StatusCode);
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
                    locale = "es",
                    profileType = "professional", activityArea = "industrial-automation", activityAreaOther = (string?)null,
                    organizationOrSchool = "Example Integration", phoneNumber = (string?)null, address = (string?)null,
                    dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false });
                Assert.Equal(HttpStatusCode.BadRequest, (await invited.Raw("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password })).StatusCode);
                var member = await Login(factory, "member-" + marker);
                Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("api/admin/accounts")).StatusCode);
                Assert.False((await member.Get("/profile")).GetProperty("owner").GetBoolean());
                Assert.Equal("es", (await member.Get("/profile")).GetProperty("locale").GetString());
                await member.Send("/profile", HttpMethod.Put, new { displayName = "Alex · atualizado", locale = "en" });
                Assert.Equal("Alex · atualizado", (await member.Get("/profile")).GetProperty("displayName").GetString());
                var faq = await member.Get("/faq?q=200&lang=en");
                Assert.Contains(faq.EnumerateArray(), article => article.GetProperty("title").GetString()!.Contains("200"));
                var faqSpanish = await member.Get("/faq?q=200&lang=es");
                Assert.Contains(faqSpanish.EnumerateArray(), article => article.GetProperty("title").GetString()!.Contains("200"));
                Assert.Equal(0, (await member.Get("/downloads")).GetArrayLength());
                var ticket = await member.Send("/support", HttpMethod.Post, new { subject = "Test " + marker, category = "technical", body = "<script>alert('xss')</script> textual support example" });
                ticketId = ticket.GetProperty("id").GetGuid();

                var otherInvite = await owner.Send("/admin/invitations", HttpMethod.Post, new { username = "other-" + marker, displayName = "Taylor" });
                var otherPayload = Invitation(otherInvite);
                await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { otherPayload.UserId, otherPayload.Token, password = Password,
                    locale = "pt-BR",
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
                    subjectPt = "Olá {{name}}", subjectEn = "Hello {{name}}", subjectEs = "Hola {{name}}",
                    bodyPt = "Referência {{reference}}", bodyEn = "Reference {{reference}}", bodyEs = "Referencia {{reference}}"
                });
                var article = await owner.Send("/admin/faq", HttpMethod.Post, new { slug = "qa-" + marker, category = "test", titlePt = "Pergunta teste", titleEn = "Test question", titleEs = "Pregunta de prueba", bodyPt = "Resposta teste", bodyEn = "Test answer", bodyEs = "Respuesta de prueba", published = true });
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
            Assert.Empty(email.ReplyTo);
            Assert.Equal($"{queued.Id:N}@scadivo.com", email.MessageId);
            Assert.Contains("<table role=\"presentation\"", email.HtmlBody);
            Assert.Contains("lang=\"en\"", email.HtmlBody);
            var textBody = Assert.IsType<string>(email.TextBody);
            Assert.Contains("Please do not reply to this email.", textBody);
            Assert.Contains("https://scadivo.com/contact?area=support&lang=en", textBody);
            var link = textBody.Split('\n').Select(x => x.Trim()).Single(x => x.StartsWith("https://localhost/portal/?lang=en#invite=", StringComparison.Ordinal));
            Assert.Contains($"<a href=\"{link}\"", email.HtmlBody);
            Assert.Contains("href=\"https://scadivo.com/contact?area=support&amp;lang=en\"", email.HtmlBody);
            var payload = Invitation(JsonSerializer.SerializeToElement(new { invitationUrl = link }));
            using var anonymous = Client(factory);
            var invited = new Session(anonymous, await Csrf(anonymous));
            await invited.Send("/auth/accept-invitation", HttpMethod.Post, new { payload.UserId, payload.Token, password = Password,
                locale = "en",
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
            Assert.DoesNotContain("Please do not reply to this email.", staffNotice.TextBody);
            Assert.Contains("&lt;script&gt;", staffNotice.HtmlBody);
            Assert.DoesNotContain("<script>", staffNotice.HtmlBody);
            var receipt = Assert.Single(contactMessages, message => message.To.Mailboxes.Single().Address == "visitor@example.test");
            Assert.Empty(receipt.ReplyTo);
            Assert.Contains(reference, receipt.TextBody);
            Assert.Contains("Please do not reply to this email.", receipt.TextBody);
            Assert.Contains("https://scadivo.com/contact?area=support&lang=en", receipt.TextBody);
            Assert.Contains("href=\"https://scadivo.com/contact?area=support&amp;lang=en\"", receipt.HtmlBody);
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
            Assert.True((await registrationClient.GetFromJsonAsync<JsonElement>("api/session")).GetProperty("publicRegistrationEnabled").GetBoolean());
            var registrationEmail = $"signup-{Guid.NewGuid():N}@example.test";
            using (var missingProfileResponse = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                displayName = "New Portal User", email = registrationEmail, password = Password,
                locale = "es",
                profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                organizationOrSchool = (string?)null, phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false
            }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, missingProfileResponse.StatusCode);
                Assert.False(await db.Users.AnyAsync(x => x.Email == registrationEmail));
            }
            using (var shortPasswordResponse = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                displayName = "New Portal User", email = registrationEmail, password = "Scad1vo",
                locale = "es",
                profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                organizationOrSchool = "Technical University", phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false
            }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, shortPasswordResponse.StatusCode);
                using var error = await JsonDocument.ParseAsync(await shortPasswordResponse.Content.ReadAsStreamAsync());
                Assert.Equal("password-requirements", error.RootElement.GetProperty("error").GetString());
                Assert.False(await db.Users.AnyAsync(x => x.Email == registrationEmail));
            }
            using var registrationResponse = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                displayName = "New Portal User", email = registrationEmail, password = Password,
                locale = "es",
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
            await db.SaveChangesAsync();
            Assert.Equal("es", pendingAccount.Locale);
            var pendingConfirmation = await db.EmailOutbox.SingleAsync(x => x.UserId == pendingAccount.Id && x.TemplateKey == "account.confirmation");
            Assert.Null(pendingConfirmation.TemplateDataJson); // The one-time token is generated only during delivery.
            using (var blockedLogin = await registrationSession.Raw("/auth/login", HttpMethod.Post, new { username = registrationEmail, password = Password }))
                Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);
            using (var pendingRecovery = await registrationSession.Raw("/auth/password-recovery", HttpMethod.Post, new { email = registrationEmail }))
            {
                Assert.Equal(HttpStatusCode.Accepted, pendingRecovery.StatusCode);
                Assert.Equal("check-email", (await pendingRecovery.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
            }
            Assert.Empty(await db.EmailOutbox.Where(x => x.UserId == pendingAccount.Id && x.TemplateKey == "account.password-reset").ToListAsync());
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(6, transport.Messages.Count);
            using var confirmationMessage = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Last())));
            Assert.Empty(confirmationMessage.ReplyTo);
            Assert.Contains("No respondas a este correo electrónico.", confirmationMessage.TextBody);
            Assert.Contains("https://scadivo.com/contact?area=support&lang=es", confirmationMessage.TextBody);
            Assert.Contains("href=\"https://scadivo.com/contact?area=support&amp;lang=es\"", confirmationMessage.HtmlBody);
            var confirmationUrl = Assert.IsType<string>(confirmationMessage.TextBody).Split('\n').Select(x => x.Trim())
                .Single(x => x.StartsWith("https://localhost/portal/?lang=es#confirm-email=", StringComparison.Ordinal));
            Assert.Equal("es", QueryHelpers.ParseQuery(new Uri(confirmationUrl).Query)["lang"]);
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
            var legacyRecoveryTemplate = await db.EmailTemplates.SingleAsync(x => x.Key == "account.password-reset");
            legacyRecoveryTemplate.BodyEn = "Hello {{name}}. To reset your password, use: {{link}}. If you did not request this, ignore this message.";
            await db.SaveChangesAsync(); // Models an already-seeded production template that predates the new copy.

            using var recoveryClient = Client(factory);
            var recoverySession = new Session(recoveryClient, await Csrf(recoveryClient));
            using var unknownRecovery = await recoverySession.Raw("/auth/password-recovery", HttpMethod.Post, new { email = "unknown@example.test" });
            using var knownRecovery = await recoverySession.Raw("/auth/password-recovery", HttpMethod.Post, new { email = registrationEmail });
            using var repeatedRecovery = await recoverySession.Raw("/auth/password-recovery", HttpMethod.Post, new { email = registrationEmail });
            Assert.Equal(HttpStatusCode.Accepted, unknownRecovery.StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, knownRecovery.StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, repeatedRecovery.StatusCode);
            Assert.Equal(await unknownRecovery.Content.ReadAsStringAsync(), await knownRecovery.Content.ReadAsStringAsync());
            Assert.Equal(await knownRecovery.Content.ReadAsStringAsync(), await repeatedRecovery.Content.ReadAsStringAsync());
            var resetOutbox = await db.EmailOutbox.SingleAsync(x => x.UserId == pendingAccount.Id && x.TemplateKey == "account.password-reset");
            Assert.Null(resetOutbox.TemplateDataJson); // Reset token is generated only while sending, never stored.
            Assert.Equal(pendingAccount.Id, resetOutbox.Reference);
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(7, transport.Messages.Count);
            using var resetMessage = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Last())));
            Assert.Equal("noreply@scadivo.com", resetMessage.From.Mailboxes.Single().Address);
            Assert.Empty(resetMessage.ReplyTo);
            Assert.Contains("No respondas a este correo electrónico.", resetMessage.TextBody);
            Assert.Contains("caduca en 24 horas", resetMessage.TextBody);
            Assert.Contains("https://scadivo.com/contact?area=support&lang=es", resetMessage.TextBody);
            var resetMarker = "https://localhost/portal/?lang=es#reset-password=";
            var resetLine = Assert.IsType<string>(resetMessage.TextBody).Split('\n').Select(x => x.Trim())
                .Single(x => x.Contains(resetMarker, StringComparison.Ordinal));
            var resetStart = resetLine.IndexOf(resetMarker, StringComparison.Ordinal);
            var resetEnd = resetLine.IndexOfAny([' ', '\t', '.'], resetStart);
            var resetUrl = resetLine[resetStart..(resetEnd < 0 ? resetLine.Length : resetEnd)].TrimEnd(',', ';');
            Assert.Equal(1, resetMessage.TextBody.Split(resetUrl, StringSplitOptions.None).Length - 1);
            using var resetPayload = PasswordReset(resetUrl);
            var resetData = new { userId = resetPayload.RootElement.GetProperty("userId").GetString(), token = resetPayload.RootElement.GetProperty("token").GetString() };
            using (var weakReset = await recoverySession.Raw("/auth/reset-password", HttpMethod.Post, new { resetData.userId, resetData.token, password = "weakpassword" }))
            {
                Assert.Equal(HttpStatusCode.BadRequest, weakReset.StatusCode);
                Assert.Equal("password-requirements", (await weakReset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
            }
            await recoverySession.Send("/auth/reset-password", HttpMethod.Post, new { resetData.userId, resetData.token, password = "NewScadivo2!" });
            using (var reusedReset = await recoverySession.Raw("/auth/reset-password", HttpMethod.Post, new { resetData.userId, resetData.token, password = "AnotherScadivo2!" }))
                Assert.Equal(HttpStatusCode.BadRequest, reusedReset.StatusCode);
            using (var oldPasswordLogin = await recoverySession.Raw("/auth/login", HttpMethod.Post, new { username = registrationEmail, password = Password }))
                Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
            using (var newPasswordLogin = await recoverySession.Raw("/auth/login", HttpMethod.Post, new { username = registrationEmail, password = "NewScadivo2!" }))
                Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
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
            using var serialized = new MemoryStream();
            await message.WriteToAsync(serialized, cancellationToken);
            Messages.Add(Encoding.UTF8.GetString(serialized.ToArray()));
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
    private static JsonDocument PasswordReset(string url)
    {
        var uri = new Uri(url);
        return JsonDocument.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(uri.Fragment[16..])));
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
