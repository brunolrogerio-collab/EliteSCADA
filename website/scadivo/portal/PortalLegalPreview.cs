using System.Text.Json;
using Microsoft.Extensions.Options;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

internal static class PortalLegalPreview
{
    internal const string EmailTemplateKey = "legal.preview";

    public static bool IsEnabled(WebApplication app) =>
        app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Portal:LegalPreviewEnabled");

    public static void MapDocuments(WebApplication app, bool enabled)
    {
        app.MapGet("/legal-preview/{locale}", (string locale, HttpContext context) =>
        {
            if (!enabled || locale is not ("pt-BR" or "en" or "es")) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            var path = Path.Combine(app.Environment.ContentRootPath, "LegalPreview", locale + ".html");
            return Results.File(path, "text/html; charset=utf-8");
        });
    }

    public static async Task SendTestEmailsAsync(WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<PortalEmailOptions>>().Value;
        if (!IsEnabled(app) || !settings.Enabled ||
            settings is not { Host: "portal-mailpit-homolog", Port: 1025 } ||
            !Uri.TryCreate(app.Configuration["Portal:PublicUrl"], UriKind.Absolute, out var portal) || !portal.IsLoopback)
            throw new InvalidOperationException("Legal preview emails require the local Mailpit environment.");

        var path = Path.Combine(app.Environment.ContentRootPath, "LegalPreview", "emails.json");
        var emails = JsonSerializer.Deserialize<Dictionary<string, LegalEmail>>(
            await File.ReadAllTextAsync(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Missing legal preview templates.");
        var template = new EmailTemplate
        {
            Key = EmailTemplateKey,
            SubjectPt = emails["pt-BR"].Subject,
            BodyPt = emails["pt-BR"].Body,
            SubjectEn = emails["en"].Subject,
            BodyEn = emails["en"].Body,
            SubjectEs = emails["es"].Subject,
            BodyEs = emails["es"].Body
        };

        var transport = app.Services.GetRequiredService<IPortalEmailTransport>();
        foreach (var locale in new[] { "pt-BR", "en", "es" })
        {
            var data = new Dictionary<string, string> { ["link"] = new Uri(portal, $"legal-preview/{locale}").AbsoluteUri };
            using var message = PortalEmailDispatcher.CreateTemplateMessage(settings, Guid.NewGuid(),
                $"legal-preview-{locale.ToLowerInvariant()}@example.test", template, locale, data);
            await transport.SendAsync(message, CancellationToken.None);
        }
    }

    private sealed record LegalEmail(string Subject, string Body);
}
