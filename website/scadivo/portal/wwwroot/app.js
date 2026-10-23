'use strict';

const state = { session: null, lang: 'pt-BR', tab: 'home', adminTab: 'accounts', invite: null, version: 0 };
try { state.lang = localStorage.getItem('scadivo.portal.lang') === 'en' ? 'en' : 'pt-BR'; } catch { /* Storage may be disabled. */ }
const app = document.querySelector('#app');
const tr = (pt, en) => state.lang === 'en' ? en : pt;
const date = value => new Date(value).toLocaleString(state.lang === 'en' ? 'en-US' : 'pt-BR');
const labels = () => ({ home: tr('Visão geral', 'Overview'), profile: tr('Minha conta', 'My account'), support: tr('Suporte', 'Support'), downloads: 'Downloads', faq: 'FAQ', community: tr('Comunidade', 'Community'), licenses: tr('Licenças', 'Licenses'), admin: tr('Administração', 'Administration') });

function h(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (key.startsWith('on')) node.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key === 'class') node.className = value;
    else if (key === 'text') node.textContent = value;
    else if (key in node && !key.startsWith('aria')) node[key] = value;
    else node.setAttribute(key, value);
  }
  for (const child of [children].flat(Infinity)) if (child != null) node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  return node;
}
const p = text => h('p', {}, text);
const button = (text, action, className = 'button secondary') => h('button', { type: 'button', class: className, onclick: action }, text);
const notice = text => h('div', { class: 'notice' }, text);
const badge = text => h('span', { class: 'badge' }, text);
const panel = (title, ...children) => h('section', { class: 'panel' }, [h('h2', {}, title), ...children]);
const field = (label, input, hint) => h('label', { class: 'field' }, [h('span', {}, label), input, hint && h('small', {}, hint)]);
const input = (name, type = 'text', value = '', extra = {}) => h('input', { name, type, value, required: true, ...extra });
const textarea = (name, value = '', extra = {}) => h('textarea', { name, value, rows: 5, required: true, ...extra });
const select = (name, entries, value) => h('select', { name }, entries.map(([id, text]) => h('option', { value: id, selected: id === value }, text)));
const values = form => Object.fromEntries(new FormData(form));
const statusName = status => ({ open: tr('Aberto', 'Open'), answered: tr('Respondido', 'Answered'), closed: tr('Encerrado', 'Closed') })[status] || status;

