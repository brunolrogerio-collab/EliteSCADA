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
        foreach (var role in new[] { "Owner", "Admin", "Member" })
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

        var faqSeeds = FaqArticles().ToArray();
        if (!await db.FaqArticles.AnyAsync()) db.FaqArticles.AddRange(faqSeeds);
        foreach (var seed in faqSeeds)
        {
            var existing = await db.FaqArticles.FirstOrDefaultAsync(x => x.Slug == seed.Slug);
            if (existing is null) continue;
            if (string.IsNullOrWhiteSpace(existing.TitleEs)) existing.TitleEs = seed.TitleEs;
            if (string.IsNullOrWhiteSpace(existing.BodyEs)) existing.BodyEs = seed.BodyEs;
        }

        foreach (var item in Templates())
        {
            var existing = await db.EmailTemplates.FindAsync(item.Key);
            if (existing is null) db.EmailTemplates.Add(item);
            else
            {
                if (string.IsNullOrWhiteSpace(existing.SubjectEs)) existing.SubjectEs = item.SubjectEs;
                if (string.IsNullOrWhiteSpace(existing.BodyEs)) existing.BodyEs = item.BodyEs;
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static IEnumerable<FaqArticle> FaqArticles()
    {
        yield return Article("one-system", "SCADIVO é o mesmo sistema para aplicações pequenas e grandes?", "Is SCADIVO the same system for small and large applications?",
            "SCADIVO es el mismo sistema para aplicaciones pequeñas y grandes?",
            "Sim. O sistema é um só para aplicações residenciais, prediais e industriais. Quantidade de TAGs, telas, integrações e complexidade da lógica acompanham o projeto.",
            "Yes. One system serves residential, building and industrial applications. Tag count, screens, integrations and logic complexity follow your project.",
            "Sí. Un solo sistema sirve para aplicaciones residenciales, de edificios e industriales. La cantidad de TAGs, pantallas, integraciones y la complejidad de la lógica se adaptan al proyecto.");
        yield return Article("free-tags", "Como funciona a faixa gratuita de 200 TAGs?", "How does the free 200-tag tier work?", "¿Cómo funciona el plan gratuito de hasta 200 TAGs?",
            "A política pública do SCADIVO prevê aplicações com até 200 TAGs gratuitas. TAGs são variáveis como temperatura, nível, estado ou comando. Projetos maiores utilizam licenciamento por capacidade.",
            "SCADIVO's public offering provides up to 200 free tags. Tags are variables such as temperature, level, state or commands. Larger projects use capacity-based licensing.",
            "SCADIVO ofrece aplicaciones gratuitas de hasta 200 TAGs. Las TAGs son variables como temperatura, nivel, estado o comando. Los proyectos más grandes utilizan licencias según su capacidad.");
        yield return Article("local-web-edge", "Web significa que o sistema precisa estar na nuvem?", "Does web access require cloud hosting?", "¿El acceso web significa que el sistema debe estar en la nube?",
            "Não. O servidor pode executar na própria máquina, rede local, servidor remoto ou dispositivo Edge compatível. A interface de engenharia e operação é acessada pelo navegador.",
            "No. The server can run on the same computer, a local network, remote infrastructure or a compatible edge device. Engineering and operations are accessed in a browser.",
            "No. El servidor puede ejecutarse en el mismo equipo, en una red local, en un servidor remoto o en un dispositivo Edge compatible. Se accede a la ingeniería y a la operación desde el navegador.");
        yield return Article("demo-access", "Como acessar a demonstração?", "How do I access the demo?", "¿Cómo accedo a la demostración?",
            "O Runtime público está em scadivo.com/demo e não exige login. A conta deste portal é independente das credenciais de engenharia da demonstração. EEE e RHOs são simulações.",
            "Public Runtime is at scadivo.com/demo and requires no login. Your portal account is independent of demo engineering credentials. EEE and RHOs are simulations.",
            "El Runtime público está en scadivo.com/demo y no requiere iniciar sesión. La cuenta del portal es independiente de las credenciales de ingeniería de la demostración. EEE y RHOs son simulaciones.");
        yield return Article("official-downloads", "Por que o catálogo pode estar vazio?", "Why might the download catalog be empty?", "¿Por qué puede estar vacío el catálogo de descargas?",
            "São disponibilizados somente artefatos oficiais aprovados, identificados por versão, plataforma e SHA-256. Quando não há publicação aprovada, nenhum instalador alternativo é oferecido.",
            "Only approved official artifacts are listed, identified by version, platform and SHA-256. No alternative installer is offered when no approved release is available.",
            "Solo se publican paquetes oficiales aprobados, identificados por versión, plataforma y SHA-256. Si no hay una versión aprobada, no se ofrecen instaladores alternativos.");
        yield return Article("private-support", "Quem vê meus chamados?", "Who can see my support requests?", "¿Quién puede ver mis solicitudes de soporte?",
            "Você vê seus próprios chamados e respostas. A administração do suporte pode atendê-los. Chamados privados não são publicações da comunidade.",
            "You see your own requests and replies. Support administrators can handle them. Private support requests are not community posts.",
            "Puedes ver tus propias solicitudes y respuestas. El equipo de soporte puede atenderlas. Las solicitudes privadas no son publicaciones de la comunidad.");
    }

    private static FaqArticle Article(string slug, string titlePt, string titleEn, string titleEs, string bodyPt, string bodyEn, string bodyEs) =>
        new() { Slug = slug, TitlePt = titlePt, TitleEn = titleEn, TitleEs = titleEs, BodyPt = bodyPt, BodyEn = bodyEn, BodyEs = bodyEs };

    private static IEnumerable<EmailTemplate> Templates()
    {
        yield return new() { Key = "account.invitation", SubjectPt = "Seu convite SCADIVO", SubjectEn = "Your SCADIVO invitation", SubjectEs = "Tu invitación a SCADIVO", BodyPt = "Olá, {{name}}. Seu convite para a conta SCADIVO foi preparado. Utilize o link recebido da administração.", BodyEn = "Hello, {{name}}. Your SCADIVO account invitation is ready. Use the link provided by the administrator.", BodyEs = "Hola, {{name}}. Tu invitación a la cuenta de SCADIVO está lista. Utiliza el enlace que te proporcionó la administración." };
        yield return new() { Key = "account.confirmation", SubjectPt = "Confirme seu e-mail SCADIVO", SubjectEn = "Confirm your SCADIVO email", SubjectEs = "Confirma tu correo electrónico de SCADIVO", BodyPt = "Olá, {{name}}.\n\nConfirme seu endereço de e-mail acessando o link pessoal abaixo:\n{{link}}\n\nO link expira em 24 horas e pode ser usado uma única vez. Se você não iniciou este cadastro, ignore esta mensagem.", BodyEn = "Hello, {{name}}.\n\nConfirm your email address using the personal link below:\n{{link}}\n\nThe link expires in 24 hours and can only be used once. If you did not start this registration, ignore this message.", BodyEs = "Hola, {{name}}.\n\nConfirma tu dirección de correo electrónico con el enlace personal que aparece a continuación:\n{{link}}\n\nEl enlace caduca en 24 horas y solo se puede usar una vez. Si no iniciaste este registro, ignora este mensaje." };
        yield return new() { Key = "account.password-reset", SubjectPt = "Recuperação de acesso SCADIVO", SubjectEn = "SCADIVO account recovery", SubjectEs = "Recuperación de acceso a SCADIVO", BodyPt = "Olá, {{name}}.\n\nPara redefinir sua senha, acesse o link pessoal abaixo:\n{{link}}\n\nO link expira em 24 horas e pode ser usado uma única vez. Se você não solicitou, ignore esta mensagem.", BodyEn = "Hello, {{name}}.\n\nTo reset your password, use the personal link below:\n{{link}}\n\nThe link expires in 24 hours and can only be used once. If you did not request this, ignore this message.", BodyEs = "Hola, {{name}}.\n\nPara restablecer tu contraseña, utiliza el enlace personal que aparece a continuación:\n{{link}}\n\nEl enlace caduca en 24 horas y solo se puede usar una vez. Si no lo solicitaste, ignora este mensaje." };
        yield return new() { Key = "support.created", SubjectPt = "Chamado SCADIVO recebido", SubjectEn = "SCADIVO support request received", SubjectEs = "Solicitud de soporte recibida por SCADIVO", BodyPt = "Olá, {{name}}. Recebemos o chamado {{reference}}. Consulte a área de suporte no portal.", BodyEn = "Hello, {{name}}. We received request {{reference}}. Open Support in your portal to follow it.", BodyEs = "Hola, {{name}}. Recibimos tu solicitud {{reference}}. Consulta el área de soporte del portal." };
        yield return new() { Key = "support.replied", SubjectPt = "Nova resposta no seu chamado SCADIVO", SubjectEn = "New reply to your SCADIVO request", SubjectEs = "Nueva respuesta a tu solicitud de SCADIVO", BodyPt = "Olá, {{name}}. Há uma resposta no chamado {{reference}}. Acesse o portal para consultá-la.", BodyEn = "Hello, {{name}}. Request {{reference}} has a new reply. Sign into the portal to read it.", BodyEs = "Hola, {{name}}. Hay una respuesta a la solicitud {{reference}}. Accede al portal para consultarla." };
        yield return new() { Key = "support.team-ticket", SubjectPt = "Novo chamado no portal SCADIVO", SubjectEn = "New SCADIVO portal support request", SubjectEs = "Nuevo caso de soporte en el portal SCADIVO", BodyPt = "Um novo chamado foi aberto no portal SCADIVO.\n\nCategoria: {{category}}\nReferência: {{reference}}\n\nAcesse o portal para consultar a conversa privada e responder:\n{{link}}\n\nO conteúdo do chamado não foi incluído neste aviso.", BodyEn = "A new support request was opened in the SCADIVO portal.\n\nCategory: {{category}}\nReference: {{reference}}\n\nOpen the portal to read the private conversation and reply:\n{{link}}\n\nThe request content is not included in this notification.", BodyEs = "Se abrió una nueva solicitud de soporte en el portal SCADIVO.\n\nCategoría: {{category}}\nReferencia: {{reference}}\n\nAccede al portal para consultar la conversación privada y responder:\n{{link}}\n\nEl contenido de la solicitud no se incluye en este aviso." };
        yield return new() { Key = "support.team-reply", SubjectPt = "Nova mensagem de cliente no portal SCADIVO", SubjectEn = "New customer message in the SCADIVO portal", SubjectEs = "Nuevo mensaje de cliente en el portal SCADIVO", BodyPt = "Um cliente enviou uma nova mensagem em um chamado do portal SCADIVO.\n\nCategoria: {{category}}\nReferência: {{reference}}\n\nAcesse o portal para consultar a conversa privada e responder:\n{{link}}\n\nO conteúdo da mensagem não foi incluído neste aviso.", BodyEn = "A customer sent a new message in a SCADIVO portal support request.\n\nCategory: {{category}}\nReference: {{reference}}\n\nOpen the portal to read the private conversation and reply:\n{{link}}\n\nThe message content is not included in this notification.", BodyEs = "Un cliente envió un nuevo mensaje en una solicitud de soporte del portal SCADIVO.\n\nCategoría: {{category}}\nReferencia: {{reference}}\n\nAccede al portal para consultar la conversación privada y responder:\n{{link}}\n\nEl contenido del mensaje no se incluye en este aviso." };
        yield return new() { Key = "contact.received", SubjectPt = "Recebemos sua mensagem — SCADIVO", SubjectEn = "We received your message — SCADIVO", SubjectEs = "Recibimos tu mensaje — SCADIVO", BodyPt = "Olá, {{name}}!\n\nRecebemos sua mensagem sobre {{subject}}. Nossa equipe responsável vai analisá-la e responder pelo e-mail informado.\n\nProtocolo: {{reference}}\n\nSe você não enviou esta mensagem, pode ignorar este e-mail.", BodyEn = "Hello, {{name}}!\n\nWe received your message about {{subject}}. The appropriate team will review it and reply to the email address you provided.\n\nReference: {{reference}}\n\nIf you did not send this message, you can ignore this email.", BodyEs = "¡Hola, {{name}}!\n\nRecibimos tu mensaje sobre {{subject}}. El equipo correspondiente lo revisará y responderá al correo electrónico que indicaste.\n\nReferencia: {{reference}}\n\nSi no enviaste este mensaje, puedes ignorar este correo." };
        yield return new() { Key = "contact.notification", SubjectPt = "Novo contato pelo site — {{category}} — {{reference}}", SubjectEn = "New website contact — {{category}} — {{reference}}", SubjectEs = "Nuevo contacto desde el sitio — {{category}} — {{reference}}", BodyPt = "Novo contato recebido pelo site SCADIVO.\n\nÁrea: {{category}}\nAssunto: {{subject}}\nNome: {{name}}\nE-mail: {{email}}\nProtocolo: {{reference}}\n\nMensagem:\n{{message}}\n\nResponda a este e-mail para falar diretamente com a pessoa.", BodyEn = "New contact received through the SCADIVO website.\n\nArea: {{category}}\nSubject: {{subject}}\nName: {{name}}\nEmail: {{email}}\nReference: {{reference}}\n\nMessage:\n{{message}}\n\nReply to this email to contact the sender directly.", BodyEs = "Nuevo contacto recibido a través del sitio web de SCADIVO.\n\nÁrea: {{category}}\nAsunto: {{subject}}\nNombre: {{name}}\nCorreo electrónico: {{email}}\nReferencia: {{reference}}\n\nMensaje:\n{{message}}\n\nResponde a este correo para comunicarte directamente con la persona." };
        yield return new() { Key = "payment.receipt", SubjectPt = "Atualização de pagamento SCADIVO", SubjectEn = "SCADIVO payment update", SubjectEs = "Actualización de pago de SCADIVO", BodyPt = "Olá, {{name}}. Consulte o estado do pedido {{reference}} em sua conta SCADIVO.", BodyEn = "Hello, {{name}}. Check the status of order {{reference}} in your SCADIVO account.", BodyEs = "Hola, {{name}}. Consulta el estado del pedido {{reference}} en tu cuenta de SCADIVO." };
        yield return new() { Key = "license.renewal-failed", SubjectPt = "Verifique a renovação SCADIVO", SubjectEn = "Check your SCADIVO renewal", SubjectEs = "Revisa la renovación de SCADIVO", BodyPt = "Olá, {{name}}. A renovação da licença {{reference}} requer sua atenção no portal.", BodyEn = "Hello, {{name}}. Renewal of license {{reference}} needs your attention in the portal.", BodyEs = "Hola, {{name}}. La renovación de la licencia {{reference}} requiere tu atención en el portal." };
    }

    public static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Code)));
    }
}
