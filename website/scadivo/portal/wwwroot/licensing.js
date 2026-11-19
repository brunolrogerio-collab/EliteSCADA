'use strict';

// Portal accounts manage entitlements here. Product activation stays in the product.
const portalLicensing = (() => {
  let selectedOrganizationId = null;
  const text = (pt, en, es) => tr(pt, en, es);
  const empty = message => h('p', { class: 'licensing-empty' }, message);
  const timestamp = value => value ? date(value) : text('Ainda não ocorreu', 'Not yet', 'Todavía no ocurrió');
  const roleName = role => ({
    Owner: text('Proprietário', 'Owner', 'Propietario'),
    Admin: text('Administrador', 'Administrator', 'Administrador'),
    Member: text('Membro', 'Member', 'Miembro'),
    PortalOwner: text('Administração SCADIVO', 'SCADIVO administration', 'Administración SCADIVO')
  })[role] || role;
  const termName = term => ({
    monthly: text('Mensal', 'Monthly', 'Mensual'),
    annual: text('Anual', 'Annual', 'Anual'),
    perpetual: text('Perpétua na conta', 'Perpetual account entitlement', 'Perpetua en la cuenta')
  })[term] || term;
  const statusName = status => ({
    active: text('Ativa', 'Active', 'Activa'),
    expired: text('Expirada', 'Expired', 'Caducada'),
    revoked: text('Revogada', 'Revoked', 'Revocada'),
    'reauthentication-required': text('Reativação necessária', 'Reactivation required', 'Reactivación necesaria')
  })[status] || status;
  const termOptions = () => [
    ['', text('Selecione o prazo', 'Select the term', 'Selecciona el plazo')],
    ['monthly', termName('monthly')], ['annual', termName('annual')], ['perpetual', termName('perpetual')]
  ];
  const tierOptions = () => [
    ['', text('Selecione a capacidade', 'Select the capacity', 'Selecciona la capacidad')],
    ...[500, 1000, 1500, 3000, 5000].map(count => [`Tags${count}`, `${count.toLocaleString(state.lang)} TAGs`]),
    ['Unlimited', text('TAGs ilimitadas', 'Unlimited tags', 'TAGs ilimitadas')]
  ];
  const tierName = tier => tierOptions().find(([id]) => id === tier)?.[1] || tier;
  const statusBadge = status => h('span', { class: `badge license-status status-${status}` }, statusName(status));
  const facts = entries => h('dl', { class: 'license-facts' }, entries.map(([label, value]) => h('div', {}, [h('dt', {}, label), h('dd', {}, value)])));
  const disclosure = (title, children, open = false) => h('details', { class: 'licensing-disclosure', open }, [h('summary', {}, title), h('div', {}, children)]);
  const check = (name, label) => h('label', { class: 'check-field' }, [input(name, 'checkbox', '', { required: false }), h('span', {}, label)]);

  function errors() {
    return {
      'application-name-already-exists': text('Já existe uma aplicação com esse nome nesta organização.', 'This organization already has an application with that name.', 'Ya existe una aplicación con ese nombre en esta organización.'),
      'account-not-available': text('A conta precisa existir, estar ativa e ter o e-mail confirmado.', 'The account must exist, be active and have a confirmed email.', 'La cuenta debe existir, estar activa y tener el correo confirmado.'),
      'account-already-in-organization': text('Essa conta já pertence à organização.', 'This account already belongs to the organization.', 'Esta cuenta ya pertenece a la organización.'),
      'organization-needs-an-owner': text('A organização precisa manter um proprietário.', 'The organization must keep an owner.', 'La organización debe conservar un propietario.'),
      'unsupported-organization-role': text('Selecione uma função válida para o membro.', 'Select a valid member role.', 'Selecciona un rol válido para el miembro.'),
      'license-revoked': text('Essa licença foi revogada e não pode ser prorrogada.', 'This license was revoked and cannot be extended.', 'Esta licencia fue revocada y no se puede ampliar.'),
      'perpetual-license-does-not-expire': text('Uma licença perpétua na conta não precisa de prorrogação.', 'A perpetual account entitlement does not need an extension.', 'Una licencia perpetua en la cuenta no necesita ampliación.'),
      'unsupported-renewal-term': text('Selecione uma prorrogação mensal ou anual.', 'Select a monthly or annual extension.', 'Selecciona una ampliación mensual o anual.'),
      'license-not-available': text('Essa licença não está disponível para a sua conta.', 'This license is not available to your account.', 'Esta licencia no está disponible para tu cuenta.')
    };
  }

  function actionButton(label, action, className = 'button secondary') {
    const node = button(label, async () => {
      node.disabled = true;
      try { await action(); } catch (error) { feedback(error.message, true); }
      finally { node.disabled = false; }
    }, className);
    return node;
  }

  function codeField(license) {
    const code = input('licenseCode', 'text', license.publicCode, { readOnly: true, required: false, class: 'license-code', 'aria-label': text('Código da licença', 'License code', 'Código de licencia') });
    const copy = actionButton(text('Copiar código', 'Copy code', 'Copiar código'), async () => {
      try {
        await navigator.clipboard.writeText(license.publicCode);
        feedback(text('Código copiado.', 'Code copied.', 'Código copiado.'));
      } catch {
        code.focus(); code.select();
        feedback(text('Selecione e copie o código.', 'Select and copy the code.', 'Selecciona y copia el código.'));
      }
    });
    return field(text('Código da licença', 'License code', 'Código de licencia'), h('div', { class: 'license-copy' }, [code, copy]));
  }

  function licenseCard(license, adminView) {
    const card = h('article', { class: 'license-card', 'data-license-code': license.publicCode }, [
      h('div', { class: 'license-heading' }, [h('div', {}, [h('h3', {}, license.applicationName), p(license.organizationName)]), statusBadge(license.status)]),
      codeField(license),
      facts([
        [text('Capacidade', 'Capacity', 'Capacidad'), tierName(license.tier)],
        [text('Prazo', 'Term', 'Plazo'), termName(license.term)],
        [text('Instalações vinculadas / limite', 'Linked installations / limit', 'Instalaciones vinculadas / límite'), `${license.activationCount} / ${license.maxActivations}`],
        [text('Vencimento do direito de uso', 'Entitlement expiry', 'Vencimiento del derecho de uso'), license.entitlementExpiresAtUtc ? date(license.entitlementExpiresAtUtc) : text('Sem prazo comercial', 'No commercial expiry', 'Sin vencimiento comercial')],
        [text('Sessões Interactive', 'Interactive sessions', 'Sesiones Interactive'), license.interactiveSeats],
        [text('Sessões View Only', 'View Only sessions', 'Sesiones View Only'), license.viewOnlySeats],
        [text('Redundância / HA', 'Redundancy / HA', 'Redundancia / HA'), license.haRuntime ? text('Incluída', 'Included', 'Incluida') : text('Não incluída', 'Not included', 'No incluida')],
        [text('Inteligência de Ativos', 'Asset Process Intelligence', 'Inteligencia de Activos'), license.assetProcessIntelligence ? text('Incluída', 'Included', 'Incluida') : text('Não incluída', 'Not included', 'No incluida')]
      ]),
      h('p', { class: 'muted' }, text('Modo online por conta. Mesmo um direito perpétuo precisa de renovação online; cada autorização dura até 72 horas.', 'Online account mode. Even a perpetual entitlement requires online renewal; each authorization lasts up to 72 hours.', 'Modo en línea por cuenta. Incluso un derecho perpetuo requiere renovación en línea; cada autorización dura hasta 72 horas.'))
    ]);
    if (adminView) card.append(adminLicenseActions(license));
    return card;
  }

  function licenseList(licenses, adminView = false) {
    const results = h('div', { class: 'license-list', 'aria-live': 'polite' });
    const search = input('licenseSearch', 'search', '', { required: false, placeholder: text('Aplicação, organização ou código', 'Application, organization or code', 'Aplicación, organización o código') });
    const update = () => {
      const query = search.value.trim().toLocaleLowerCase(state.lang);
      const filtered = licenses.filter(item => [item.applicationName, item.organizationName, item.publicCode].some(value => value.toLocaleLowerCase(state.lang).includes(query)));
      results.replaceChildren(...(filtered.length ? filtered.map(item => licenseCard(item, adminView)) : [empty(text('Nenhuma licença encontrada.', 'No licenses found.', 'No se encontraron licencias.'))]));
    };
    search.addEventListener('input', update); update();
    return h('div', {}, [field(text('Buscar licenças', 'Find licenses', 'Buscar licencias'), search), results]);
  }

  function activationCard(activation, adminView = false) {
    const revoke = actionButton(text('Desativar instalação', 'Deactivate installation', 'Desactivar instalación'), async () => {
      if (!window.confirm(text('Impedir novas renovações desta instalação? A autorização já emitida continua até o vencimento, por no máximo 72 horas.', 'Block further renewals for this installation? Its issued authorization remains valid until expiry, for at most 72 hours.', '¿Impedir nuevas renovaciones de esta instalación? La autorización emitida sigue vigente hasta su vencimiento, como máximo 72 horas.'))) return;
      await api(adminView ? `/admin/licensing/activations/${activation.id}/revoke` : `/licensing/activations/${activation.id}`, adminView ? 'POST' : 'DELETE', adminView ? { reason: 'portal-admin-deactivated' } : undefined);
      feedback(text('Instalação desativada.', 'Installation deactivated.', 'Instalación desactivada.')); await render();
    }, 'button danger');
    return h('article', { class: 'license-activation' }, [
      h('div', { class: 'license-heading' }, [h('div', {}, [h('h3', {}, activation.applicationName || activation.userName), activation.organizationName && p(activation.organizationName)]), statusBadge(activation.status)]),
      facts([
        [text('Ativação', 'Activation', 'Activación'), h('code', {}, activation.id)],
        [adminView ? text('Usuário', 'Username', 'Usuario') : text('Código da licença', 'License code', 'Código de licencia'), adminView ? activation.userName : activation.licenseCode],
        [text('Criada em', 'Created', 'Creada el'), timestamp(activation.createdAtUtc)],
        [text('Última renovação', 'Last renewal', 'Última renovación'), timestamp(activation.lastRenewedAtUtc)]
      ]),
      activation.status !== 'revoked' ? revoke : null
    ]);
  }

  async function organizationWorkspace(organizations) {
    const host = h('div', { class: 'organization-workspace' });
    const canCreate = state.session.user.profileComplete;
    const create = canCreate ? form([
      field(text('Nome da organização ou titular', 'Organization or account holder name', 'Nombre de la organización o titular'), input('name', 'text', '', { maxLength: 200 }))
    ], text('Criar organização', 'Create organization', 'Crear organización'), async data => {
      const result = await api('/licensing/organizations', 'POST', data);
      selectedOrganizationId = result.id; await session();
      feedback(text('Organização criada.', 'Organization created.', 'Organización creada.')); await render();
    }) : h('div', {}, [p(text('Complete seu perfil para criar uma organização.', 'Complete your profile to create an organization.', 'Completa tu perfil para crear una organización.')), button(text('Minha conta', 'My account', 'Mi cuenta'), () => navigate('profile'))]);
    const createDisclosure = disclosure(text('Nova organização', 'New organization', 'Nueva organización'), [
      p(text('Use uma organização para reunir aplicações, licenças e pessoas. Para uso pessoal, informe seu próprio nome.', 'Use an organization to group applications, licenses and people. For personal use, enter your own name.', 'Utiliza una organización para reunir aplicaciones, licencias y personas. Para uso personal, indica tu propio nombre.')), create
    ], organizations.length === 0);
    if (!organizations.length) {
      host.append(empty(text('Você ainda não participa de uma organização.', 'You do not belong to an organization yet.', 'Todavía no perteneces a una organización.')), createDisclosure);
      return host;
    }
    const selected = organizations.find(item => item.id === selectedOrganizationId) || organizations[0];
    selectedOrganizationId = selected.id;
    const picker = select('organization', organizations.map(item => [item.id, item.name]), selected.id);
    const detail = h('div'); let requestVersion = 0;
    const load = async () => {
      const version = ++requestVersion;
      selectedOrganizationId = picker.value;
      const organization = organizations.find(item => item.id === picker.value);
      detail.replaceChildren(p(text('Carregando…', 'Loading…', 'Cargando…')));
      try {
        const [applications, members] = await Promise.all([
          api(`/licensing/organizations/${organization.id}/applications`),
          api(`/licensing/organizations/${organization.id}/members`)
        ]);
        if (version !== requestVersion) return;
        detail.replaceChildren(organizationDetail(organization, applications, members));
      } catch (error) {
        if (version === requestVersion) detail.replaceChildren(notice(error.message), button(text('Tentar novamente', 'Try again', 'Reintentar'), load));
      }
    };
    picker.addEventListener('change', load);
    host.append(field(text('Organização', 'Organization', 'Organización'), picker), detail, createDisclosure);
    await load(); return host;
  }

  function organizationDetail(organization, applications, members) {
    const canManage = ['Owner', 'Admin', 'PortalOwner'].includes(organization.role);
    const canAddAdmin = ['Owner', 'PortalOwner'].includes(organization.role);
    const apps = panel(text('Aplicações', 'Applications', 'Aplicaciones'),
      p(text('Cada licença pertence a uma aplicação identificada pelo nome.', 'Each license belongs to a named application.', 'Cada licencia pertenece a una aplicación identificada por su nombre.')),
      applications.length ? h('ul', { class: 'license-application-list' }, applications.map(item => h('li', {}, item.name))) : empty(text('Nenhuma aplicação cadastrada.', 'No applications registered.', 'No hay aplicaciones registradas.')),
      canManage ? form([field(text('Nome da aplicação', 'Application name', 'Nombre de la aplicación'), input('name', 'text', '', { maxLength: 120 }))], text('Adicionar aplicação', 'Add application', 'Añadir aplicación'), async data => {
        await api(`/licensing/organizations/${organization.id}/applications`, 'POST', data);
        feedback(text('Aplicação adicionada.', 'Application added.', 'Aplicación añadida.')); await render();
      }) : null);
    const memberCards = members.map(member => h('div', { class: 'organization-member' }, [
      h('div', {}, [h('strong', {}, member.displayName || member.userName), p(`${member.userName} · ${roleName(member.role)}`)]),
      canManage && member.role !== 'Owner' ? actionButton(text('Remover', 'Remove', 'Eliminar'), async () => {
        if (!window.confirm(text(`Remover ${member.userName} da organização? As credenciais das instalações desse membro serão invalidadas.`, `Remove ${member.userName} from the organization? Their installation credentials will be invalidated.`, `¿Eliminar a ${member.userName} de la organización? Se invalidarán las credenciales de sus instalaciones.`))) return;
        await api(`/licensing/organizations/${organization.id}/members/${encodeURIComponent(member.id)}`, 'DELETE');
        await session(); feedback(text('Membro removido.', 'Member removed.', 'Miembro eliminado.')); await render();
      }, 'button danger') : null
    ]));
    const memberPanel = panel(text('Pessoas e acesso', 'People and access', 'Personas y acceso'),
      p(`${text('Sua função', 'Your role', 'Tu rol')}: ${roleName(organization.role)}`),
      h('div', { class: 'organization-members' }, memberCards),
      canManage ? form([
        field(text('E-mail ou usuário da conta confirmada', 'Email or username of the confirmed account', 'Correo o usuario de la cuenta confirmada'), input('userIdentifier', 'text', '', { maxLength: 256, autocomplete: 'off' })),
        field(text('Função na organização', 'Organization role', 'Rol en la organización'), select('role', [['Member', roleName('Member')], ...(canAddAdmin ? [['Admin', roleName('Admin')]] : [])], 'Member'))
      ], text('Adicionar membro', 'Add member', 'Añadir miembro'), async data => {
        await api(`/licensing/organizations/${organization.id}/members`, 'POST', data);
        feedback(text('Membro adicionado.', 'Member added.', 'Miembro añadido.')); await render();
      }) : null);
    return h('div', { class: 'content-grid organization-panels' }, [apps, memberPanel]);
  }

  function serviceNotice(status) {
    return notice(status.onlineActivationAvailable
      ? text('Ative o SCADIVO pelo próprio produto com seu usuário ou e-mail, senha e código da licença. A renovação ocorre a cada 48 horas e a autorização vale por até 72 horas.', 'Activate SCADIVO in the product with your username or email, password and license code. Renewal occurs every 48 hours and the authorization lasts up to 72 hours.', 'Activa SCADIVO desde el producto con tu usuario o correo, contraseña y código de licencia. La renovación se realiza cada 48 horas y la autorización dura hasta 72 horas.')
      : text('Você já pode organizar aplicações e consultar os direitos de uso. A ativação online no produto está em preparação.', 'You can organize applications and view entitlements now. Online product activation is being prepared.', 'Ya puedes organizar aplicaciones y consultar los derechos de uso. La activación en línea del producto está en preparación.'));
  }

  async function customer() {
    const [snapshot, activationSnapshot, organizations, status] = await Promise.all([
      api('/licensing/licenses'), api('/licensing/activations'), api('/licensing/organizations'), api('/licensing/status')
    ]);
    const activations = activationSnapshot.activations;
    return h('div', { class: 'licensing-page' }, [
      serviceNotice(status),
      panel(text('Suas licenças', 'Your licenses', 'Tus licencias'),
        p(text('O SCADIVO é gratuito para até 200 TAGs. Os direitos adicionais da sua conta aparecem abaixo, por aplicação.', 'SCADIVO is free for up to 200 tags. Your account’s additional entitlements appear below, by application.', 'SCADIVO es gratuito hasta 200 TAGs. Los derechos adicionales de tu cuenta aparecen abajo, por aplicación.')),
        h('p', { class: 'muted' }, `${text('Atualizado em', 'As of', 'Actualizado el')}: ${date(snapshot.asOfUtc)}`),
        snapshot.licenses.length ? licenseList(snapshot.licenses) : empty(text('Sua conta ainda não possui licenças adicionais.', 'Your account has no additional licenses yet.', 'Tu cuenta todavía no tiene licencias adicionales.'))),
      panel(text('Minhas instalações', 'My installations', 'Mis instalaciones'),
        p(text('Aqui aparecem as instalações ativadas com a sua conta. Desativar impede novas renovações; a autorização já emitida vale até seu vencimento.', 'Installations activated with your account appear here. Deactivation blocks further renewals; an issued authorization remains valid until expiry.', 'Aquí aparecen las instalaciones activadas con tu cuenta. Desactivar impide nuevas renovaciones; la autorización emitida sigue vigente hasta su vencimiento.')),
        activations.length ? h('div', { class: 'license-list' }, activations.map(item => activationCard(item))) : empty(text('Nenhuma instalação ativada com sua conta.', 'No installations activated with your account.', 'No hay instalaciones activadas con tu cuenta.'))),
      panel(text('Organizações e aplicações', 'Organizations and applications', 'Organizaciones y aplicaciones'), await organizationWorkspace(organizations))
    ]);
  }

  function adminLicenseActions(license) {
    const activations = h('div'); let loaded = false;
    const detail = disclosure(text('Gerenciar licença', 'Manage license', 'Gestionar licencia'), [activations]);
    detail.addEventListener('toggle', async () => {
      if (!detail.open || loaded) return;
      loaded = true; activations.replaceChildren(p(text('Carregando…', 'Loading…', 'Cargando…')));
      try {
        const items = await api(`/admin/licensing/licenses/${license.id}/activations`);
        activations.replaceChildren(h('h4', {}, text('Instalações desta licença', 'Installations for this license', 'Instalaciones de esta licencia')),
          ...(items.length ? items.map(item => activationCard(item, true)) : [empty(text('Nenhuma instalação vinculada.', 'No linked installations.', 'No hay instalaciones vinculadas.'))]));
      } catch (error) { loaded = false; activations.replaceChildren(notice(error.message)); }
    });
    const body = detail.querySelector('div');
    if (license.status !== 'revoked' && license.term !== 'perpetual') body.append(form([
      field(text('Prorrogar direito de uso', 'Extend entitlement', 'Ampliar derecho de uso'), select('term', termOptions().filter(([value]) => value !== 'perpetual'), '', { required: true, 'aria-label': text('Prorrogar direito de uso', 'Extend entitlement', 'Ampliar derecho de uso') })),
      p(text('Acrescenta o período ao vencimento atual ou, se expirado, a partir de agora. Não realiza cobrança.', 'Adds the period to the current expiry or, if expired, from now. No payment is processed.', 'Añade el período al vencimiento actual o, si ha caducado, desde ahora. No procesa ningún cobro.'))
    ], text('Prorrogar licença', 'Extend license', 'Ampliar licencia'), async data => {
      await api(`/admin/licensing/licenses/${license.id}/extend`, 'POST', data);
      feedback(text('Licença prorrogada.', 'License extended.', 'Licencia ampliada.')); await render();
    }));
    if (license.status !== 'revoked') body.append(disclosure(text('Revogar licença', 'Revoke license', 'Revocar licencia'), [
      p(text('Revogar bloqueia novas ativações e renovações de todas as instalações desta licença. Autorizações já emitidas continuam até o vencimento, por no máximo 72 horas.', 'Revocation blocks new activations and renewals for every installation on this license. Issued authorizations remain valid until expiry, for at most 72 hours.', 'La revocación bloquea nuevas activaciones y renovaciones de todas las instalaciones de esta licencia. Las autorizaciones emitidas siguen vigentes hasta su vencimiento, como máximo 72 horas.')),
      form([field(text('Motivo', 'Reason', 'Motivo'), textarea('reason', '', { rows: 2, maxLength: 240 }))], text('Confirmar revogação', 'Confirm revocation', 'Confirmar revocación'), async data => {
        if (!window.confirm(`${license.publicCode}\n\n${text('Revogar esta licença?', 'Revoke this license?', '¿Revocar esta licencia?')}`)) return;
        await api(`/admin/licensing/licenses/${license.id}/revoke`, 'POST', data);
        feedback(text('Licença revogada.', 'License revoked.', 'Licencia revocada.')); await render();
      })
    ]));
    return detail;
  }

  function issueForm(organizations) {
    const organization = select('organizationId', [['', text('Selecione a organização', 'Select the organization', 'Selecciona la organización')], ...organizations.map(item => [item.id, item.name])], '', { required: true, 'aria-label': text('Organização', 'Organization', 'Organización') });
    const application = select('applicationId', [], '', { required: true, disabled: true, 'aria-label': text('Aplicação', 'Application', 'Aplicación') });
    const submitStatus = h('p', { class: 'muted', role: 'status' });
    const node = form([
      h('div', { class: 'form-row' }, [field(text('Organização', 'Organization', 'Organización'), organization), field(text('Aplicação', 'Application', 'Aplicación'), application)]),
      h('div', { class: 'form-row' }, [field(text('Capacidade de TAGs', 'Tag capacity', 'Capacidad de TAGs'), select('tier', tierOptions(), '', { required: true })), field(text('Prazo do direito de uso', 'Entitlement term', 'Plazo del derecho de uso'), select('term', termOptions(), '', { required: true, 'aria-label': text('Prazo do direito de uso', 'Entitlement term', 'Plazo del derecho de uso') }))]),
      h('div', { class: 'form-row' }, [field(text('Total de sessões Interactive', 'Total Interactive sessions', 'Total de sesiones Interactive'), input('interactiveSeats', 'number', '', { min: 0, max: 2147483647, step: 1 })), field(text('Total de sessões View Only', 'Total View Only sessions', 'Total de sesiones View Only'), input('viewOnlySeats', 'number', '', { min: 0, max: 2147483647, step: 1 }))]),
      field(text('Limite de instalações ativadas', 'Activated installation limit', 'Límite de instalaciones activadas'), input('maxActivations', 'number', '', { min: 1, max: 2147483647, step: 1 }), text('Informe o limite aprovado. Cada nó de redundância precisa de uma ativação própria.', 'Enter the approved limit. Each redundancy node needs its own activation.', 'Indica el límite aprobado. Cada nodo de redundancia necesita su propia activación.')),
      check('haRuntime', text('Incluir redundância / HA Runtime', 'Include redundancy / HA Runtime', 'Incluir redundancia / HA Runtime')),
      check('assetProcessIntelligence', text('Incluir Inteligência de Ativos', 'Include Asset Process Intelligence', 'Incluir Inteligencia de Activos')),
      p(text('Os números de sessões são totais efetivos compartilhados por Web Runtime e EliteGO. Cadastrar o direito não cobra nem instala uma licença no produto.', 'Session numbers are effective totals shared by Web Runtime and EliteGO. Creating the entitlement does not charge or install a product license.', 'Las sesiones son totales efectivos compartidos por Web Runtime y EliteGO. Registrar el derecho no realiza cobros ni instala una licencia en el producto.')),
      submitStatus
    ], text('Cadastrar direito de uso', 'Create entitlement', 'Registrar derecho de uso'), async data => {
      await api('/admin/licensing/licenses', 'POST', {
        applicationId: data.applicationId, tier: data.tier, term: data.term,
        interactiveSeats: Number(data.interactiveSeats), viewOnlySeats: Number(data.viewOnlySeats),
        maxActivations: Number(data.maxActivations), haRuntime: data.haRuntime, assetProcessIntelligence: data.assetProcessIntelligence
      });
      feedback(text('Direito de uso cadastrado.', 'Entitlement created.', 'Derecho de uso registrado.')); await render();
    });
    const submit = node.querySelector('button[type="submit"]'); submit.disabled = true;
    let requestVersion = 0;
    organization.addEventListener('change', async () => {
      const version = ++requestVersion;
      application.replaceChildren(); application.disabled = true; submit.disabled = true;
      submitStatus.textContent = '';
      if (!organization.value) return;
      submitStatus.textContent = text('Carregando aplicações…', 'Loading applications…', 'Cargando aplicaciones…');
      try {
        const applications = await api(`/licensing/organizations/${organization.value}/applications`);
        if (version !== requestVersion) return;
        application.replaceChildren(...[['', text('Selecione a aplicação', 'Select the application', 'Selecciona la aplicación')], ...applications.map(item => [item.id, item.name])].map(([value, label]) => h('option', { value }, label)));
        application.disabled = applications.length === 0; submit.disabled = applications.length === 0;
        submitStatus.textContent = applications.length ? '' : text('Cadastre uma aplicação na aba Licenças antes de continuar.', 'Register an application in the Licenses tab before continuing.', 'Registra una aplicación en la pestaña Licencias antes de continuar.');
      } catch (error) { if (version === requestVersion) submitStatus.textContent = error.message; }
    });
    return node;
  }

  async function admin() {
    const [organizations, snapshot, status] = await Promise.all([api('/admin/licensing/organizations'), api('/admin/licensing/licenses'), api('/licensing/status')]);
    return h('div', { class: 'licensing-page' }, [
      serviceNotice(status),
      panel(text('Direitos de uso', 'License entitlements', 'Derechos de uso'),
        h('p', { class: 'muted' }, `${text('Atualizado em', 'As of', 'Actualizado el')}: ${date(snapshot.asOfUtc)}`),
        snapshot.licenses.length ? licenseList(snapshot.licenses, true) : empty(text('Nenhum direito de uso cadastrado.', 'No entitlements registered.', 'No hay derechos de uso registrados.'))),
      panel(text('Novo direito de uso', 'New entitlement', 'Nuevo derecho de uso'),
        organizations.length ? issueForm(organizations) : h('div', {}, [p(text('Crie uma organização e uma aplicação na aba Licenças.', 'Create an organization and an application in the Licenses tab.', 'Crea una organización y una aplicación en la pestaña Licencias.')), button(text('Abrir Licenças', 'Open Licenses', 'Abrir Licencias'), () => navigate('licenses'))]))
    ]);
  }

  return { customer, admin, errors };
})();