function feedback(message, error = false) {
  const node = document.querySelector('#feedback');
  node.textContent = message; node.className = error ? 'error' : 'success'; node.hidden = false;
  clearTimeout(feedback.timer); feedback.timer = setTimeout(() => { node.hidden = true; }, 6500);
}
function errorText(code) {
  const errors = {
    'invalid-credentials': tr('Usuário ou senha inválidos. Após várias tentativas, aguarde 15 minutos.', 'Invalid username or password. After repeated attempts, wait 15 minutes.'),
    'invalid-input': tr('Confira os campos informados.', 'Check the information entered.'),
    'invalid-request-token': tr('A sessão mudou. Atualize a página e tente novamente.', 'The session changed. Refresh this page and try again.'),
    'account-already-exists': tr('Esse usuário ou e-mail já está cadastrado.', 'This username or email is already registered.'),
    'account-not-created': tr('Use um nome de usuário com letras, números, ponto ou hífen.', 'Use a username with letters, numbers, a dot or a hyphen.'),
    'password-not-changed': tr('Confira a senha atual. A nova senha precisa ter 12 caracteres, maiúscula, minúscula e número.', 'Check your current password. The new password needs 12 characters, uppercase, lowercase and a number.'),
    'invalid-invitation': tr('Convite inválido ou já utilizado.', 'Invalid or already used invitation.'),
    'invalid-invitation-or-password': tr('Convite expirado ou senha fora dos requisitos.', 'The invitation expired or the password does not meet the requirements.'),
    'slug-already-exists': tr('Esse endereço de artigo já existe.', 'This article slug already exists.')
  };
  return errors[code] || tr('Não foi possível concluir. Tente novamente.', 'Unable to complete. Please try again.');
}
async function api(path, method = 'GET', body) {
  const response = await fetch(`/portal/api${path}`, { method, credentials: 'same-origin', headers: method === 'GET' ? {} : { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': state.session?.csrfToken || '' }, body: body == null ? undefined : JSON.stringify(body) });
  const result = await response.json().catch(() => ({}));
  if (!response.ok) {
    if (response.status === 401 && !path.includes('/auth/login')) {
      state.session = await fetch('/portal/api/session', { credentials: 'same-origin' }).then(r => r.json());
      render();
    }
    throw new Error(response.status === 429 ? tr('Muitas tentativas. Aguarde um minuto.', 'Too many attempts. Wait one minute.') : response.status === 403 ? tr('Acesso reservado à administração.', 'Administrator access required.') : errorText(result.error));
  }
  return result;
}
async function session() { state.session = await api('/session'); }
function form(children, submitText, action) {
  const submit = h('button', { type: 'submit', class: 'button primary' }, submitText);
  const node = h('form', { class: 'form-stack', onsubmit: async event => {
    event.preventDefault(); submit.disabled = true;
    try { await action(values(node), node); } catch (error) { feedback(error.message, true); }
    finally { submit.disabled = false; }
  } }, [...children, submit]);
  return node;
}
function table(headers, rows) {
  return h('div', { class: 'table-wrap' }, h('table', {}, [h('thead', {}, h('tr', {}, headers.map(x => h('th', { scope: 'col' }, x)))), h('tbody', {}, rows.map(row => h('tr', {}, row.map(cell => h('td', {}, cell)))))]));
}
function navigate(tab) { state.tab = tab; history.replaceState(null, '', `/portal/#${tab}`); render(); }

function renderHeader() {
  document.documentElement.lang = state.lang;
  document.querySelector('#demo-link').textContent = tr('Demonstração ↗', 'Live demo ↗');
  document.querySelector('#language-button').textContent = state.lang === 'en' ? 'PT' : 'EN';
  document.querySelector('#language-button').onclick = () => { state.lang = state.lang === 'en' ? 'pt-BR' : 'en'; try { localStorage.setItem('scadivo.portal.lang', state.lang); } catch { /* Optional storage. */ } render(); };
  const actions = document.querySelector('#account-actions');
  actions.replaceChildren();
  if (state.session?.authenticated) actions.append(h('span', { class: 'account-chip' }, state.session.user.displayName), button(tr('Sair', 'Sign out'), async () => {
    try { await api('/auth/logout', 'POST', {}); await session(); state.tab = 'home'; render(); } catch (error) { feedback(error.message, true); }
  }));
}
function login() {
  const intro = h('section', { class: 'login-intro' }, [badge(tr('ÁREA DO CLIENTE · PILOTO', 'CUSTOMER PORTAL · PILOT')), h('h1', {}, tr('Uma conta. Mais conexão com seu projeto.', 'One account. Closer to your project.')), p(tr('Suporte, conhecimento e distribuição oficial do SCADIVO, em um ambiente próprio para clientes e integradores.', 'SCADIVO support, knowledge and official distribution, in a dedicated environment for customers and integrators.')), h('div', { class: 'intro-features' }, [panel(tr('Suporte privado', 'Private support'), p(tr('Converse com a equipe e acompanhe seus chamados.', 'Talk to the team and track your requests.'))), panel(tr('Conhecimento e downloads', 'Knowledge and downloads'), p(tr('FAQ pesquisável e catálogo de versões oficiais.', 'Searchable FAQ and official release catalog.')))]), notice(tr('Este portal tem contas próprias. O acesso à engenharia da demonstração é independente.', 'This portal has its own accounts. Demo engineering access is separate.'))]);
  const card = panel(tr('Entre no portal', 'Sign in to the portal'), p(tr('Use a conta criada por convite nesta etapa de implantação.', 'Use your invited account during this rollout.')), form([
    field(tr('Usuário', 'Username'), input('username', 'text', '', { autocomplete: 'username', maxLength: 100 })),
    field(tr('Senha', 'Password'), input('password', 'password', '', { autocomplete: 'current-password', maxLength: 1024 }))
  ], tr('Entrar', 'Sign in'), async data => { await api('/auth/login', 'POST', data); await session(); render(); }), h('div', { class: 'login-note' }, tr('Cadastro público e recuperação por e-mail serão liberados após a configuração do serviço de e-mail e dos termos de uso. Se precisar de acesso, solicite um convite ao responsável SCADIVO.', 'Public registration and email recovery will open after email delivery and terms are configured. To request access, contact your SCADIVO representative.')));
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'login-layout' }, [intro, card]));
}
function invitation() {
  app.replaceChildren(h('div', { class: 'invite-layout' }, panel(tr('Bem-vindo ao SCADIVO', 'Welcome to SCADIVO'), p(tr('Defina a senha da sua conta convidada. O link é pessoal e válido por 24 horas.', 'Set the password for your invited account. The personal link is valid for 24 hours.')), form([
    field(tr('Nova senha', 'New password'), input('password', 'password', '', { autocomplete: 'new-password', minLength: 12, maxLength: 1024 }), tr('Mínimo de 12 caracteres, maiúscula, minúscula e número.', 'At least 12 characters, uppercase, lowercase and a number.')),
    field(tr('Repita a senha', 'Repeat password'), input('confirmation', 'password', '', { autocomplete: 'new-password', minLength: 12 }))
  ], tr('Ativar minha conta', 'Activate my account'), async data => {
    if (data.password !== data.confirmation) throw new Error(tr('As senhas não coincidem.', 'The passwords do not match.'));
    await api('/auth/accept-invitation', 'POST', { ...state.invite, password: data.password }); state.invite = null;
    history.replaceState(null, '', '/portal/'); await session(); render(); feedback(tr('Conta ativada. Entre com seu usuário e senha.', 'Account activated. Sign in with your username and password.'));
  }))));
}
async function render() {
  const version = ++state.version; renderHeader();
  if (state.invite) return invitation();
  if (!state.session?.authenticated) return login();
  const tabs = labels();
  if (!tabs[state.tab] || (state.tab === 'admin' && !state.session.user.owner)) state.tab = 'home';
  const nav = h('nav', { class: 'portal-nav', 'aria-label': tr('Portal do cliente', 'Customer portal') }, Object.entries(tabs).filter(([id]) => id !== 'admin' || state.session.user.owner).map(([id, label]) => button(label, () => navigate(id), `nav-item${state.tab === id ? ' active' : ''}`)));
  const content = h('div', { class: 'portal-content' }, [h('div', { class: 'page-heading' }, [h('div', {}, [h('p', { class: 'eyebrow' }, 'SCADIVO PORTAL'), h('h1', {}, tabs[state.tab])]), badge(tr('PILOTO', 'PILOT'))]), h('div', { class: 'content-body' }, p(tr('Carregando…', 'Loading…')))]);
  app.replaceChildren(h('div', { class: 'portal-layout' }, [h('aside', { class: 'portal-sidebar' }, [nav, p(tr('Seu projeto, conectado.', 'Your project, connected.'))]), content]));
  const body = content.querySelector('.content-body');
  try {
    const node = await ({ home, profile, support, downloads, faq, community, licenses, admin })[state.tab]();
    if (version === state.version) body.replaceChildren(node);
  } catch (error) { if (version === state.version) body.replaceChildren(notice(error.message), button(tr('Tentar novamente', 'Try again'), render)); }
}
function home() {
  return h('div', {}, [panel(`${tr('Olá', 'Hello')}, ${state.session.user.displayName}.`, p(tr('Este é seu ponto de encontro com o SCADIVO. Comece pelo conhecimento, abra uma conversa privada ou acompanhe as versões disponibilizadas pela equipe.', 'This is your SCADIVO hub. Explore the knowledge base, start a private conversation or follow releases made available by the team.'))), h('div', { class: 'content-grid' }, ['faq', 'support', 'downloads', 'profile'].map(id => panel(labels()[id], p(({ faq: tr('Encontre respostas sobre implantação e uso.', 'Find answers about deployment and use.'), support: tr('Acompanhe suas conversas com a equipe.', 'Follow your conversations with the team.'), downloads: tr('Somente arquivos de distribuição aprovados.', 'Approved distribution files only.'), profile: tr('Mantenha seu perfil e senha atualizados.', 'Keep your profile and password up to date.') })[id]), button(tr('Acessar →', 'Open →'), () => navigate(id))))), notice(tr('Comunidade, compras e licenciamento online estão nas próximas etapas. Nenhuma licença do produto é alterada por este portal nesta fase.', 'Community, purchasing and online licensing are upcoming stages. This portal does not change any product license in this phase.'))]);
}
function profile() {
  const user = state.session.user;
  return h('div', { class: 'content-grid' }, [panel(tr('Seu perfil', 'Your profile'), p(`${tr('Usuário', 'Username')}: ${user.userName}`), p(`${tr('E-mail', 'Email')}: ${user.email || tr('Ainda não informado', 'Not provided yet')}`), form([
    field(tr('Nome de exibição', 'Display name'), input('displayName', 'text', user.displayName, { maxLength: 160 })),
    field(tr('Idioma preferido', 'Preferred language'), select('locale', [['pt-BR', 'Português'], ['en', 'English']], user.locale))
  ], tr('Salvar perfil', 'Save profile'), async data => { await api('/profile', 'PUT', data); await session(); feedback(tr('Perfil atualizado.', 'Profile updated.')); render(); }), h('div', { class: 'organization-list' }, user.organizations.map(org => p(`${org.name} · ${org.role}`)))), panel(tr('Alterar senha', 'Change password'), form([
    field(tr('Senha atual', 'Current password'), input('currentPassword', 'password', '', { autocomplete: 'current-password' })),
    field(tr('Nova senha', 'New password'), input('newPassword', 'password', '', { autocomplete: 'new-password', minLength: 12 }), tr('12 caracteres, maiúscula, minúscula e número.', '12 characters, uppercase, lowercase and a number.')),
    field(tr('Repita a nova senha', 'Repeat new password'), input('confirmation', 'password', '', { autocomplete: 'new-password', minLength: 12 }))
  ], tr('Atualizar senha', 'Update password'), async (data, node) => { if (data.newPassword !== data.confirmation) throw new Error(tr('As senhas não coincidem.', 'The passwords do not match.')); await api('/profile/password', 'POST', data); await session(); node.reset(); feedback(tr('Senha atualizada.', 'Password updated.')); }))]);
}
async function support() {
  const tickets = await api('/support');
  const list = panel(tr('Conversas', 'Conversations'), state.session.user.owner ? p(tr('Você vê os chamados de todos os clientes como administrador.', 'As an administrator, you see all customer requests.')) : p(tr('Somente você e a equipe podem acessar seus chamados.', 'Only you and the team can access your requests.')));
  if (!tickets.length) list.append(p(tr('Nenhum chamado por enquanto.', 'No requests yet.')));
  for (const ticket of tickets) list.append(button(`${ticket.subject} · ${statusName(ticket.status)} · ${date(ticket.updatedAtUtc)}`, () => openTicket(ticket.id, list), 'ticket-button'));
  const create = panel(tr('Novo chamado', 'New request'), form([
    field(tr('Assunto', 'Subject'), input('subject', 'text', '', { minLength: 3, maxLength: 180 })),
    field(tr('Categoria', 'Category'), select('category', [['technical', tr('Técnico', 'Technical')], ['commercial', tr('Comercial', 'Sales')], ['account', tr('Conta e acesso', 'Account and access')]], 'technical')),
    field(tr('Descreva sua dúvida', 'Describe your question'), textarea('body', '', { minLength: 10, maxLength: 8000 }), tr('Não inclua senhas, chaves privadas ou dados sensíveis da sua planta.', 'Do not include passwords, private keys or sensitive plant data.'))
  ], tr('Abrir chamado', 'Create request'), async data => { await api('/support', 'POST', data); feedback(tr('Chamado registrado. Acompanhe as respostas aqui no portal.', 'Request saved. Follow replies here in the portal.')); render(); }));
  return h('div', { class: 'content-grid' }, [list, create]);
}
async function openTicket(id, target) {
  try {
    const result = await api(`/support/${id}`); const ticket = result.ticket;
    target.replaceChildren(button(tr('← Voltar', '← Back'), render), h('h2', {}, ticket.subject), badge(statusName(ticket.status)), h('div', { class: 'message-list' }, result.messages.map(message => h('article', { class: `message${message.staffReply ? ' staff' : ''}` }, [h('small', {}, `${message.staffReply ? 'SCADIVO' : tr('Cliente', 'Customer')} · ${date(message.createdAtUtc)}`), h('p', { class: 'preserve-lines' }, message.body)]))), form([field(tr('Sua mensagem', 'Your message'), textarea('body', '', { maxLength: 8000 }))], tr('Enviar resposta', 'Send reply'), async data => { await api(`/support/${id}/messages`, 'POST', data); await openTicket(id, target); }));
    if (state.session.user.owner) target.append(form([field(tr('Estado do chamado', 'Request status'), select('status', ['open', 'answered', 'closed'].map(s => [s, statusName(s)]), ticket.status))], tr('Atualizar estado', 'Update status'), async data => { await api(`/admin/support/${id}/status`, 'PUT', data); await openTicket(id, target); }));
  } catch (error) { feedback(error.message, true); }
}
async function downloads() {
  const releases = await api('/downloads');
  return panel(tr('Distribuição oficial', 'Official distribution'), p(tr('Os instaladores Windows, Linux e as instruções Docker serão publicados aqui quando cada distribuição oficial estiver aprovada.', 'Windows and Linux installers and Docker instructions will appear here when each official distribution is approved.')), releases.length ? h('div', { class: 'content-grid' }, releases.map(release => panel(`${release.platform} · ${release.version}`, p(state.lang === 'en' ? release.notesEn : release.notesPt), h('code', { class: 'checksum' }, `SHA-256 ${release.sha256}`), h('a', { class: 'button primary', href: `/portal/api/downloads/${release.id}/file` }, 'Download ↗')))) : notice(tr('Nenhum instalador publicado nesta etapa. Não há links provisórios ou arquivos não validados.', 'No installer is published at this stage. There are no placeholder links or unverified files.')));
}
async function faq() {
  const result = panel(tr('Como podemos ajudar?', 'How can we help?'));
  const list = h('div', { class: 'faq-list' });
  async function search(query = '') { const articles = await api(`/faq?lang=${state.lang}&q=${encodeURIComponent(query)}`); list.replaceChildren(...articles.map(article => h('details', {}, [h('summary', {}, article.title), h('p', { class: 'preserve-lines' }, article.body)]))); if (!articles.length) list.append(p(tr('Nenhum resultado para esta pesquisa.', 'No results for this search.'))); }
  result.append(form([field(tr('Pesquisar no FAQ', 'Search FAQ'), input('q', 'search', '', { required: false, maxLength: 120, placeholder: tr('Ex.: Docker, TAGs, demonstração', 'E.g. Docker, tags, demo') }))], tr('Pesquisar', 'Search'), data => search(data.q)), list);
  await search(); return result;
}
function community() { return panel(tr('Uma comunidade em construção', 'A community in the making'), badge(tr('PRÓXIMA ETAPA', 'UPCOMING')), p(tr('O fórum usará a mesma conta do portal. As categorias previstas incluem primeiros passos, residencial e predial, aplicações industriais, scripts Python e boas práticas.', 'The forum will use the same portal account. Planned categories include getting started, homes and buildings, industrial applications, Python scripts and best practices.')), notice(tr('A publicação de tópicos será liberada após a configuração das regras, moderação e recuperação de conta.', 'Topic publishing will open after rules, moderation and account recovery are configured.'))); }
function licenses() { return panel(tr('Licenciamento conectado ao seu projeto', 'Licensing connected to your project'), badge(tr('EM PREPARAÇÃO', 'IN PREPARATION')), p(tr('O próximo ciclo prevê catálogo, pagamentos e gestão de licenças. Ainda não há cobrança ou emissão automática por aqui.', 'The next stage covers the catalog, payments and license management. There is no billing or automatic issuance here yet.')), notice(tr('O SCADIVO é gratuito para até 200 TAGs. Licenças por máquina sem conexão permanente continuam previstas; o licenciamento online terá integração própria, sem alterar o produto nesta etapa.', 'SCADIVO is free for up to 200 tags. Machine licenses without a permanent connection remain planned; online licensing will have its own integration, without product changes at this stage.'))); }
async function admin() {
  const overview = await api('/admin/overview');
  const metrics = h('div', { class: 'metrics' }, [[tr('Contas', 'Accounts'), overview.accounts], [tr('Chamados abertos', 'Open requests'), overview.openTickets], [tr('Convites pendentes', 'Pending invitations'), overview.pendingInvitations]].map(([label, value]) => h('div', { class: 'metric' }, [h('strong', {}, value), h('span', {}, label)])));
  const body = h('div');
  const items = { accounts: tr('Contas e convites', 'Accounts and invitations'), templates: tr('Mensagens', 'Messages'), articles: 'FAQ', audit: tr('Auditoria', 'Audit') };
  const tabs = h('nav', { class: 'admin-subnav', 'aria-label': tr('Administração', 'Administration') }, Object.entries(items).map(([key, label]) => button(label, () => { state.adminTab = key; render(); }, `button ${state.adminTab === key ? 'primary' : 'secondary'}`)));
  const node = await ({ accounts: accountsAdmin, templates: templatesAdmin, articles: faqAdmin, audit: auditAdmin })[state.adminTab](); body.append(node);
  return h('div', {}, [metrics, notice(tr(`E-mail ainda sem transporte configurado. ${overview.queuedMessages} notificações registradas, nenhuma enviada automaticamente. Cadastro público, pagamentos e licenciamento online não estão ativos.`, `Email delivery is not configured. ${overview.queuedMessages} notifications recorded, none automatically sent. Public signup, payments and online licensing are not active.`)), tabs, body]);
}
async function accountsAdmin() {
  const accounts = await api('/admin/accounts');
  const result = h('div'); const invitationResult = h('div');
  result.append(panel(tr('Criar convite', 'Create invitation'), p(tr('O convidado define a própria senha. Compartilhe o link por um canal privado; ele não é enviado por e-mail nesta fase.', 'The invitee sets their own password. Share the link privately; it is not emailed at this stage.')), form([
    h('div', { class: 'form-row' }, [field(tr('Usuário', 'Username'), input('username', 'text', '', { minLength: 3, maxLength: 100, autocomplete: 'off' })), field(tr('Nome', 'Name'), input('displayName', 'text', '', { maxLength: 160 }))]),
    h('div', { class: 'form-row' }, [field(tr('E-mail (opcional)', 'Email (optional)'), input('email', 'email', '', { required: false, maxLength: 256 })), field(tr('Organização (opcional)', 'Organization (optional)'), input('organization', 'text', '', { required: false, maxLength: 200 }))])
  ], tr('Gerar convite', 'Generate invitation'), async (data, node) => {
    const result = await api('/admin/invitations', 'POST', data); node.reset();
    invitationResult.replaceChildren(notice(tr(`Convite de ${result.userName}: válido por 24 horas e uso único. Guarde o nome de usuário junto com o link.`, `Invitation for ${result.userName}: valid for 24 hours and one use. Keep the username with the link.`)), field(tr('Link privado', 'Private link'), textarea('invitationLink', result.invitationUrl, { readOnly: true, rows: 3, class: 'private-link' })), button(tr('Copiar link', 'Copy link'), async () => { try { await navigator.clipboard.writeText(result.invitationUrl); feedback(tr('Link copiado.', 'Link copied.')); } catch { feedback(tr('Selecione e copie o link acima.', 'Select and copy the link above.')); } }));
    feedback(tr('Convite criado.', 'Invitation created.'));
  }), invitationResult), panel(tr('Contas do portal', 'Portal accounts'), table([tr('Usuário', 'Username'), tr('Nome', 'Name'), tr('Situação', 'Status'), tr('Criada em', 'Created')], accounts.map(account => [account.userName, account.displayName, badge(account.invitationAccepted ? tr('Ativa', 'Active') : tr('Convite pendente', 'Pending invitation')), date(account.createdAtUtc)]))));
  return result;
}
async function templatesAdmin() {
  const templates = await api('/admin/templates'); const editor = h('div');
  function edit(template) {
    const preview = h('pre', { class: 'template-preview' });
    const node = form([
      h('div', { class: 'content-grid' }, [h('div', {}, [h('h3', {}, 'Português'), field('Assunto', input('subjectPt', 'text', template.subjectPt, { maxLength: 200 })), field('Mensagem', textarea('bodyPt', template.bodyPt, { maxLength: 8000, rows: 8 }))]), h('div', {}, [h('h3', {}, 'English'), field('Subject', input('subjectEn', 'text', template.subjectEn, { maxLength: 200 })), field('Message', textarea('bodyEn', template.bodyEn, { maxLength: 8000, rows: 8 }))])]),
      p(tr('Variáveis: {{name}}, {{link}}, {{reference}}. Somente texto; nenhum script é executado.', 'Variables: {{name}}, {{link}}, {{reference}}. Text only; no script is executed.')), h('h3', {}, tr('Prévia no idioma atual', 'Preview in current language')), preview
    ], tr('Salvar modelo', 'Save template'), async data => { await api(`/admin/templates/${encodeURIComponent(template.key)}`, 'PUT', data); Object.assign(template, data); feedback(tr('Modelo salvo. O envio permanece desativado.', 'Template saved. Delivery remains disabled.')); });
    const update = () => { const data = values(node); preview.textContent = `${state.lang === 'en' ? data.subjectEn : data.subjectPt}\n\n${state.lang === 'en' ? data.bodyEn : data.bodyPt}`.replaceAll('{{name}}', 'Alex').replaceAll('{{reference}}', 'SCADIVO-EXAMPLE').replaceAll('{{link}}', 'https://scadivo.com/portal/'); };
    node.addEventListener('input', update); update(); editor.replaceChildren(h('h3', {}, template.key), node);
  }
  const picker = select('template', templates.map(x => [x.key, x.key]), templates[0].key); picker.addEventListener('change', () => edit(templates.find(x => x.key === picker.value))); edit(templates[0]);
  return panel(tr('Modelos de mensagens automáticas', 'Automatic message templates'), field(tr('Tipo de mensagem', 'Message type'), picker), editor);
}
async function faqAdmin() {
  const articles = await api('/admin/faq'); const editor = h('div');
  function edit(article = {}) {
    const published = input('published', 'checkbox', 'yes', { required: false, checked: article.published ?? true });
    editor.replaceChildren(form([
      h('div', { class: 'form-row' }, [field(tr('Endereço do artigo', 'Article slug'), input('slug', 'text', article.slug || '', { pattern: '[a-z0-9-]+', maxLength: 100 })), field(tr('Categoria', 'Category'), input('category', 'text', article.category || 'getting-started', { maxLength: 60 }))]),
      h('div', { class: 'content-grid' }, [h('div', {}, [field('Título (PT)', input('titlePt', 'text', article.titlePt || '', { maxLength: 200 })), field('Texto (PT)', textarea('bodyPt', article.bodyPt || '', { maxLength: 12000, rows: 9 }))]), h('div', {}, [field('Title (EN)', input('titleEn', 'text', article.titleEn || '', { maxLength: 200 })), field('Text (EN)', textarea('bodyEn', article.bodyEn || '', { maxLength: 12000, rows: 9 }))])]),
      h('label', { class: 'check-field' }, [published, tr('Publicado no FAQ', 'Published in FAQ')])
    ], tr('Salvar artigo', 'Save article'), async data => { await api(`/admin/faq${article.id ? `/${article.id}` : ''}`, article.id ? 'PUT' : 'POST', { ...data, published: published.checked }); feedback(tr('Artigo salvo.', 'Article saved.')); render(); }));
  }
  const picker = select('article', [['', tr('Novo artigo', 'New article')], ...articles.map(x => [x.id, state.lang === 'en' ? x.titleEn : x.titlePt])], ''); picker.addEventListener('change', () => edit(articles.find(x => x.id === picker.value))); edit();
  return panel(tr('Editar base de conhecimento', 'Edit knowledge base'), field(tr('Artigo', 'Article'), picker), editor);
}
async function auditAdmin() {
  const events = await api('/admin/audit');
  return panel(tr('Últimas 100 ações', 'Latest 100 actions'), p(tr('O registro não contém senhas nem códigos de convite.', 'The log contains no passwords or invitation codes.')), table([tr('Data/hora', 'Date/time'), tr('Ação', 'Action'), tr('Referência', 'Reference')], events.map(event => [date(event.createdAtUtc), event.action, event.resourceId || '—'])));
}

async function start() {
  const fragment = location.hash.slice(1);
  if (fragment.startsWith('invite=')) {
    try {
      const encoded = fragment.slice(7).replaceAll('-', '+').replaceAll('_', '/');
      const parsed = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(encoded), x => x.charCodeAt(0))));
      if (typeof parsed.userId !== 'string' || typeof parsed.token !== 'string') throw new Error('Invalid invitation');
      state.invite = { userId: parsed.userId, token: parsed.token };
    } catch { feedback(tr('O link de convite é inválido.', 'This invitation link is invalid.'), true); }
    history.replaceState(null, '', '/portal/#accept-invite');
  } else if (labels()[fragment]) state.tab = fragment;
  try { await session(); render(); } catch { app.replaceChildren(panel(tr('Portal indisponível', 'Portal unavailable'), p(tr('Não foi possível conectar. Aguarde um momento e tente novamente.', 'Unable to connect. Please wait a moment and try again.')), button(tr('Tentar novamente', 'Try again'), start))); }
}
start();
