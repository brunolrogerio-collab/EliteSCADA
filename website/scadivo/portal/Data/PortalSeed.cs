using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Scadivo.Portal.Data;

public static class PortalSeed
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<PortalDb>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<PortalUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var role in new[] { "Owner", "Member" })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));

        if (!await db.Users.AnyAsync())
        {
            var passwordFile = configuration["Portal:Bootstrap:PasswordFile"];
            if (string.IsNullOrWhiteSpace(passwordFile) || !File.Exists(passwordFile))
                throw new InvalidOperationException("Initial portal owner password file is required for an empty database.");
            var owner = new PortalUser
            {
                UserName = configuration["Portal:Bootstrap:Username"] ?? "scadivo.owner",
                DisplayName = "SCADIVO",
                InvitationAccepted = true
            };
            Ensure(await users.CreateAsync(owner, (await File.ReadAllTextAsync(passwordFile)).Trim()));
            Ensure(await users.AddToRoleAsync(owner, "Owner"));
            var organization = new Organization { Name = "SCADIVO" };
            db.Organizations.Add(organization);
            db.Memberships.Add(new Membership { OrganizationId = organization.Id, UserId = owner.Id, Role = "Owner" });
            db.Audit.Add(new PortalAudit { ActorId = owner.Id, Action = "portal.owner.initialized" });
        }

        if (!await db.FaqArticles.AnyAsync())
        {
            db.FaqArticles.AddRange(
                Article("one-system", "SCADIVO é o mesmo sistema para aplicações pequenas e grandes?", "Is SCADIVO the same system for small and large applications?",
                    "Sim. O sistema é um só para aplicações residenciais, prediais e industriais. Quantidade de TAGs, telas, integrações e complexidade da lógica acompanham o projeto.",
                    "Yes. One system serves residential, building and industrial applications. Tag count, screens, integrations and logic complexity follow your project."),
                Article("free-tags", "Como funciona a faixa gratuita de 200 TAGs?", "How does the free 200-tag tier work?",
                    "A política pública do SCADIVO prevê aplicações com até 200 TAGs gratuitas. TAGs são variáveis como temperatura, nível, estado ou comando. Projetos maiores utilizam licenciamento por capacidade.",
                    "SCADIVO's public offering provides up to 200 free tags. Tags are variables such as temperature, level, state or commands. Larger projects use capacity-based licensing."),
                Article("local-web-edge", "Web significa que o sistema precisa estar na nuvem?", "Does web access require cloud hosting?",
                    "Não. O servidor pode executar na própria máquina, rede local, servidor remoto ou dispositivo Edge compatível. A interface de engenharia e operação é acessada pelo navegador.",
                    "No. The server can run on the same computer, a local network, remote infrastructure or a compatible edge device. Engineering and operations are accessed in a browser."),
                Article("demo-access", "Como acessar a demonstração?", "How do I access the demo?",
                    "O Runtime público está em scadivo.com/demo e não exige login. A conta deste portal é independente das credenciais de engenharia da demonstração. EEE e RHOs são simulações.",
                    "Public Runtime is at scadivo.com/demo and requires no login. Your portal account is independent of demo engineering credentials. EEE and RHOs are simulations."),
                Article("official-downloads", "Por que o catálogo pode estar vazio?", "Why might the download catalog be empty?",
                    "São disponibilizados somente artefatos oficiais aprovados, identificados por versão, plataforma e SHA-256. Quando não há publicação aprovada, nenhum instalador alternativo é oferecido.",
                    "Only approved official artifacts are listed, identified by version, platform and SHA-256. No alternative installer is offered when no approved release is available."),
                Article("private-support", "Quem vê meus chamados?", "Who can see my support requests?",
                    "Você vê seus próprios chamados e respostas. A administração do suporte pode atendê-los. Chamados privados não são publicações da comunidade.",
                    "You see your own requests and replies. Support administrators can handle them. Private support requests are not community posts."));
        }

        foreach (var item in Templates())
            if (!await db.EmailTemplates.AnyAsync(x => x.Key == item.Key)) db.EmailTemplates.Add(item);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static FaqArticle Article(string slug, string titlePt, string titleEn, string bodyPt, string bodyEn) =>
        new() { Slug = slug, TitlePt = titlePt, TitleEn = titleEn, BodyPt = bodyPt, BodyEn = bodyEn };

    private static IEnumerable<EmailTemplate> Templates()
    {
        yield return new() { Key = "account.invitation", SubjectPt = "Seu convite SCADIVO", SubjectEn = "Your SCADIVO invitation", BodyPt = "Olá, {{name}}. Seu convite para a conta SCADIVO foi preparado. Utilize o link recebido da administração.", BodyEn = "Hello, {{name}}. Your SCADIVO account invitation is ready. Use the link provided by the administrator." };
        yield return new() { Key = "account.confirmation", SubjectPt = "Confirme seu e-mail SCADIVO", SubjectEn = "Confirm your SCADIVO email", BodyPt = "Olá, {{name}}. Confirme seu endereço pelo link de verificação: {{link}}", BodyEn = "Hello, {{name}}. Confirm your address using the verification link: {{link}}" };
        yield return new() { Key = "account.password-reset", SubjectPt = "Recuperação de acesso SCADIVO", SubjectEn = "SCADIVO account recovery", BodyPt = "Olá, {{name}}. Para redefinir sua senha, use: {{link}}. Se não solicitou, ignore esta mensagem.", BodyEn = "Hello, {{name}}. To reset your password, use: {{link}}. If you did not request this, ignore this message." };
        yield return new() { Key = "support.created", SubjectPt = "Chamado SCADIVO recebido", SubjectEn = "SCADIVO support request received", BodyPt = "Olá, {{name}}. Recebemos o chamado {{reference}}. Consulte a área de suporte no portal.", BodyEn = "Hello, {{name}}. We received request {{reference}}. Open Support in your portal to follow it." };
        yield return new() { Key = "support.replied", SubjectPt = "Nova resposta no seu chamado SCADIVO", SubjectEn = "New reply to your SCADIVO request", BodyPt = "Olá, {{name}}. Há uma resposta no chamado {{reference}}. Acesse o portal para consultá-la.", BodyEn = "Hello, {{name}}. Request {{reference}} has a new reply. Sign into the portal to read it." };
        yield return new() { Key = "contact.received", SubjectPt = "Recebemos sua mensagem — SCADIVO", SubjectEn = "We received your message — SCADIVO", BodyPt = "Olá, {{name}}!\n\nRecebemos sua mensagem sobre {{subject}}. Nossa equipe responsável vai analisá-la e responder pelo e-mail informado.\n\nProtocolo: {{reference}}\n\nSe você não enviou esta mensagem, pode ignorar este e-mail.", BodyEn = "Hello, {{name}}!\n\nWe received your message about {{subject}}. The appropriate team will review it and reply to the email address you provided.\n\nReference: {{reference}}\n\nIf you did not send this message, you can ignore this email." };
        yield return new() { Key = "contact.notification", SubjectPt = "Novo contato pelo site — {{category}} — {{reference}}", SubjectEn = "New website contact — {{category}} — {{reference}}", BodyPt = "Novo contato recebido pelo site SCADIVO.\n\nÁrea: {{category}}\nAssunto: {{subject}}\nNome: {{name}}\nE-mail: {{email}}\nProtocolo: {{reference}}\n\nMensagem:\n{{message}}\n\nResponda a este e-mail para falar diretamente com a pessoa.", BodyEn = "New contact received through the SCADIVO website.\n\nArea: {{category}}\nSubject: {{subject}}\nName: {{name}}\nEmail: {{email}}\nReference: {{reference}}\n\nMessage:\n{{message}}\n\nReply to this email to contact the sender directly." };
        yield return new() { Key = "payment.receipt", SubjectPt = "Atualização de pagamento SCADIVO", SubjectEn = "SCADIVO payment update", BodyPt = "Olá, {{name}}. Consulte o estado do pedido {{reference}} em sua conta SCADIVO.", BodyEn = "Hello, {{name}}. Check the status of order {{reference}} in your SCADIVO account." };
        yield return new() { Key = "license.renewal-failed", SubjectPt = "Verifique a renovação SCADIVO", SubjectEn = "Check your SCADIVO renewal", BodyPt = "Olá, {{name}}. A renovação da licença {{reference}} requer sua atenção no portal.", BodyEn = "Hello, {{name}}. Renewal of license {{reference}} needs your attention in the portal." };
    }

    public static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Code)));
    }
}
