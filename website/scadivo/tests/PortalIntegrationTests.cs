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
using Scadivo.Portal.Licensing;
using Xunit;
using Xunit.Abstractions;

namespace Scadivo.Portal.Tests;

public sealed class PortalIntegrationTests(ITestOutputHelper output)
{
    private const string Password = "Scad1vo!";

    [Fact]
    public void CommunitySsoAllowsHttpOnlyForLoopbackInDevelopment()
    {
        var secretFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(secretFile, new string('s', 32));
            var options = new CommunitySsoOptions
            {
                Enabled = true,
                DiscourseUrl = "http://localhost:18085",
                SharedSecretFile = secretFile
            };

            Assert.True(options.Valid(allowHttpLoopbackForDevelopment: true));
            Assert.False(options.Valid());

            options.DiscourseUrl = "http://community.example.test:18085";
            Assert.False(options.Valid(allowHttpLoopbackForDevelopment: true));
        }
        finally
        {
            File.Delete(secretFile);
        }
    }

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

            var nonce = "nonce-" + marker;
            var callback = "https://community.example.test/session/sso_login";
            var challenge = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"nonce={Uri.EscapeDataString(nonce)}&return_sso_url={Uri.EscapeDataString(callback)}"));
            var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(challenge))).ToLowerInvariant();

            using var anonymous = Client(factory);
            var challengeQuery = QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = signature
            });
            using (var anonymousResponse = await anonymous.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
                Assert.NotNull(anonymousResponse.Headers.Location);
                var loginUrl = new Uri(new Uri("https://localhost"), anonymousResponse.Headers.Location!);
                var loginFragment = loginUrl.Fragment;
                Assert.StartsWith("#community-login=", loginFragment);
                using var resume = JsonDocument.Parse(WebEncoders.Base64UrlDecode(loginFragment["#community-login=".Length..]));
                Assert.Equal(challenge, resume.RootElement.GetProperty("sso").GetString());
                Assert.Equal(signature, resume.RootElement.GetProperty("sig").GetString());
            }

            var anonymousSession = new Session(anonymous, await Csrf(anonymous));
            using (var loginResponse = await anonymousSession.Raw("/auth/login", HttpMethod.Post, new { username = email, password = Password }))
                Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            using (var resumedSso = await anonymous.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, resumedSso.StatusCode);
                Assert.NotNull(resumedSso.Headers.Location);
                Assert.Equal(callback, new Uri(new Uri("https://community.example.test"), resumedSso.Headers.Location).GetLeftPart(UriPartial.Path));
            }

            var member = await Login(factory, email);
            var session = await member.Client.GetFromJsonAsync<JsonElement>("api/session");
            Assert.True(session.GetProperty("communityEnabled").GetBoolean());
            Assert.Equal("https://community.example.test/session/sso", session.GetProperty("communityStartUrl").GetString());

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

            var returnedSsoPayload = Encoding.UTF8.GetString(Convert.FromBase64String(returnedSso));
            var fields = QueryHelpers.ParseQuery(returnedSsoPayload);
            Assert.Equal(nonce, fields["nonce"].ToString());
            Assert.Equal(email, fields["email"].ToString());
            Assert.Equal(userId, fields["external_id"].ToString());
            Assert.StartsWith("scadivo_", fields["username"].ToString());
            Assert.Equal("es", fields["locale"].ToString());
            Assert.Equal("true", fields["locale_force_update"].ToString());
            Assert.Equal("Private Legal Name", fields["name"].ToString());
            Assert.False(fields.ContainsKey("admin"));
            Assert.False(fields.ContainsKey("moderator"));
            Assert.False(fields.ContainsKey("groups"));
            Assert.DoesNotContain("Private School Name", returnedSsoPayload);

            var chosenUsername = "com-" + marker;
            var profileUpdate = await member.Send("/profile", HttpMethod.Put,
                new { username = chosenUsername, displayName = "Public Community Name", locale = "es" });
            Assert.Equal(chosenUsername, profileUpdate.GetProperty("userName").GetString());
            using var updatedSso = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = signature
            }));
            Assert.Equal(HttpStatusCode.Redirect, updatedSso.StatusCode);
            var updatedRedirect = updatedSso.Headers.Location!.IsAbsoluteUri
                ? updatedSso.Headers.Location
                : new Uri(new Uri("https://community.example.test"), updatedSso.Headers.Location);
            var updatedPayload = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(Convert.FromBase64String(QueryHelpers.ParseQuery(updatedRedirect.Query)["sso"].ToString())));
            Assert.Equal(chosenUsername, updatedPayload["username"].ToString());
            Assert.Equal("Public Community Name", updatedPayload["name"].ToString());
            Assert.Equal(email, updatedPayload["email"].ToString());
            Assert.DoesNotContain("Private School Name", string.Join("&", updatedPayload.Values.Select(value => value.ToString())));

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

            using (var logout = await member.Raw("/auth/logout", HttpMethod.Post, new { }))
                Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
            Assert.False((await member.Client.GetFromJsonAsync<JsonElement>("api/session")).GetProperty("authenticated").GetBoolean());

            using (var resumeAfterLogout = await member.Client.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, resumeAfterLogout.StatusCode);
                Assert.NotNull(resumeAfterLogout.Headers.Location);
                var loginUrl = new Uri(new Uri("https://localhost"), resumeAfterLogout.Headers.Location!);
                Assert.StartsWith("#community-login=", loginUrl.Fragment);
            }

            var reauthentication = new Session(member.Client, await Csrf(member.Client));
            using (var loginAgain = await reauthentication.Raw("/auth/login", HttpMethod.Post, new { username = email, password }))
                Assert.Equal(HttpStatusCode.OK, loginAgain.StatusCode);
            using (var forumAgain = await member.Client.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, forumAgain.StatusCode);
                Assert.NotNull(forumAgain.Headers.Location);
                var destination = forumAgain.Headers.Location!.IsAbsoluteUri
                    ? forumAgain.Headers.Location
                    : new Uri(new Uri("https://community.example.test"), forumAgain.Headers.Location);
                Assert.Equal(callback, destination.GetLeftPart(UriPartial.Path));
            }

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

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                var account = await users.FindByIdAsync(userId);
                Assert.NotNull(account);
                account.EmailConfirmed = true;
                account.LockoutEnabled = true;
                account.LockoutEnd = DateTimeOffset.UtcNow.AddHours(1);
                Assert.True((await users.UpdateAsync(account)).Succeeded);
            }
            using (var lockedAccount = await member.Client.GetAsync(QueryHelpers.AddQueryString("api/community/sso", new Dictionary<string, string?>
            {
                ["sso"] = challenge,
                ["sig"] = signature
            })))
                Assert.Equal(HttpStatusCode.Forbidden, lockedAccount.StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                var account = await users.FindByIdAsync(userId);
                Assert.NotNull(account);
                account.LockoutEnd = null;
                Assert.True((await users.UpdateAsync(account)).Succeeded);
            }
            using (var memberAdmin = await member.Raw($"/admin/accounts/{userId}/access", HttpMethod.Put, new { suspended = true }))
                Assert.Equal(HttpStatusCode.Forbidden, memberAdmin.StatusCode);
            var owner = await Login(factory, "scadivo.owner");
            var ownerProfile = await owner.Get("/profile");
            using (var protectOwner = await owner.Raw($"/admin/accounts/{ownerProfile.GetProperty("id").GetString()}/access", HttpMethod.Put, new { suspended = true }))
                Assert.Equal(HttpStatusCode.Conflict, protectOwner.StatusCode);
            using (var missingStatus = await owner.Raw($"/admin/accounts/{userId}/access", HttpMethod.Put, new { }))
                Assert.Equal(HttpStatusCode.BadRequest, missingStatus.StatusCode);

            // Keep another pre-suspension cookie untouched to prove that reactivation does not revive it.
            var dormantSession = await Login(factory, email);
            await owner.Send($"/admin/accounts/{userId}/access", HttpMethod.Put, new { suspended = true });
            var accounts = await owner.Get("/admin/accounts");
            Assert.True(accounts.EnumerateArray().Single(account => account.GetProperty("id").GetString() == userId).GetProperty("isSuspended").GetBoolean());
            using (var revokedProfile = await member.Client.GetAsync("api/profile"))
                Assert.Equal(HttpStatusCode.Unauthorized, revokedProfile.StatusCode);
            Assert.False((await member.Client.GetFromJsonAsync<JsonElement>("api/session")).GetProperty("authenticated").GetBoolean());
            using (var suspendedSso = await member.Client.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, suspendedSso.StatusCode);
                Assert.StartsWith("/portal/#community-login=", suspendedSso.Headers.Location!.OriginalString);
            }
            var suspendedLogin = new Session(member.Client, await Csrf(member.Client));
            using (var deniedLogin = await suspendedLogin.Raw("/auth/login", HttpMethod.Post, new { username = email, password }))
                Assert.Equal(HttpStatusCode.Unauthorized, deniedLogin.StatusCode);

            await owner.Send($"/admin/accounts/{userId}/access", HttpMethod.Put, new { suspended = false });
            using (var dormantCookie = await dormantSession.Client.GetAsync("api/profile"))
                Assert.Equal(HttpStatusCode.Unauthorized, dormantCookie.StatusCode);
            var reactivated = await Login(factory, email);
            using (var restoredSso = await reactivated.Client.GetAsync(challengeQuery))
            {
                Assert.Equal(HttpStatusCode.Redirect, restoredSso.StatusCode);
                Assert.Equal(callback, restoredSso.Headers.Location!.GetLeftPart(UriPartial.Path));
            }
            var restoredProfile = await reactivated.Get("/profile");
            Assert.Equal("Private School Name", restoredProfile.GetProperty("organizationOrSchool").GetString());
            var audit = await owner.Get("/admin/audit");
            Assert.Contains(audit.EnumerateArray(), item => item.GetProperty("action").GetString() == "account.suspended" && item.GetProperty("resourceId").GetString() == userId);
            Assert.Contains(audit.EnumerateArray(), item => item.GetProperty("action").GetString() == "account.reactivated" && item.GetProperty("resourceId").GetString() == userId);
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
                Assert.False((await owner.Get("/licensing/status")).GetProperty("onlineActivationAvailable").GetBoolean());
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
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            var ticketId = ticket.GetProperty("id").GetGuid();
            var ticketNotices = transport.Messages.Skip(1).Take(2)
                .Select(raw => MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)))).ToArray();
            var customerReceipt = Assert.Single(ticketNotices, message => message.To.Mailboxes.Single().Address != "support@scadivo.com");
            Assert.Contains("SCADIVO support request received", customerReceipt.Subject);
            var teamTicketNotice = Assert.Single(ticketNotices, message => message.To.Mailboxes.Single().Address == "support@scadivo.com");
            Assert.Contains(ticketId.ToString(), teamTicketNotice.TextBody);
            Assert.Contains("Técnico", teamTicketNotice.TextBody);
            Assert.Contains("https://localhost/portal/#support", teamTicketNotice.TextBody);
            Assert.DoesNotContain("A private support request for delivery verification.", teamTicketNotice.TextBody);
            await owner.Send("/support/" + ticket.GetProperty("id").GetGuid() + "/messages", HttpMethod.Post, new { body = "Private staff reply is not copied into email." });
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(4, transport.Messages.Count);
            Assert.DoesNotContain("Private staff reply", transport.Messages.Last());
            await member.Send("/support/" + ticketId + "/messages", HttpMethod.Post, new { body = "Private customer reply is not copied into the staff email either." });
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            Assert.Equal(5, transport.Messages.Count);
            var teamReplyNotice = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Last())));
            Assert.Equal("support@scadivo.com", teamReplyNotice.To.Mailboxes.Single().Address);
            Assert.Contains(ticketId.ToString(), teamReplyNotice.TextBody);
            Assert.Contains("https://localhost/portal/#support", teamReplyNotice.TextBody);
            Assert.DoesNotContain("Private customer reply is not copied into the staff email either.", teamReplyNotice.TextBody);
            Assert.True((await owner.Get("/admin/overview")).GetProperty("sentMessages").GetInt32() >= 5);

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
            Assert.Equal(7, transport.Messages.Count);
            var contactMessages = transport.Messages.Skip(5)
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
            var registrationUsername = "signup-" + Guid.NewGuid().ToString("N")[..12];
            using (var missingProfileResponse = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                username = registrationUsername, displayName = "New Portal User", email = registrationEmail, password = Password,
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
                username = registrationUsername, displayName = "New Portal User", email = registrationEmail, password = "Scad1vo",
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
                username = registrationUsername, displayName = "New Portal User", email = registrationEmail, password = Password,
                locale = "es",
                profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                organizationOrSchool = "Technical University", phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false
            });
            Assert.Equal(HttpStatusCode.Accepted, registrationResponse.StatusCode);
            var pendingAccount = await db.Users.SingleAsync(x => x.Email == registrationEmail);
            Assert.Equal(registrationUsername, pendingAccount.UserName);
            Assert.False(pendingAccount.EmailConfirmed);
            Assert.False(pendingAccount.InvitationAccepted);
            Assert.False(pendingAccount.MarketingEmailOptIn);
            Assert.Null(pendingAccount.DateOfBirth); // Optional birth date does not block registration.
            await db.SaveChangesAsync();
            Assert.Equal("es", pendingAccount.Locale);
            using (var duplicateUsername = await registrationSession.Raw("/auth/register", HttpMethod.Post, new
            {
                username = registrationUsername.ToUpperInvariant(), displayName = "Another User", email = $"other-{Guid.NewGuid():N}@example.test", password = Password,
                locale = "es", profileType = "student", activityArea = "education-research", activityAreaOther = (string?)null,
                organizationOrSchool = "Another University", phoneNumber = (string?)null, address = (string?)null,
                dateOfBirth = (DateOnly?)null, marketingEmailOptIn = false
            }))
            {
                Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);
                Assert.Equal("account-already-exists", (await duplicateUsername.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
            }
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
            Assert.Equal(8, transport.Messages.Count);
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
            Assert.Equal(registrationUsername, registeredProfile.GetProperty("userName").GetString());
            Assert.True(registeredProfile.GetProperty("profileComplete").GetBoolean());
            Assert.Equal("student", registeredProfile.GetProperty("profileType").GetString());
            Assert.Equal("Technical University", registeredProfile.GetProperty("organizationOrSchool").GetString());
            Assert.False(registeredProfile.GetProperty("marketingEmailOptIn").GetBoolean());
            var registeredByUsername = await Login(factory, registrationUsername);
            Assert.Equal(registrationEmail, (await registeredByUsername.Get("/profile")).GetProperty("email").GetString());
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
            Assert.Equal(9, transport.Messages.Count);
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

    [Fact]
    public async Task ExistingOwnerCanConfirmEmailWithoutLosingAccess()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-owner-confirmation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "test-credential");
        await File.WriteAllTextAsync(passwordFile, Password);
        var transport = new RecordingEmailTransport();
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
                ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!",
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Portal:PublicUrl"] = "https://localhost/portal/",
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
                services.Remove(worker);
            }));
            var email = $"existing-owner-{Guid.NewGuid():N}@example.test";
            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
            var user = new PortalUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                InvitationAccepted = true,
                DisplayName = "Existing Owner",
                Locale = "pt-BR"
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Owner")).Succeeded);

            // An existing owner stays signed in and can use administration while the new email is pending confirmation.
            var ownerSession = await Login(factory, email);
            Assert.True((await ownerSession.Get("/admin/overview")).GetProperty("accounts").GetInt32() >= 1);

            using var resendClient = Client(factory);
            var resendSession = new Session(resendClient, await Csrf(resendClient));
            using var resendResponse = await resendSession.Raw("/auth/confirmation/resend", HttpMethod.Post, new { email });
            Assert.Equal(HttpStatusCode.Accepted, resendResponse.StatusCode);
            Assert.Equal("check-email", (await resendResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

            var confirmation = await scope.ServiceProvider.GetRequiredService<PortalDb>().EmailOutbox
                .SingleAsync(x => x.UserId == user.Id && x.TemplateKey == "account.confirmation");
            Assert.Null(confirmation.TemplateDataJson);
            var dispatcher = scope.ServiceProvider.GetRequiredService<PortalEmailDispatcher>();
            Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
            using var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(transport.Messages.Single())));
            Assert.Equal(email, message.To.Mailboxes.Single().Address);
            var confirmationUrl = Assert.IsType<string>(message.TextBody).Split('\n').Select(x => x.Trim())
                .Single(x => x.StartsWith("https://localhost/portal/#confirm-email=", StringComparison.Ordinal));
            using var payload = Confirmation(confirmationUrl);

            using var confirmClient = Client(factory);
            var confirmSession = new Session(confirmClient, await Csrf(confirmClient));
            await confirmSession.Send("/auth/confirm-email", HttpMethod.Post, new
            {
                userId = payload.RootElement.GetProperty("userId").GetString(),
                token = payload.RootElement.GetProperty("token").GetString()
            });
            await scope.ServiceProvider.GetRequiredService<PortalDb>().Entry(user).ReloadAsync();
            Assert.True(user.EmailConfirmed);
            Assert.True(user.InvitationAccepted);
            Assert.Contains("Owner", await users.GetRolesAsync(user));
            Assert.True((await ownerSession.Get("/admin/overview")).GetProperty("accounts").GetInt32() >= 1);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void OnlineLeaseSigningIsDisabledByDefault()
    {
        var options = new OnlineLeaseSigningOptions();
        Assert.False(options.Enabled);
        Assert.True(options.IsValid(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "scadivo-data-protection")));
        Assert.False(new OnlineLeaseSigningOptions { Enabled = true }.IsValid(
            Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "scadivo-data-protection")));
        using var signer = new RsaPssOnlineLeaseSigner(Microsoft.Extensions.Options.Options.Create(options));
        Assert.False(signer.IsEnabled);
        var now = DateTimeOffset.UtcNow;
        var claims = new OnlineLeaseClaims(1, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), new string('a', 64), "Tags500", 2, 2,
            false, false, now, now.AddHours(72), OnlineLeaseSigningOptions.ApprovedKeyId);
        Assert.Throws<InvalidOperationException>(() => signer.Sign(claims));
    }

    [Fact]
    public async Task OnlineLicenseActivationAndRenewalAreIdempotentAndDetectCredentialReuse()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-license-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "owner-password");
        await File.WriteAllTextAsync(passwordFile, Password);
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
                ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!",
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Logging:LogLevel:Default"] = "Warning"
        };

        try
        {
            await using var factory = Factory(configuration).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOnlineLeaseSigner>();
                services.AddSingleton<IOnlineLeaseSigner, TestOnlineLeaseSigner>();
            }));
            var owner = await Login(factory, "scadivo.owner");
            Assert.True((await owner.Get("/licensing/status")).GetProperty("onlineActivationAvailable").GetBoolean());
            var organizations = await owner.Get("/licensing/organizations");
            var organizationId = organizations.EnumerateArray().First().GetProperty("id").GetGuid();
            var marker = Guid.NewGuid().ToString("N")[..8];
            var application = await owner.Send($"/licensing/organizations/{organizationId}/applications", HttpMethod.Post,
                new { name = "Licensing test " + marker });
            var applicationId = application.GetProperty("id").GetGuid();
            var license = await owner.Send("/admin/licensing/licenses", HttpMethod.Post, new
            {
                applicationId,
                tier = "Tags500",
                interactiveSeats = 2,
                viewOnlySeats = 2,
                haRuntime = false,
                assetProcessIntelligence = false,
                term = "perpetual",
                maxActivations = 2
            });
            var licenseCode = license.GetProperty("publicCode").GetString()!;

            string userId;
            var username = "lease-" + marker;
            var email = $"lease-{marker}@example.test";
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                var user = new PortalUser
                {
                    UserName = username,
                    Email = email,
                    EmailConfirmed = true,
                    InvitationAccepted = true,
                    DisplayName = "Lease Test",
                    ProfileType = "student",
                    ActivityArea = "education-research",
                    OrganizationOrSchool = "Test University"
                };
                var created = await users.CreateAsync(user, Password);
                Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Code)));
                Assert.True((await users.AddToRoleAsync(user, "Member")).Succeeded);
                userId = user.Id;
                var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
                db.Memberships.Add(new Membership { OrganizationId = organizationId, UserId = user.Id, Role = "Member" });
                await db.SaveChangesAsync();
            }

            using var product = Client(factory);
            var activationId = Guid.NewGuid();
            var activationRequestId = Guid.NewGuid();
            var refresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var activationBody = new
            {
                username,
                password = Password,
                licenseCode,
                activationId,
                requestId = activationRequestId,
                machineFingerprint = new string('a', 64),
                refreshCredential = refresh
            };
            var prepareBody = new { username, password = Password, licenseCode, activationId, machineFingerprint = new string('a', 64) };
            async Task<string> PrepareToken()
            {
                using var response = await product.PostAsJsonAsync("api/licensing/device/prepare", prepareBody);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("online-account", json.GetProperty("summary").GetProperty("mode").GetString());
                return json.GetProperty("confirmationToken").GetString()!;
            }
            object Confirmation(string token, string fingerprint) => new { confirmationToken = token, activationId,
                requestId = activationRequestId, machineFingerprint = fingerprint, refreshCredential = refresh };
            var cancelledToken = await PrepareToken();
            await using (var scope = factory.Services.CreateAsyncScope())
                Assert.False(await scope.ServiceProvider.GetRequiredService<PortalDb>().ProductLicenseActivations.AnyAsync(x => x.Id == activationId));
            using var cancelled = await product.PostAsJsonAsync("api/licensing/device/cancel", new { confirmationToken = cancelledToken });
            Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
            using var cancelledConfirm = await product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(cancelledToken, new string('a', 64)));
            Assert.Equal(HttpStatusCode.Conflict, cancelledConfirm.StatusCode);
            var expiredToken = await PrepareToken();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
                // Use the only unconsumed, noncancelled preparation for this installation.
                var row = await db.ProductLicensePreparations.SingleAsync(x => x.ActivationId == activationId && !x.Cancelled);
                row.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            using var expiredConfirm = await product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(expiredToken, new string('a', 64)));
            Assert.Equal(HttpStatusCode.Conflict, expiredConfirm.StatusCode);
            var revokedToken = await PrepareToken();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
                var row = await db.ProductLicenses.SingleAsync(x => x.PublicCode == licenseCode);
                row.Status = ProductLicenseStates.Revoked;
                await db.SaveChangesAsync();
            }
            using var revokedConfirm = await product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(revokedToken, new string('a', 64)));
            Assert.Equal(HttpStatusCode.NotFound, revokedConfirm.StatusCode);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
                var row = await db.ProductLicenses.SingleAsync(x => x.PublicCode == licenseCode);
                row.Status = ProductLicenseStates.Active;
                await db.SaveChangesAsync();
            }
            var confirmationToken = await PrepareToken();
            using var wrongMachine = await product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(confirmationToken, new string('f', 64)));
            Assert.Equal(HttpStatusCode.Conflict, wrongMachine.StatusCode);
            var concurrent = await Task.WhenAll(
                product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(confirmationToken, new string('a', 64))),
                product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(confirmationToken, new string('a', 64))));
            using var firstActivation = concurrent[0];
            using var concurrentRetry = concurrent[1];
            Assert.Equal(HttpStatusCode.OK, concurrentRetry.StatusCode);
            var concurrentLease = (await concurrentRetry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lease").GetString();
            Assert.Equal(HttpStatusCode.OK, firstActivation.StatusCode);
            var firstLease = (await firstActivation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lease").GetString()!;
            Assert.Equal(firstLease, concurrentLease);
            Assert.StartsWith("ESLEASE1.", firstLease, StringComparison.Ordinal);
            using var leasePayload = JsonDocument.Parse(WebEncoders.Base64UrlDecode(firstLease.Split('.')[1]));
            Assert.Equal(14, leasePayload.RootElement.EnumerateObject().Count());
            Assert.Equal(1, leasePayload.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(activationId.ToString("D"), leasePayload.RootElement.GetProperty("activationId").GetString());
            Assert.Equal(new string('a', 64), leasePayload.RootElement.GetProperty("machineFingerprint").GetString());
            Assert.Equal(OnlineLeaseSigningOptions.ApprovedKeyId, leasePayload.RootElement.GetProperty("keyId").GetString());
            Assert.Equal(TimeSpan.FromHours(72), leasePayload.RootElement.GetProperty("notAfterUtc").GetDateTimeOffset() -
                leasePayload.RootElement.GetProperty("issuedAtUtc").GetDateTimeOffset());

            using var activationRetry = await product.PostAsJsonAsync("api/licensing/device/confirm", Confirmation(confirmationToken, new string('a', 64)));
            Assert.Equal(HttpStatusCode.OK, activationRetry.StatusCode);
            Assert.Equal(firstLease, (await activationRetry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lease").GetString());

            var renewalId = Guid.NewGuid();
            var nextRefresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            using var firstRenewalRequest = new HttpRequestMessage(HttpMethod.Post, $"api/licensing/device/activations/{activationId}/renew")
            {
                Content = JsonContent.Create(new { requestId = renewalId, nextRefreshCredential = nextRefresh })
            };
            firstRenewalRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refresh);
            using var firstRenewal = await product.SendAsync(firstRenewalRequest);
            Assert.Equal(HttpStatusCode.OK, firstRenewal.StatusCode);
            var renewedLease = (await firstRenewal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lease").GetString()!;

            using var lostResponseRetryRequest = new HttpRequestMessage(HttpMethod.Post, $"api/licensing/device/activations/{activationId}/renew")
            {
                Content = JsonContent.Create(new { requestId = renewalId, nextRefreshCredential = nextRefresh })
            };
            lostResponseRetryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refresh);
            using var lostResponseRetry = await product.SendAsync(lostResponseRetryRequest);
            Assert.Equal(HttpStatusCode.OK, lostResponseRetry.StatusCode);
            Assert.Equal(renewedLease, (await lostResponseRetry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lease").GetString());

            var attackerNextRefresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            using var reuseRequest = new HttpRequestMessage(HttpMethod.Post, $"api/licensing/device/activations/{activationId}/renew")
            {
                Content = JsonContent.Create(new { requestId = Guid.NewGuid(), nextRefreshCredential = attackerNextRefresh })
            };
            reuseRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", refresh);
            using var reuseResponse = await product.SendAsync(reuseRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

            using var currentTokenRequest = new HttpRequestMessage(HttpMethod.Post, $"api/licensing/device/activations/{activationId}/renew")
            {
                Content = JsonContent.Create(new { requestId = Guid.NewGuid(), nextRefreshCredential = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)) })
            };
            currentTokenRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", nextRefresh);
            using var currentTokenAfterReuse = await product.SendAsync(currentTokenRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, currentTokenAfterReuse.StatusCode);

            var secondActivationId = Guid.NewGuid();
            var secondRefresh = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            using var secondActivation = await ActivateThroughConfirmation(product, new
            {
                username,
                password = Password,
                licenseCode,
                activationId = secondActivationId,
                requestId = Guid.NewGuid(),
                machineFingerprint = new string('b', 64),
                refreshCredential = secondRefresh
            });
            Assert.Equal(HttpStatusCode.OK, secondActivation.StatusCode);

            using var activationLimit = await ActivateThroughConfirmation(product, new
            {
                username,
                password = Password,
                licenseCode,
                activationId = Guid.NewGuid(),
                requestId = Guid.NewGuid(),
                machineFingerprint = new string('c', 64),
                refreshCredential = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))
            });
            Assert.Equal(HttpStatusCode.Conflict, activationLimit.StatusCode);
            Assert.Equal("activation-limit-reached", (await activationLimit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());

            var member = await Login(factory, username);
            await member.Send("/profile/password", HttpMethod.Post,
                new { currentPassword = Password, newPassword = "ChangedScadivo2!" });

            using var renewalAfterPasswordChangeRequest = new HttpRequestMessage(HttpMethod.Post, $"api/licensing/device/activations/{secondActivationId}/renew")
            {
                Content = JsonContent.Create(new { requestId = Guid.NewGuid(), nextRefreshCredential = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)) })
            };
            renewalAfterPasswordChangeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secondRefresh);
            using var renewalAfterPasswordChange = await product.SendAsync(renewalAfterPasswordChangeRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, renewalAfterPasswordChange.StatusCode);

            var activations = await member.Get("/licensing/activations");
            var activationStatuses = activations.GetProperty("activations").EnumerateArray().Select(x => x.GetProperty("status").GetString()).ToArray();
            Assert.Equal(2, activationStatuses.Length);
            Assert.All(activationStatuses, status => Assert.Equal("reauthentication-required", status));

            await using var scopeAfter = factory.Services.CreateAsyncScope();
            var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<PortalDb>();
            var persisted = await dbAfter.ProductLicenseRefreshCredentials.Where(x => x.ActivationId == activationId).ToListAsync();
            Assert.Equal(2, persisted.Count);
            var storedHashes = persisted.Select(credential => credential.CredentialHash).ToArray();
            Assert.Contains(Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(refresh))).ToLowerInvariant(), storedHashes);
            Assert.Contains(Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(nextRefresh))).ToLowerInvariant(), storedHashes);
            Assert.All(persisted, credential => Assert.DoesNotContain(credential.CredentialHash, new[] { refresh, nextRefresh }));
            Assert.Equal(userId, (await dbAfter.ProductLicenseActivations.SingleAsync(x => x.Id == activationId)).UserId);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip", true)]
    [InlineData("https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip?secret=value", false)]
    [InlineData("https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip#fragment", false)]
    [InlineData("https://user:password@github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip", false)]
    [InlineData("https://github.com.evil.invalid/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip", false)]
    [InlineData("https://github.com/another/repository/releases/download/v1/scadivo.zip", false)]
    [InlineData("https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/", false)]
    [InlineData("https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/file%2felsewhere.zip", false)]
    [InlineData("http://github.com/brunolrogerio-collab/EliteSCADA/releases/download/v1/scadivo.zip", false)]
    public void DownloadCatalogOnlyAcceptsOfficialReleaseFiles(string url, bool allowed)
        => Assert.Equal(allowed, PortalDownloadCatalog.IsOfficialUrl(url));

    [Fact]
    public async Task AdministrationDelegationAndDownloadPublishingEnforceServerBoundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "scadivo-administration-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passwordFile = Path.Combine(root, "owner-password");
        await File.WriteAllTextAsync(passwordFile, Password);
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Portal"] = Environment.GetEnvironmentVariable("PORTAL_TEST_DATABASE")
                ?? "Host=127.0.0.1;Port=15441;Database=scadivo_portal;Username=scadivo_portal;Password=PortalTestOnly-NotASecret!",
            ["Portal:Bootstrap:PasswordFile"] = passwordFile,
            ["Portal:KeyDirectory"] = Path.Combine(root, "keys"),
            ["Logging:LogLevel:Default"] = "Warning"
        };
        var marker = Guid.NewGuid().ToString("N")[..8];
        try
        {
            await using var factory = Factory(configuration);
            var owner = await Login(factory, "scadivo.owner");
            var ownerId = (await owner.Get("/profile")).GetProperty("id").GetString()!;
            string memberId, otherId, pendingId;
            Guid customerOrganizationId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<PortalUser>>();
                async Task<string> CreateAccount(string suffix, bool confirmed = true)
                {
                    var account = new PortalUser { UserName = $"staff-{marker}-{suffix}", Email = $"staff-{marker}-{suffix}@example.test",
                        EmailConfirmed = confirmed, InvitationAccepted = true, DisplayName = "Synthetic Staff",
                        ProfileType = "professional", ActivityArea = "industrial-automation", OrganizationOrSchool = "Synthetic Company" };
                    PortalSeed.Ensure(await manager.CreateAsync(account, Password));
                    PortalSeed.Ensure(await manager.AddToRoleAsync(account, "Member"));
                    return account.Id;
                }
                memberId = await CreateAccount("one"); otherId = await CreateAccount("two"); pendingId = await CreateAccount("pending", false);
                await CreateAccount("customer");
                var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
                var organization = new Organization { Name = "Synthetic customer " + marker };
                customerOrganizationId = organization.Id;
                db.Organizations.Add(organization);
                db.Memberships.Add(new Membership { OrganizationId = organization.Id, UserId = memberId, Role = "Admin" });
                await db.SaveChangesAsync();
            }

            using var anonymous = Client(factory);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/downloads")).StatusCode);
            var member = await Login(factory, $"staff-{marker}-one");
            Assert.False((await member.Get("/profile")).GetProperty("administrator").GetBoolean());
            Assert.Equal(HttpStatusCode.Forbidden, (await member.Raw($"/admin/accounts/{memberId}/administration", HttpMethod.Put, new { administrator = true })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("api/admin/downloads")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await member.Raw("/admin/downloads", HttpMethod.Post, new { version = "v1", platform = "windows-x64", published = false })).StatusCode);
            using (var noCsrf = await owner.Client.PutAsJsonAsync($"api/admin/accounts/{memberId}/administration", new { administrator = true }))
                Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await owner.Raw($"/admin/accounts/{pendingId}/administration", HttpMethod.Put, new { administrator = true })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await owner.Raw($"/admin/accounts/{ownerId}/administration", HttpMethod.Put, new { administrator = false })).StatusCode);
            await owner.Send($"/admin/accounts/{memberId}/administration", HttpMethod.Put, new { administrator = true });
            Assert.Equal(HttpStatusCode.Unauthorized, (await member.Client.GetAsync("api/admin/overview")).StatusCode);
            var staff = await Login(factory, $"staff-{marker}-one");
            var profile = await staff.Get("/profile");
            Assert.True(profile.GetProperty("administrator").GetBoolean()); Assert.False(profile.GetProperty("owner").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await staff.Client.GetAsync("api/admin/licensing/licenses")).StatusCode);
            await staff.Send($"/admin/accounts/{otherId}/administration", HttpMethod.Put, new { administrator = true });
            var delegatedStaff = await Login(factory, $"staff-{marker}-two");
            Assert.True((await delegatedStaff.Get("/profile")).GetProperty("administrator").GetBoolean());
            Assert.Empty((await delegatedStaff.Get("/licensing/organizations")).EnumerateArray());
            Assert.Equal(HttpStatusCode.NotFound, (await delegatedStaff.Client.GetAsync($"api/licensing/organizations/{customerOrganizationId}/members")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await delegatedStaff.Raw($"/licensing/organizations/{customerOrganizationId}/applications", HttpMethod.Post,
                new { name = "Unauthorized staff application" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await delegatedStaff.Raw($"/licensing/organizations/{customerOrganizationId}/members", HttpMethod.Post,
                new { userIdentifier = "staff-member@example.test", role = "Admin" })).StatusCode);
            Assert.Empty((await delegatedStaff.Get("/licensing/licenses")).GetProperty("licenses").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, (await delegatedStaff.Client.GetAsync("api/admin/licensing/organizations")).StatusCode);

            var version = "test-" + marker;
            var draft = await staff.Send("/admin/downloads", HttpMethod.Post, new { version, platform = "windows-x64", published = false });
            var releaseId = draft.GetProperty("id").GetGuid();
            var ordinaryMember = await Login(factory, $"staff-{marker}-customer");
            Assert.DoesNotContain((await ordinaryMember.Get("/downloads")).EnumerateArray(), item => item.GetProperty("id").GetGuid() == releaseId);
            Assert.Equal(HttpStatusCode.NotFound, (await ordinaryMember.Client.GetAsync($"api/downloads/{releaseId}/file")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await staff.Raw("/admin/downloads", HttpMethod.Post, new { version, platform = "windows-x64", published = false })).StatusCode);
            var url = $"https://github.com/brunolrogerio-collab/EliteSCADA/releases/download/{version}/scadivo.zip";
            object ReleaseInput(string officialUrl, string hash, bool published, string notesEs = "Notas de prueba") => new
            {
                version, platform = "windows-x64", officialUrl, sha256 = hash, published,
                notesPt = "Notas de teste", notesEn = "Test release notes", notesEs
            };
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.Raw($"/admin/downloads/{releaseId}", HttpMethod.Put, ReleaseInput(url, "invalid", true))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.Raw($"/admin/downloads/{releaseId}", HttpMethod.Put, ReleaseInput("https://other.invalid/file.zip", new string('a', 64), true))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.Raw($"/admin/downloads/{releaseId}", HttpMethod.Put, ReleaseInput(url, new string('a', 64), true, ""))).StatusCode);
            await staff.Send($"/admin/downloads/{releaseId}", HttpMethod.Put, ReleaseInput(url, new string('a', 64), true));
            var visible = (await ordinaryMember.Get("/downloads")).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == releaseId);
            Assert.Equal("Test release notes", visible.GetProperty("notesEn").GetString());
            Assert.False(visible.TryGetProperty("officialUrl", out _));
            using (var download = await ordinaryMember.Client.GetAsync($"api/downloads/{releaseId}/file"))
            {
                Assert.Equal(HttpStatusCode.Redirect, download.StatusCode);
                Assert.Equal(url, download.Headers.Location?.AbsoluteUri);
            }
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"api/downloads/{releaseId}/file")).StatusCode);
            await staff.Send($"/admin/downloads/{releaseId}", HttpMethod.Put, ReleaseInput(url, new string('a', 64), false));
            Assert.DoesNotContain((await ordinaryMember.Get("/downloads")).EnumerateArray(), item => item.GetProperty("id").GetGuid() == releaseId);
            Assert.Equal(HttpStatusCode.NotFound, (await ordinaryMember.Client.GetAsync($"api/downloads/{releaseId}/file")).StatusCode);
            Assert.Contains((await staff.Get("/admin/downloads")).EnumerateArray(), item => item.GetProperty("id").GetGuid() == releaseId);

            await staff.Send($"/admin/accounts/{otherId}/administration", HttpMethod.Put, new { administrator = false });
            Assert.Equal(HttpStatusCode.Unauthorized, (await delegatedStaff.Client.GetAsync("api/admin/overview")).StatusCode);
            await owner.Send($"/admin/accounts/{memberId}/administration", HttpMethod.Put, new { administrator = false });
            Assert.Equal(HttpStatusCode.Unauthorized, (await staff.Client.GetAsync("api/admin/downloads")).StatusCode);
            var formerStaff = await Login(factory, $"staff-{marker}-one");
            Assert.Equal(HttpStatusCode.Forbidden, (await formerStaff.Client.GetAsync("api/admin/downloads")).StatusCode);
            Assert.Contains((await owner.Get("/admin/audit")).EnumerateArray(), item => item.GetProperty("action").GetString() == "account.administration.revoked");
            Assert.Contains((await owner.Get("/admin/audit")).EnumerateArray(), item => item.GetProperty("action").GetString() == "download.release.unpublished");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class TestOnlineLeaseSigner : IOnlineLeaseSigner
    {
        public bool IsEnabled => true;

        public string Sign(OnlineLeaseClaims claims)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(claims, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return $"ESLEASE1.{WebEncoders.Base64UrlEncode(payload)}.{WebEncoders.Base64UrlEncode(new byte[384])}";
        }
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
    private static async Task<HttpResponseMessage> ActivateThroughConfirmation(HttpClient product, object body)
    {
        var data = JsonSerializer.SerializeToElement(body);
        using var prepared = await product.PostAsJsonAsync("api/licensing/device/prepare", new {
            username = data.GetProperty("username").GetString(), password = data.GetProperty("password").GetString(),
            licenseCode = data.GetProperty("licenseCode").GetString(), activationId = data.GetProperty("activationId").GetGuid(),
            machineFingerprint = data.GetProperty("machineFingerprint").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var token = (await prepared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmationToken").GetString();
        return await product.PostAsJsonAsync("api/licensing/device/confirm", new {
            confirmationToken = token, activationId = data.GetProperty("activationId").GetGuid(),
            requestId = data.GetProperty("requestId").GetGuid(), machineFingerprint = data.GetProperty("machineFingerprint").GetString(),
            refreshCredential = data.GetProperty("refreshCredential").GetString()
        });
    }

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
