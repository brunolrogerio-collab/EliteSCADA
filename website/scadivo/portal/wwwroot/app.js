'use strict';

const state = { session: null, lang: 'pt-BR', tab: 'home', adminTab: 'accounts', invite: null, confirmEmail: null, resetPassword: null, communitySso: null, recover: false, register: false, pendingEmail: '', version: 0 };
const communitySsoStorageKey = 'scadivo.portal.community-sso';
try {
  const requested = new URLSearchParams(location.search).get('lang');
  const remembered = localStorage.getItem('scadivo.portal.lang') || localStorage.getItem('scadivo-site-language');
  const preferred = ['en', 'es'].includes(requested) ? requested : remembered;
  state.lang = ['en', 'es'].includes(preferred) ? preferred : 'pt-BR';
} catch { /* Portuguese remains the default when storage is unavailable. */ }
const app = document.querySelector('#app');
const spanish = {
  '← Voltar': '← Volver', 'a página de suporte SCADIVO': 'la página de soporte de SCADIVO',
  'A publicação de tópicos será liberada após a configuração das regras, moderação e recuperação de conta.': 'La publicación de temas se habilitará cuando estén configuradas las reglas, la moderación y la recuperación de cuentas.',
  'A senha precisa ter ao menos 8 caracteres, incluindo maiúscula, minúscula e número.': 'La contraseña debe tener al menos 8 caracteres e incluir una mayúscula, una minúscula y un número.',
  'A sessão mudou. Atualize a página e tente novamente.': 'La sesión cambió. Actualiza la página e inténtalo de nuevo.', 'Aberto': 'Abierto', 'Abrir chamado': 'Crear solicitud', 'Ação': 'Acción', 'Acessar →': 'Abrir →',
  'Acesso reservado à administração.': 'Acceso reservado a la administración.', 'Acompanhe suas conversas com a equipe.': 'Consulta tus conversaciones con el equipo.', 'Administração': 'Administración',
  'Água, saneamento e energia': 'Agua, saneamiento y energía', 'Aguardando ativação': 'Pendiente de activación', 'Ainda não informado': 'Aún no indicado',
  'Alterar senha': 'Cambiar contraseña', 'Aplicações residenciais': 'Aplicaciones residenciales', 'Área de atuação': 'Área de actividad', 'ÁREA DO CLIENTE · PILOTO': 'ÁREA DE CLIENTES · PILOTO',
  'Artigo': 'Artículo', 'Artigo salvo.': 'Artículo guardado.', 'As senhas não coincidem.': 'Las contraseñas no coinciden.', 'Assunto': 'Asunto', 'Ativa': 'Activa',
  'Ativar minha conta': 'Activar mi cuenta', 'Atualizar estado': 'Actualizar estado', 'Atualizar senha': 'Actualizar contraseña', 'Auditoria': 'Auditoría',
  'Automação industrial e processos': 'Automatización industrial y procesos', 'Autônomo/independente': 'Autónomo/independiente', 'Bem-vindo ao SCADIVO': 'Te damos la bienvenida a SCADIVO',
  'Cadastrar e enviar confirmação': 'Registrarme y enviar confirmación', 'Cadastro aberto somente neste ambiente de homologação.': 'El registro público solo está habilitado en este entorno de homologación.',
  'Cadastro público desativado.': 'El registro público está desactivado.', 'Carregando…': 'Cargando…', 'Categoria': 'Categoría',
  'Chamado registrado. Acompanhe as respostas aqui no portal.': 'Solicitud registrada. Consulta las respuestas aquí, en el portal.', 'Chamados abertos': 'Solicitudes abiertas',
  'Cliente': 'Cliente', 'Comercial': 'Ventas', 'Como podemos ajudar?': '¿Cómo podemos ayudarte?', 'Complete seu perfil': 'Completa tu perfil',
  'Complete seu perfil antes de navegar pelo portal.': 'Completa tu perfil antes de navegar por el portal.', 'Comunidade': 'Comunidad',
  'Comunidade, compras e licenciamento online estão nas próximas etapas. Nenhuma licença do produto é alterada por este portal nesta fase.': 'La comunidad, las compras y las licencias en línea llegarán en próximas etapas. En esta fase, el portal no modifica ninguna licencia del producto.',
  'Confira a senha atual. A nova senha precisa ter ao menos 8 caracteres, maiúscula, minúscula e número.': 'Verifica la contraseña actual. La nueva contraseña debe tener al menos 8 caracteres e incluir una mayúscula, una minúscula y un número.',
  'Confira os campos informados.': 'Revisa los campos indicados.', 'Confirmar e-mail': 'Confirmar correo electrónico', 'Confirme seu e-mail': 'Confirma tu correo electrónico',
  'Conhecimento e downloads': 'Conocimiento y descargas', 'Conta ativada. Entre com seu usuário e senha.': 'Cuenta activada. Inicia sesión con tu usuario y contraseña.', 'Conta e acesso': 'Cuenta y acceso',
  'Contas': 'Cuentas', 'Contas do portal': 'Cuentas del portal', 'Contas e convites': 'Cuentas e invitaciones', 'Conversas': 'Conversaciones',
  'Converse com a equipe e acompanhe seus chamados.': 'Habla con el equipo y consulta tus solicitudes.', 'Convite criado e colocado na fila de e-mail.': 'Invitación creada y añadida a la cola de correo electrónico.',
  'Convite criado. Compartilhe o link privado.': 'Invitación creada. Comparte el enlace privado.', 'Convite expirado ou senha fora dos requisitos.': 'La invitación ha caducado o la contraseña no cumple los requisitos.',
  'Convite inválido ou já utilizado.': 'La invitación no es válida o ya se utilizó.', 'Convite pendente': 'Invitación pendiente', 'Copiar link': 'Copiar enlace',
  'Criada em': 'Creada el', 'Criar conta': 'Crear cuenta', 'Criar conta SCADIVO': 'Crear cuenta SCADIVO', 'Criar convite': 'Crear invitación',
  'Data de nascimento (opcional)': 'Fecha de nacimiento (opcional)', 'Data/hora': 'Fecha y hora', 'Defina a senha da sua conta convidada. O link é pessoal e válido por 24 horas.': 'Define la contraseña de tu cuenta de invitado. El enlace es personal y válido durante 24 horas.',
  'Definir nova senha': 'Definir nueva contraseña', 'Demonstração ↗': 'Demostración ↗',
  'Depois do cadastro, enviaremos um link único para confirmar seu e-mail. O login será liberado somente após a confirmação.': 'Después del registro, te enviaremos un enlace único para confirmar tu correo electrónico. Podrás iniciar sesión después de confirmarlo.',
  'Descreva sua dúvida': 'Describe tu consulta', 'Distribuição oficial': 'Distribución oficial', 'E-mail': 'Correo electrónico', 'E-mail (opcional)': 'Correo electrónico (opcional)',
  'E-mail confirmado. Agora você pode entrar com seu e-mail e senha.': 'Correo electrónico confirmado. Ya puedes iniciar sesión con tu correo electrónico y contraseña.',
  'E-mail ou usuário': 'Correo electrónico o usuario', 'Edificações e instalações': 'Edificios e instalaciones', 'Editar base de conhecimento': 'Editar base de conocimiento',
  'EM PREPARAÇÃO': 'EN PREPARACIÓN', 'Empresa, escola ou vínculo (obrigatório)': 'Empresa, institución educativa o vínculo (obligatorio)', 'Encerrado': 'Cerrado',
  'Encontre respostas sobre implantação e uso.': 'Encuentra respuestas sobre la implementación y el uso.', 'Endereço (opcional)': 'Dirección (opcional)', 'Endereço do artigo': 'Dirección del artículo',
  'Ensino e pesquisa': 'Educación e investigación', 'Entrar': 'Iniciar sesión', 'Entre no portal': 'Accede al portal', 'Enviar link de recuperação': 'Enviar enlace de recuperación',
  'Enviar resposta': 'Enviar respuesta', 'Esqueceu a senha?': '¿Olvidaste tu contraseña?', 'Esse endereço de artigo já existe.': 'Esta dirección de artículo ya existe.',
  'Esse usuário ou e-mail já está cadastrado.': 'Este usuario o correo electrónico ya está registrado.', 'Estado do chamado': 'Estado de la solicitud',
  'Este é seu ponto de encontro com o SCADIVO. Comece pelo conhecimento, abra uma conversa privada ou acompanhe as versões disponibilizadas pela equipe.': 'Este es tu punto de encuentro con SCADIVO. Explora la base de conocimiento, inicia una conversación privada o consulta las versiones publicadas por el equipo.',
  'Este link é pessoal, de uso único e expira em 24 horas. Pressione o botão para concluir; apenas abrir o link não ativa a conta.': 'Este enlace es personal, de un solo uso y caduca en 24 horas. Pulsa el botón para continuar; abrir el enlace por sí solo no activa la cuenta.',
  'Este portal tem contas próprias. O acesso à engenharia da demonstração é independente.': 'Este portal tiene sus propias cuentas. El acceso a la ingeniería de la demostración es independiente.', 'Estudante': 'Estudiante',
  'Ex.: Docker, TAGs, demonstração': 'Ej.: Docker, TAGs, demostración', 'FAQ pesquisável e catálogo de versões oficiais.': 'Preguntas frecuentes con búsqueda y catálogo de versiones oficiales.',
  'Downloads': 'Descargas', 'Gerar convite': 'Generar invitación', 'Idioma preferido': 'Idioma preferido',
  'Informe o e-mail da conta. Se houver uma conta apta, enviaremos um link temporário. Por segurança, a resposta será a mesma para qualquer endereço.': 'Indica el correo electrónico de la cuenta. Si existe una cuenta habilitada, enviaremos un enlace temporal. Por seguridad, la respuesta será la misma para cualquier dirección.',
  'Informe um endereço de e-mail válido.': 'Indica una dirección de correo electrónico válida.', 'Integração e consultoria': 'Integración y consultoría', 'Licenças': 'Licencias',
  'Licenciamento conectado ao seu projeto': 'Licencias vinculadas a tu proyecto', 'Link copiado.': 'Enlace copiado.', 'Link privado': 'Enlace privado',
  'Mantenha seu perfil e senha atualizados.': 'Mantén tu perfil y contraseña actualizados.', 'Mensagens': 'Mensajes', 'Minha conta': 'Mi cuenta',
  'Mínimo de 8 caracteres, com maiúscula, minúscula e número.': 'Al menos 8 caracteres, con una mayúscula, una minúscula y un número.', 'Mínimo de 8 caracteres, maiúscula, minúscula e número.': 'Al menos 8 caracteres, una mayúscula, una minúscula y un número.',
  'Modelo salvo. O envio permanece desativado.': 'Plantilla guardada. El envío sigue desactivado.', 'Modelos de mensagens automáticas': 'Plantillas de mensajes automáticos',
  'Muitas tentativas. Aguarde um minuto.': 'Demasiados intentos. Espera un minuto.', 'Não foi possível concluir. Tente novamente.': 'No se pudo completar. Inténtalo de nuevo.',
  'Não foi possível conectar. Aguarde um momento e tente novamente.': 'No se pudo conectar. Espera un momento e inténtalo de nuevo.',
  'Não inclua senhas, chaves privadas ou dados sensíveis da sua planta.': 'No incluyas contraseñas, claves privadas ni datos sensibles de tu planta.', 'Nenhum chamado por enquanto.': 'Aún no tienes solicitudes.',
  'Nenhum instalador publicado nesta etapa. Não há links provisórios ou arquivos não validados.': 'Todavía no hay instaladores publicados. No ofrecemos enlaces provisionales ni archivos sin validar.', 'Nenhum resultado para esta pesquisa.': 'No hay resultados para esta búsqueda.',
  'Nome': 'Nombre', 'Nome completo': 'Nombre completo', 'Nome de exibição': 'Nombre visible', 'Nova senha': 'Nueva contraseña', 'Novo artigo': 'Nuevo artículo', 'Novo chamado': 'Nueva solicitud',
  'O cadastro e a recuperação automática não estão disponíveis no momento.': 'El registro y la recuperación automática no están disponibles en este momento.',
  'O cadastro público está disponível. Após criar a conta, confirme o endereço pelo link enviado.': 'El registro público está disponible. Después de crear tu cuenta, confirma tu dirección mediante el enlace enviado.',
  'O cadastro público não está disponível no momento. Contas convidadas entram após confirmar o endereço pelo link de ativação.': 'El registro público no está disponible por ahora. Las cuentas invitadas pueden acceder después de confirmar su dirección con el enlace de activación.',
  'O convidado define a própria senha. Compartilhe o link por um canal privado; o envio por e-mail ainda não está habilitado.': 'El invitado define su propia contraseña. Comparte el enlace por un canal privado; el envío por correo electrónico aún no está habilitado.',
  'O convidado define a própria senha. Se informar um e-mail, o convite entra na fila de envio automático.': 'El invitado define su propia contraseña. Si indicas un correo electrónico, la invitación se añadirá a la cola de envío automático.',
  'O envio de confirmação está temporariamente indisponível. Tente mais tarde.': 'El envío de confirmaciones no está disponible temporalmente. Inténtalo más tarde.',
  'O fórum usará a mesma conta do portal. As categorias previstas incluem primeiros passos, residencial e predial, aplicações industriais, scripts Python e boas práticas.': 'El foro utilizará la misma cuenta del portal. Las categorías previstas incluyen primeros pasos, aplicaciones residenciales y para edificios, aplicaciones industriales, scripts de Python y buenas prácticas.',
  'O link de confirmação é inválido.': 'El enlace de confirmación no es válido.', 'O link de convite é inválido.': 'El enlace de invitación no es válido.', 'O link de recuperação é inválido.': 'El enlace de recuperación no es válido.',
  'O link é inválido, já foi usado ou expirou. Solicite um novo e-mail de confirmação.': 'El enlace no es válido, ya se utilizó o ha caducado. Solicita un nuevo correo de confirmación.',
  'O link é inválido, já foi usado ou expirou. Solicite uma nova recuperação de senha.': 'El enlace no es válido, ya se utilizó o ha caducado. Solicita una nueva recuperación de contraseña.',
  'O link é pessoal, de uso único e expira em 24 horas. Escolha uma senha com pelo menos 8 caracteres, incluindo maiúscula, minúscula e número.': 'El enlace es personal, de un solo uso y caduca en 24 horas. Elige una contraseña de al menos 8 caracteres que incluya una mayúscula, una minúscula y un número.',
  'O próximo ciclo prevê catálogo, pagamentos e gestão de licenças. Ainda não há cobrança ou emissão automática por aqui.': 'La próxima etapa contempla el catálogo, los pagos y la gestión de licencias. Todavía no se procesan pagos ni se emiten licencias automáticamente desde aquí.',
  'O registro não contém senhas nem códigos de convite.': 'El registro no contiene contraseñas ni códigos de invitación.',
  'O SCADIVO é gratuito para até 200 TAGs. Licenças por máquina sem conexão permanente continuam previstas; o licenciamento online terá integração própria, sem alterar o produto nesta etapa.': 'SCADIVO es gratuito para hasta 200 TAGs. Se mantienen previstas las licencias por máquina sin conexión permanente; las licencias en línea tendrán una integración propia y no modificarán el producto en esta etapa.',
  'Obrigatório se escolher “Outra área”.': 'Obligatorio si seleccionas «Otra área».', 'Obrigatório. Se não houver empresa ou escola, informe “Autônomo/independente”.': 'Obligatorio. Si no tienes empresa ni centro educativo, indica «Autónomo/independiente».', 'Olá': 'Hola',
  'Organização': 'Organización', 'Organização (opcional)': 'Organización (opcional)', 'Os instaladores Windows, Linux e as instruções Docker serão publicados aqui quando cada distribuição oficial estiver aprovada.': 'Los instaladores para Windows y Linux y las instrucciones de Docker se publicarán aquí cuando se apruebe cada distribución oficial.',
  'Outra área': 'Otra área', 'Pagamentos e licenciamento online não estão ativos.': 'Los pagos y las licencias en línea no están activos.',
  'Para continuar no portal, informe seu perfil, área de atuação e empresa/escola. Os demais campos são opcionais.': 'Para continuar, indica tu perfil, área de actividad y empresa o centro educativo. Los demás campos son opcionales.',
  'Perfil': 'Perfil', 'Perfil atualizado.': 'Perfil actualizado.', 'Pesquisar': 'Buscar', 'Pesquisar no FAQ': 'Buscar en preguntas frecuentes', 'PILOTO': 'PILOTO', 'Portal do cliente': 'Portal de clientes', 'Portal indisponível': 'Portal no disponible',
  'Precisa de apoio? Acesse ': '¿Necesitas ayuda? Visita ', 'Preencha os campos obrigatórios do perfil e confira os dados opcionais.': 'Completa los campos obligatorios del perfil y revisa los datos opcionales.',
  'Prévia no idioma atual': 'Vista previa en el idioma actual', 'Profissional': 'Profesional', 'PRÓXIMA ETAPA': 'PRÓXIMA ETAPA', 'Publicado no FAQ': 'Publicado en preguntas frecuentes',
  'Qual área?': '¿Qué área?', 'Quero receber novidades e informações comerciais por e-mail (opcional).': 'Quiero recibir novedades e información comercial por correo electrónico (opcional).',
  'Recuperar acesso': 'Recuperar acceso', 'Redefinir senha': 'Restablecer contraseña', 'Reenviar confirmação': 'Reenviar confirmación', 'Referência': 'Referencia', 'Repita a nova senha': 'Repite la nueva contraseña', 'Repita a senha': 'Repite la contraseña',
  'Respondido': 'Respondido', 'Sair': 'Cerrar sesión', 'Salvar artigo': 'Guardar artículo', 'Salvar e continuar': 'Guardar y continuar', 'Salvar modelo': 'Guardar plantilla', 'Salvar perfil': 'Guardar perfil',
  'Se houver uma confirmação pendente para esse endereço, enviaremos um novo link.': 'Si hay una confirmación pendiente para esta dirección, enviaremos un nuevo enlace.',
  'Se houver uma conta confirmada para esse endereço, enviaremos as instruções. Confira a caixa de entrada e o spam.': 'Si existe una cuenta confirmada para esta dirección, enviaremos las instrucciones. Revisa tu bandeja de entrada y el correo no deseado.',
  'Se o cadastro foi concluído, confirme o endereço pelo link enviado. Confira também a caixa de spam.': 'Si completaste el registro, confirma tu dirección mediante el enlace enviado. Revisa también el correo no deseado.',
  'Se o endereço estiver válido e disponível, enviaremos o link de confirmação. Confira também o spam.': 'Si la dirección es válida y está disponible, enviaremos un enlace de confirmación. Revisa también el correo no deseado.',
  'Selecione e copie o link acima.': 'Selecciona y copia el enlace anterior.', 'Selecione seu perfil': 'Selecciona tu perfil', 'Selecione sua área': 'Selecciona tu área', 'Senha': 'Contraseña', 'Senha atual': 'Contraseña actual',
  'Senha atualizada.': 'Contraseña actualizada.', 'Senha redefinida. Entre com sua nova senha.': 'Contraseña restablecida. Inicia sesión con tu nueva contraseña.', 'Seu perfil': 'Tu perfil', 'Seu projeto, conectado.': 'Tu proyecto, conectado.',
  'Situação': 'Estado', 'Somente arquivos de distribuição aprovados.': 'Solo archivos de distribución aprobados.', 'Somente você e a equipe podem acessar seus chamados.': 'Solo tú y el equipo pueden acceder a tus solicitudes.',
  'Sua mensagem': 'Tu mensaje', 'Suporte': 'Soporte', 'Suporte privado': 'Soporte privado',
  'Suporte, conhecimento e distribuição oficial do SCADIVO, em um ambiente próprio para clientes e integradores.': 'Soporte, documentación y distribución oficial de SCADIVO en un entorno para clientes e integradores.',
  'Técnico': 'Técnico', 'Telefone (opcional)': 'Teléfono (opcional)', 'Tentar novamente': 'Intentar de nuevo', 'Tipo de mensagem': 'Tipo de mensaje', 'Últimas 100 ações': 'Últimas 100 acciones',
  'Uma comunidade em construção': 'Una comunidad en construcción', 'Uma conta. Mais conexão com seu projeto.': 'Una cuenta. Más conexión con tu proyecto.', 'Use seu e-mail ou nome de usuário.': 'Usa tu correo electrónico o nombre de usuario.',
  'Use um nome de usuário com letras, números, ponto ou hífen.': 'Usa un nombre de usuario con letras, números, punto o guion.', 'Usuário': 'Usuario',
  'Usuário ou senha inválidos. Após várias tentativas, aguarde 15 minutos.': 'El usuario o la contraseña no son válidos. Después de varios intentos, espera 15 minutos.',
  'Variáveis: {{name}}, {{link}}, {{reference}}. Somente texto; nenhum script é executado.': 'Variables: {{name}}, {{link}}, {{reference}}. Solo texto; no se ejecuta ningún script.', 'Visão geral': 'Resumen',
  'Você vê os chamados de todos os clientes como administrador.': 'Como administrador, puedes ver las solicitudes de todos los clientes.', 'Voltar ao login': 'Volver al inicio de sesión'
};
const tr = (pt, en, es = spanish[pt] || spanish[en] || en) => state.lang === 'en' ? en : state.lang === 'es' ? es : pt;
const date = value => new Date(value).toLocaleString(state.lang === 'en' ? 'en-US' : state.lang === 'es' ? 'es-ES' : 'pt-BR');
const labels = () => ({ home: tr('Visão geral', 'Overview'), profile: tr('Minha conta', 'My account'), support: tr('Suporte', 'Support'), downloads: tr('Downloads', 'Downloads', 'Descargas'), faq: 'FAQ', community: tr('Comunidade', 'Community'), licenses: tr('Licenças', 'Licenses'), admin: tr('Administração', 'Administration') });
const localeOptions = () => [['pt-BR', 'Português'], ['en', 'English'], ['es', 'Español']];

function h(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (key.startsWith('on')) node.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key === 'class') node.className = value;
    else if (key === 'text') node.textContent = value;
    else if (key in node && !key.startsWith('aria')) node[key] = value;
    else node.setAttribute(key, value);
  }
  for (const child of [children].flat(Infinity)) {
    if (child == null || typeof child === 'boolean') continue;
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
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
const select = (name, entries, value, extra = {}) => h('select', { name, value, ...extra }, entries.map(([id, text]) => h('option', { value: id, selected: id === value }, text)));
const values = form => Object.fromEntries([...form.elements].filter(control => control.name).map(control => [control.name, control.type === 'checkbox' ? control.checked : control.value === '' ? null : control.value]));
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
    'profile-fields-required': tr('Preencha os campos obrigatórios do perfil e confira os dados opcionais.', 'Complete the required profile fields and check the optional information.'),
    'profile-incomplete': tr('Complete seu perfil antes de navegar pelo portal.', 'Complete your profile before using the portal.'),
    'password-requirements': tr('A senha precisa ter ao menos 8 caracteres, incluindo maiúscula, minúscula e número.', 'The password needs at least 8 characters, including uppercase, lowercase and a number.'),
    'email-not-configured': tr('O envio de confirmação está temporariamente indisponível. Tente mais tarde.', 'Email confirmation is temporarily unavailable. Please try again later.'),
    'invalid-email': tr('Informe um endereço de e-mail válido.', 'Enter a valid email address.'),
    'username-requirements': tr('Use um nome de usuário disponível com 3 a 20 caracteres, sem espaços ou símbolos repetidos.', 'Choose an available username from 3 to 20 characters, without spaces or repeated symbols.', 'Elige un nombre de usuario disponible de 3 a 20 caracteres, sin espacios ni símbolos repetidos.'),
    'invalid-confirmation': tr('O link é inválido, já foi usado ou expirou. Solicite um novo e-mail de confirmação.', 'This link is invalid, already used or expired. Request a new confirmation email.'),
    'invalid-password-reset': tr('O link é inválido, já foi usado ou expirou. Solicite uma nova recuperação de senha.', 'This link is invalid, already used or expired. Request a new password reset.'),
    'invalid-request-token': tr('A sessão mudou. Atualize a página e tente novamente.', 'The session changed. Refresh this page and try again.'),
    'account-already-exists': tr('Esse usuário ou e-mail já está cadastrado.', 'This username or email is already registered.'),
    'account-not-created': tr('Use um nome de usuário com letras, números, ponto ou hífen.', 'Use a username with letters, numbers, a dot or a hyphen.'),
    'owner-account-protected': tr('Contas proprietárias não podem ser suspensas por este controle.', 'Owner accounts cannot be suspended with this control.', 'Las cuentas propietarias no se pueden suspender con este control.'),
    'account-not-updated': tr('A conta mudou durante a operação. Atualize a página e tente novamente.', 'The account changed during this operation. Refresh the page and try again.', 'La cuenta cambió durante la operación. Actualiza la página e inténtalo de nuevo.'),
    'password-not-changed': tr('Confira a senha atual. A nova senha precisa ter ao menos 8 caracteres, maiúscula, minúscula e número.', 'Check your current password. The new password needs at least 8 characters, uppercase, lowercase and a number.'),
    'invalid-invitation': tr('Convite inválido ou já utilizado.', 'Invalid or already used invitation.'),
    'invalid-invitation-or-password': tr('Convite expirado ou senha fora dos requisitos.', 'The invitation expired or the password does not meet the requirements.'),
    'slug-already-exists': tr('Esse endereço de artigo já existe.', 'This article slug already exists.'),
    ...portalLicensing.errors()
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
async function session() {
  state.session = await api('/session');
  const accountLocale = state.session?.authenticated ? state.session.user?.locale : null;
  if (['pt-BR', 'en', 'es'].includes(accountLocale)) {
    state.lang = accountLocale;
    try { localStorage.setItem('scadivo.portal.lang', state.lang); } catch { /* Account locale remains available for this session. */ }
  }
}
function validateCommunitySso(parsed) {
  if (typeof parsed.sso !== 'string' || parsed.sso.length < 1 || parsed.sso.length > 8192 ||
      typeof parsed.sig !== 'string' || !/^[a-f\d]{64}$/i.test(parsed.sig)) throw new Error('Invalid community SSO request');
  return { sso: parsed.sso, sig: parsed.sig };
}
function parseCommunitySso(encoded) {
  const base64 = encoded.replaceAll('-', '+').replaceAll('_', '/') + '='.repeat((4 - encoded.length % 4) % 4);
  return validateCommunitySso(JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(base64), value => value.charCodeAt(0)))));
}
function continueCommunitySso() {
  const user = state.session?.user;
  if (!state.communitySso || !user || (!user.owner && !user.profileComplete)) return false;
  const query = new URLSearchParams(state.communitySso);
  state.communitySso = null;
  try { sessionStorage.removeItem(communitySsoStorageKey); } catch { /* The request remains in memory for this navigation. */ }
  location.assign(`/portal/api/community/sso?${query.toString()}`);
  return true;
}
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
  const languageSelect = document.querySelector('#language-select');
  languageSelect.value = state.lang;
  languageSelect.setAttribute('aria-label', state.lang === 'pt-BR' ? 'Idioma da interface' : state.lang === 'en' ? 'Interface language' : 'Idioma de la interfaz');
  languageSelect.onchange = () => {
    if (!['pt-BR', 'en', 'es'].includes(languageSelect.value)) return;
    state.lang = languageSelect.value;
    try { localStorage.setItem('scadivo.portal.lang', state.lang); } catch { /* The selected language remains active for this session. */ }
    render();
  };
  const actions = document.querySelector('#account-actions');
  actions.replaceChildren();
  if (state.session?.authenticated) actions.append(h('span', { class: 'account-chip' }, state.session.user.displayName), button(tr('Sair', 'Sign out'), async () => {
    try { await api('/auth/logout', 'POST', {}); await session(); state.tab = 'home'; render(); } catch (error) { feedback(error.message, true); }
  }));
}
function login() {
  const intro = h('section', { class: 'login-intro' }, [badge(tr('ÁREA DO CLIENTE · PILOTO', 'CUSTOMER PORTAL · PILOT')), h('h1', {}, tr('Uma conta. Mais conexão com seu projeto.', 'One account. Closer to your project.')), p(tr('Suporte, conhecimento e distribuição oficial do SCADIVO, em um ambiente próprio para clientes e integradores.', 'SCADIVO support, knowledge and official distribution, in a dedicated environment for customers and integrators.')), h('div', { class: 'intro-features' }, [panel(tr('Suporte privado', 'Private support'), p(tr('Converse com a equipe e acompanhe seus chamados.', 'Talk to the team and track your requests.'))), panel(tr('Conhecimento e downloads', 'Knowledge and downloads'), p(tr('FAQ pesquisável e catálogo de versões oficiais.', 'Searchable FAQ and official release catalog.')))]), notice(tr('Este portal tem contas próprias. O acesso à engenharia da demonstração é independente.', 'This portal has its own accounts. Demo engineering access is separate.'))]);
  const card = panel(tr('Entre no portal', 'Sign in to the portal'), p(tr('Use seu e-mail ou nome de usuário.', 'Use your email or username.')), form([
    field(tr('E-mail ou usuário', 'Email or username'), input('username', 'text', state.pendingEmail, { autocomplete: 'username', maxLength: 256 })),
    field(tr('Senha', 'Password'), input('password', 'password', '', { autocomplete: 'current-password', maxLength: 1024 }))
  ], tr('Entrar', 'Sign in'), async data => { await api('/auth/login', 'POST', data); await session(); state.pendingEmail = ''; if (continueCommunitySso()) return; render(); }));
  const registrationEnabled = state.session?.publicRegistrationEnabled === true;
  const recoveryEnabled = state.session?.emailDeliveryConfigured === true;
  const note = h('div', { class: 'login-note' }, [
    p(state.pendingEmail
      ? tr('Se o cadastro foi concluído, confirme o endereço pelo link enviado. Confira também a caixa de spam.', 'If registration was completed, confirm your address using the link we sent. Check spam as well.')
      : registrationEnabled
        ? tr('O cadastro público está disponível. Após criar a conta, confirme o endereço pelo link enviado.', 'Public registration is available. After creating your account, confirm your address using the link sent by email.')
        : tr('O cadastro público não está disponível no momento. Contas convidadas entram após confirmar o endereço pelo link de ativação.', 'Public registration is not available right now. Invited accounts can sign in after confirming their address using the activation link.')),
    state.pendingEmail && button(tr('Reenviar confirmação', 'Resend confirmation'), async () => {
      try { await api('/auth/confirmation/resend', 'POST', { email: state.pendingEmail }); feedback(tr('Se houver uma confirmação pendente para esse endereço, enviaremos um novo link.', 'If confirmation is pending for that address, a new link will be sent.')); }
      catch (error) { feedback(error.message, true); }
    }),
    recoveryEnabled && button(tr('Esqueceu a senha?', 'Forgot your password?'), () => { state.recover = true; history.replaceState(null, '', '/portal/#forgot-password'); render(); }),
    registrationEnabled && button(tr('Criar conta', 'Create an account'), () => { state.register = true; history.replaceState(null, '', '/portal/#register'); render(); }, 'button primary'),
    p([tr('Precisa de apoio? Acesse ', 'Need help? Visit ', '¿Necesitas ayuda? Visita '), h('a', { href: state.lang === 'en' ? 'https://scadivo.com/contact?area=support' : state.lang === 'es' ? 'https://scadivo.com/contact?area=support&lang=es' : 'https://scadivo.com/contato?area=suporte' }, tr('a página de suporte SCADIVO', 'SCADIVO support', 'el soporte de SCADIVO')), '.']),
    !registrationEnabled && !recoveryEnabled && p(tr('O cadastro e a recuperação automática não estão disponíveis no momento.', 'Registration and automatic recovery are not available right now.'))
  ]);
  card.append(note); card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'login-layout' }, [intro, card]));
}

function passwordRecovery() {
  const card = panel(tr('Recuperar acesso', 'Recover account access'),
    p(tr('Informe o e-mail da conta. Se houver uma conta apta, enviaremos um link temporário. Por segurança, a resposta será a mesma para qualquer endereço.', 'Enter the account email. If an eligible account exists, we will send a temporary link. For security, the response is the same for every address.')),
    form([field(tr('E-mail', 'Email'), input('email', 'email', state.pendingEmail, { autocomplete: 'email', maxLength: 256 }))], tr('Enviar link de recuperação', 'Send recovery link'), async data => {
      await api('/auth/password-recovery', 'POST', data);
      state.recover = false; history.replaceState(null, '', '/portal/'); render();
      feedback(tr('Se houver uma conta confirmada para esse endereço, enviaremos as instruções. Confira a caixa de entrada e o spam.', 'If a confirmed account exists for this address, we will send instructions. Check your inbox and spam folder.'));
    }),
    button(tr('Voltar ao login', 'Back to sign in'), () => { state.recover = false; history.replaceState(null, '', '/portal/'); render(); })
  );
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'invite-layout' }, card));
}

function passwordReset() {
  const card = panel(tr('Definir nova senha', 'Set a new password'),
    p(tr('O link é pessoal, de uso único e expira em 24 horas. Escolha uma senha com pelo menos 8 caracteres, incluindo maiúscula, minúscula e número.', 'This personal, single-use link expires in 24 hours. Choose a password with at least 8 characters, including uppercase, lowercase and a number.')),
    form([
      field(tr('Nova senha', 'New password'), input('password', 'password', '', { minLength: 8, maxLength: 1024, autocomplete: 'new-password' })),
      field(tr('Repita a nova senha', 'Repeat new password'), input('confirmation', 'password', '', { minLength: 8, maxLength: 1024, autocomplete: 'new-password' }))
    ], tr('Redefinir senha', 'Reset password'), async data => {
      if (data.password !== data.confirmation) throw new Error(tr('As senhas não coincidem.', 'The passwords do not match.'));
      await api('/auth/reset-password', 'POST', { ...state.resetPassword, password: data.password });
      state.resetPassword = null; history.replaceState(null, '', '/portal/'); render();
      feedback(tr('Senha redefinida. Entre com sua nova senha.', 'Password reset. Sign in with your new password.'));
    }),
    button(tr('Voltar ao login', 'Back to sign in'), () => { state.resetPassword = null; history.replaceState(null, '', '/portal/'); render(); })
  );
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'invite-layout' }, card));
}

const profileTypeOptions = () => [['', tr('Selecione seu perfil', 'Select your profile')], ['student', tr('Estudante', 'Student')], ['professional', tr('Profissional', 'Professional')], ['organization', tr('Organização', 'Organization')]];
const activityAreaOptions = () => [
  ['', tr('Selecione sua área', 'Select your area')],
  ['industrial-automation', tr('Automação industrial e processos', 'Industrial automation and process')],
  ['buildings-facilities', tr('Edificações e instalações', 'Buildings and facilities')],
  ['residential', tr('Aplicações residenciais', 'Residential applications')],
  ['utilities', tr('Água, saneamento e energia', 'Water, sanitation and energy utilities')],
  ['integrator-consulting', tr('Integração e consultoria', 'Systems integration and consulting')],
  ['education-research', tr('Ensino e pesquisa', 'Education and research')],
  ['other', tr('Outra área', 'Other')]
];
function profileFields(user = {}) {
  const profileType = select('profileType', profileTypeOptions(), user.profileType || '', { required: true });
  const activityArea = select('activityArea', activityAreaOptions(), user.activityArea || '', { required: true });
  const otherInput = input('activityAreaOther', 'text', user.activityAreaOther || '', { maxLength: 120, required: user.activityArea === 'other' });
  const otherField = field(tr('Qual área?', 'Which area?'), otherInput, tr('Obrigatório se escolher “Outra área”.', 'Required when “Other” is selected.'));
  const updateOther = () => { otherField.hidden = activityArea.value !== 'other'; otherInput.required = activityArea.value === 'other'; };
  activityArea.addEventListener('change', updateOther); updateOther();
  const marketing = input('marketingEmailOptIn', 'checkbox', 'yes', { checked: user.marketingEmailOptIn === true, required: false });
  return [
    h('div', { class: 'form-row' }, [field(tr('Perfil', 'Profile'), profileType), field(tr('Área de atuação', 'Activity area'), activityArea)]),
    otherField,
    field(tr('Empresa, escola ou vínculo (obrigatório)', 'Company, school or affiliation (required)'), input('organizationOrSchool', 'text', user.organizationOrSchool || '', { maxLength: 200, placeholder: tr('Autônomo/independente', 'Independent') }), tr('Obrigatório. Se não houver empresa ou escola, informe “Autônomo/independente”.', 'Required. If you have no company or school, enter “Independent”.')),
    h('div', { class: 'form-row' }, [field(tr('Telefone (opcional)', 'Phone (optional)'), input('phoneNumber', 'tel', user.phoneNumber || '', { required: false, maxLength: 40, autocomplete: 'tel' })), field(tr('Data de nascimento (opcional)', 'Date of birth (optional)'), input('dateOfBirth', 'date', user.dateOfBirth || '', { required: false }))]),
    field(tr('Endereço (opcional)', 'Address (optional)'), input('address', 'text', user.address || '', { required: false, maxLength: 300, autocomplete: 'street-address' })),
    h('label', { class: 'check-field' }, [marketing, tr('Quero receber novidades e informações comerciais por e-mail (opcional).', 'I would like to receive product news and commercial updates by email (optional).')])
  ];
}
function registration() {
  const card = panel(tr('Criar conta SCADIVO', 'Create a SCADIVO account'),
    p(tr('Depois do cadastro, enviaremos um link único para confirmar seu e-mail. O login será liberado somente após a confirmação.', 'After signup, we will send a unique link to confirm your email. Sign-in is enabled only after confirmation.')),
    form([
      field(tr('Nome de apresentação', 'Display name', 'Nombre visible'), input('displayName', 'text', '', { maxLength: 160, autocomplete: 'nickname' }), tr('Esse nome aparecerá na comunidade. Escolha como quer ser identificado.', 'This name will appear in the community. Choose how you want to be identified.', 'Este nombre aparecerá en la comunidad. Elige cómo quieres que te identifiquen.')),
      field(tr('Nome de usuário', 'Username', 'Nombre de usuario'), input('username', 'text', '', { minLength: 3, maxLength: 20, pattern: '[A-Za-z0-9_][A-Za-z0-9._-]{1,18}[A-Za-z0-9]', autocomplete: 'username' }), tr('Precisa estar disponível e ter de 3 a 20 caracteres. Você poderá entrar com ele ou com seu e-mail.', 'Must be available and 3 to 20 characters long. You can sign in with it or with your email.', 'Debe estar disponible y tener de 3 a 20 caracteres. Puedes iniciar sesión con este nombre o con tu correo.')),
      field(tr('E-mail', 'Email'), input('email', 'email', '', { maxLength: 256, autocomplete: 'email' })),
      field(tr('Idioma preferido', 'Preferred language'), select('locale', localeOptions(), state.lang, { required: true })),
      field(tr('Senha', 'Password'), input('password', 'password', '', { minLength: 8, maxLength: 1024, autocomplete: 'new-password' }), tr('Mínimo de 8 caracteres, com maiúscula, minúscula e número.', 'At least 8 characters, with uppercase, lowercase and a number.')),
      field(tr('Repita a senha', 'Repeat password'), input('confirmation', 'password', '', { minLength: 8, maxLength: 1024, autocomplete: 'new-password' })),
      ...profileFields()
    ], tr('Cadastrar e enviar confirmação', 'Sign up and send confirmation'), async data => {
      if (data.password !== data.confirmation) throw new Error(tr('As senhas não coincidem.', 'The passwords do not match.'));
      const { confirmation, ...registrationData } = data;
      await api('/auth/register', 'POST', registrationData);
      state.pendingEmail = data.email; state.register = false;
      history.replaceState(null, '', '/portal/'); render();
      feedback(tr('Se o endereço estiver válido e disponível, enviaremos o link de confirmação. Confira também o spam.', 'If the address is valid and available, we will send a confirmation link. Check spam as well.'));
    }),
    button(tr('Voltar ao login', 'Back to sign in'), () => { state.register = false; history.replaceState(null, '', '/portal/'); render(); })
  );
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'invite-layout' }, card));
}
function emailConfirmation() {
  const token = state.confirmEmail;
  const card = panel(tr('Confirme seu e-mail', 'Confirm your email'),
    p(tr('Este link é pessoal, de uso único e expira em 24 horas. Pressione o botão para concluir; apenas abrir o link não ativa a conta.', 'This link is personal, single-use and expires in 24 hours. Press the button to continue; opening the link alone does not activate the account.')),
    form([], tr('Confirmar e-mail', 'Confirm email'), async () => {
      await api('/auth/confirm-email', 'POST', token);
      state.confirmEmail = null; state.pendingEmail = ''; history.replaceState(null, '', '/portal/'); render();
      feedback(tr('E-mail confirmado. Agora você pode entrar com seu nome de usuário ou e-mail e senha.', 'Email confirmed. You can now sign in with your username or email and password.', 'Correo confirmado. Ahora puedes iniciar sesión con tu nombre de usuario o correo y contraseña.'));
    }),
    button(tr('Voltar ao login', 'Back to sign in'), () => { state.confirmEmail = null; history.replaceState(null, '', '/portal/'); render(); })
  );
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'invite-layout' }, card));
}
function completeProfile() {
  const user = state.session.user;
  const accountUsername = user.userName?.toLowerCase() === user.email?.toLowerCase() ? '' : user.userName;
  const card = panel(tr('Complete seu perfil', 'Complete your profile'),
    p(tr('Para continuar no portal, informe seu perfil, área de atuação e empresa/escola. Os demais campos são opcionais.', 'To continue, tell us your profile, activity area and company/school. The remaining fields are optional.')),
    form([
      field(tr('Nome de apresentação', 'Display name', 'Nombre visible'), input('displayName', 'text', user.displayName, { maxLength: 160, autocomplete: 'nickname' }), tr('Esse nome aparecerá na comunidade. Escolha como quer ser identificado.', 'This name will appear in the community. Choose how you want to be identified.', 'Este nombre aparecerá en la comunidad. Elige cómo quieres que te identifiquen.')),
      field(tr('Nome de usuário', 'Username', 'Nombre de usuario'), input('username', 'text', accountUsername || '', { minLength: 3, maxLength: 20, pattern: '[A-Za-z0-9_][A-Za-z0-9._-]{1,18}[A-Za-z0-9]', autocomplete: 'username' }), tr('Precisa estar disponível e ter de 3 a 20 caracteres. Você poderá entrar com ele ou com seu e-mail.', 'Must be available and 3 to 20 characters long. You can sign in with it or with your email.', 'Debe estar disponible y tener de 3 a 20 caracteres. Puedes iniciar sesión con este nombre o con tu correo.')),
      field(tr('Idioma preferido', 'Preferred language'), select('locale', localeOptions(), user.locale, { required: true })),
      ...profileFields(user)
    ], tr('Salvar e continuar', 'Save and continue'), async data => {
      await api('/profile', 'PUT', data); await session(); state.tab = 'home'; if (continueCommunitySso()) return; render();
    })
  );
  card.classList.add('login-card'); app.replaceChildren(h('div', { class: 'invite-layout' }, card));
}
function invitation() {
  app.replaceChildren(h('div', { class: 'invite-layout' }, panel(tr('Bem-vindo ao SCADIVO', 'Welcome to SCADIVO'), p(tr('Defina a senha da sua conta convidada. O link é pessoal e válido por 24 horas.', 'Set the password for your invited account. The personal link is valid for 24 hours.')), form([
    field(tr('Idioma preferido', 'Preferred language'), select('locale', localeOptions(), state.lang, { required: true })),
    field(tr('Nova senha', 'New password'), input('password', 'password', '', { autocomplete: 'new-password', minLength: 8, maxLength: 1024 }), tr('Mínimo de 8 caracteres, maiúscula, minúscula e número.', 'At least 8 characters, uppercase, lowercase and a number.')),
    field(tr('Repita a senha', 'Repeat password'), input('confirmation', 'password', '', { autocomplete: 'new-password', minLength: 8 })),
    ...profileFields()
  ], tr('Ativar minha conta', 'Activate my account'), async data => {
    if (data.password !== data.confirmation) throw new Error(tr('As senhas não coincidem.', 'The passwords do not match.'));
    const { confirmation, ...profileData } = data;
    await api('/auth/accept-invitation', 'POST', { ...state.invite, ...profileData }); state.invite = null;
    history.replaceState(null, '', '/portal/'); await session(); render(); feedback(tr('Conta ativada. Entre com seu usuário e senha.', 'Account activated. Sign in with your username and password.'));
  }))));
}
async function render() {
  const version = ++state.version; renderHeader();
  if (state.invite) return invitation();
  if (state.resetPassword) return passwordReset();
  if (state.confirmEmail) return emailConfirmation();
  if (state.recover && state.session?.emailDeliveryConfigured) return passwordRecovery();
  if (state.register && state.session?.publicRegistrationEnabled) return registration();
  if (!state.session?.authenticated) return login();
  if (!state.session.user.owner && !state.session.user.profileComplete) return completeProfile();
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
  return h('div', {}, [panel(`${tr('Olá', 'Hello')}, ${state.session.user.displayName}.`, p(tr('Este é seu ponto de encontro com o SCADIVO. Comece pelo conhecimento, abra uma conversa privada ou acompanhe as versões disponibilizadas pela equipe.', 'This is your SCADIVO hub. Explore the knowledge base, start a private conversation or follow releases made available by the team.'))), h('div', { class: 'content-grid' }, ['faq', 'support', 'downloads', 'profile'].map(id => panel(labels()[id], p(({ faq: tr('Encontre respostas sobre implantação e uso.', 'Find answers about deployment and use.'), support: tr('Acompanhe suas conversas com a equipe.', 'Follow your conversations with the team.'), downloads: tr('Somente arquivos de distribuição aprovados.', 'Approved distribution files only.'), profile: tr('Mantenha seu perfil e senha atualizados.', 'Keep your profile and password up to date.') })[id]), button(tr('Acessar →', 'Open →'), () => navigate(id))))), notice(tr('O SCADIVO é gratuito para até 200 TAGs. Organize suas aplicações e consulte os direitos de uso na aba Licenças.', 'SCADIVO is free for up to 200 tags. Organize your applications and view entitlements in the Licenses tab.', 'SCADIVO es gratuito hasta 200 TAGs. Organiza tus aplicaciones y consulta los derechos de uso en la pestaña Licencias.'))]);
}
function profile() {
  const user = state.session.user;
  const accountUsername = user.userName?.toLowerCase() === user.email?.toLowerCase() ? '' : user.userName;
  return h('div', { class: 'content-grid' }, [panel(tr('Seu perfil', 'Your profile'), p(`${tr('Usuário', 'Username')}: ${user.userName}`), p(`${tr('E-mail', 'Email')}: ${user.email || tr('Ainda não informado', 'Not provided yet')}`), form([
    field(tr('Nome de apresentação', 'Display name', 'Nombre visible'), input('displayName', 'text', user.displayName, { maxLength: 160, autocomplete: 'nickname' }), tr('Esse nome aparecerá na comunidade. Escolha como quer ser identificado.', 'This name will appear in the community. Choose how you want to be identified.', 'Este nombre aparecerá en la comunidad. Elige cómo quieres que te identifiquen.')),
    field(tr('Nome de usuário', 'Username', 'Nombre de usuario'), input('username', 'text', accountUsername || '', { minLength: 3, maxLength: 20, pattern: '[A-Za-z0-9_][A-Za-z0-9._-]{1,18}[A-Za-z0-9]', autocomplete: 'username' }), tr('Precisa estar disponível e ter de 3 a 20 caracteres. Você poderá entrar com ele ou com seu e-mail.', 'Must be available and 3 to 20 characters long. You can sign in with it or with your email.', 'Debe estar disponible y tener de 3 a 20 caracteres. Puedes iniciar sesión con este nombre o con tu correo.')),
    field(tr('Idioma preferido', 'Preferred language'), select('locale', localeOptions(), user.locale, { required: true })),
    ...profileFields(user)
  ], tr('Salvar perfil', 'Save profile'), async data => { await api('/profile', 'PUT', data); await session(); feedback(tr('Perfil atualizado.', 'Profile updated.')); render(); }), h('div', { class: 'organization-list' }, user.organizations.map(org => p(`${org.name} · ${org.role}`)))), panel(tr('Alterar senha', 'Change password'), form([
    field(tr('Senha atual', 'Current password'), input('currentPassword', 'password', '', { autocomplete: 'current-password' })),
    field(tr('Nova senha', 'New password'), input('newPassword', 'password', '', { autocomplete: 'new-password', minLength: 8 }), tr('Mínimo de 8 caracteres, maiúscula, minúscula e número.', 'At least 8 characters, uppercase, lowercase and a number.')),
    field(tr('Repita a nova senha', 'Repeat new password'), input('confirmation', 'password', '', { autocomplete: 'new-password', minLength: 8 }))
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
  return panel(tr('Distribuição oficial', 'Official distribution'), p(tr('Os instaladores Windows, Linux e as instruções Docker serão publicados aqui quando cada distribuição oficial estiver aprovada.', 'Windows and Linux installers and Docker instructions will appear here when each official distribution is approved.')), releases.length ? h('div', { class: 'content-grid' }, releases.map(release => panel(`${release.platform} · ${release.version}`, p(state.lang === 'en' ? release.notesEn : state.lang === 'es' ? release.notesEs : release.notesPt), h('code', { class: 'checksum' }, `SHA-256 ${release.sha256}`), h('a', { class: 'button primary', href: `/portal/api/downloads/${release.id}/file` }, 'Download ↗')))) : notice(tr('Nenhum instalador publicado nesta etapa. Não há links provisórios ou arquivos não validados.', 'No installer is published at this stage. There are no placeholder links or unverified files.')));
}
async function faq() {
  const result = panel(tr('Como podemos ajudar?', 'How can we help?'));
  const list = h('div', { class: 'faq-list' });
  async function search(query = '') { const articles = await api(`/faq?lang=${state.lang}&q=${encodeURIComponent(query)}`); list.replaceChildren(...articles.map(article => h('details', {}, [h('summary', {}, article.title), h('p', { class: 'preserve-lines' }, article.body)]))); if (!articles.length) list.append(p(tr('Nenhum resultado para esta pesquisa.', 'No results for this search.'))); }
  result.append(form([field(tr('Pesquisar no FAQ', 'Search FAQ'), input('q', 'search', '', { required: false, maxLength: 120, placeholder: tr('Ex.: Docker, TAGs, demonstração', 'E.g. Docker, tags, demo') }))], tr('Pesquisar', 'Search'), data => search(data.q)), list);
  await search(); return result;
}
function community() {
  if (state.session.communityEnabled && state.session.communityStartUrl) return panel(
    tr('Comunidade SCADIVO', 'SCADIVO Community', 'Comunidad SCADIVO'),
    p(tr('Entre na comunidade usando a mesma conta confirmada do portal.', 'Sign in to the community with your confirmed portal account.', 'Accede a la comunidad con la misma cuenta confirmada del portal.')),
    p(tr('Ao entrar, a Comunidade recebe seu e-mail confirmado, nome de usuário, nome de apresentação e idioma preferido. Sua senha, empresa/escola, endereço e telefone não são compartilhados.', 'When you sign in, the Community receives your confirmed email, username, display name, and preferred language. Your password, company/school, address, and phone number are not shared.', 'Al iniciar sesión, la Comunidad recibe tu correo confirmado, nombre de usuario, nombre para mostrar e idioma preferido. No se comparten tu contraseña, empresa/centro educativo, dirección ni teléfono.')),
    h('a', { class: 'button primary', href: state.session.communityStartUrl }, tr('Entrar na comunidade →', 'Enter the community →', 'Entrar en la comunidad →'))
  );
  return panel(tr('Uma comunidade em construção', 'A community in the making', 'Una comunidad en construcción'), badge(tr('PRÓXIMA ETAPA', 'UPCOMING', 'PRÓXIMA ETAPA')), p(tr('O fórum usará a mesma conta do portal. As categorias previstas incluem primeiros passos, residencial e predial, aplicações industriais, scripts Python e boas práticas.', 'The forum will use the same portal account. Planned categories include getting started, homes and buildings, industrial applications, Python scripts and best practices.', 'El foro utilizará la misma cuenta del portal. Las categorías previstas incluyen primeros pasos, aplicaciones residenciales y para edificios, aplicaciones industriales, scripts de Python y buenas prácticas.')), notice(tr('A publicação de tópicos será liberada após a configuração das regras, moderação e recuperação de conta.', 'Topic publishing will open after rules, moderation and account recovery are configured.', 'La publicación de temas se habilitará cuando estén configuradas las reglas, la moderación y la recuperación de cuentas.')));
}
function licenses() { return portalLicensing.customer(); }
async function admin() {
  const overview = await api('/admin/overview');
  const licensingStatus = await api('/licensing/status').catch(() => null);
  const licensingNotice = licensingStatus === null
    ? tr('Não foi possível consultar a disponibilidade do licenciamento online.', 'Online licensing availability could not be checked.', 'No se pudo consultar la disponibilidad de las licencias en línea.')
    : licensingStatus.onlineActivationAvailable
      ? tr('Ativação e renovação online habilitadas.', 'Online activation and renewal enabled.', 'Activación y renovación en línea habilitadas.')
      : tr('Ativação e renovação online desativadas.', 'Online activation and renewal disabled.', 'Activación y renovación en línea deshabilitadas.');
  const metrics = h('div', { class: 'metrics' }, [[tr('Contas', 'Accounts'), overview.accounts], [tr('Chamados abertos', 'Open requests'), overview.openTickets], [tr('Aguardando ativação', 'Awaiting activation'), overview.pendingInvitations]].map(([label, value]) => h('div', { class: 'metric' }, [h('strong', {}, value), h('span', {}, label)])));
  const body = h('div');
  const items = { accounts: tr('Contas e convites', 'Accounts and invitations'), licensing: tr('Licenças', 'Licenses', 'Licencias'), templates: tr('Mensagens', 'Messages'), articles: 'FAQ', audit: tr('Auditoria', 'Audit') };
  const tabs = h('nav', { class: 'admin-subnav', 'aria-label': tr('Administração', 'Administration') }, Object.entries(items).map(([key, label]) => button(label, () => { state.adminTab = key; render(); }, `button ${state.adminTab === key ? 'primary' : 'secondary'}`)));
  const node = await ({ accounts: accountsAdmin, licensing: portalLicensing.admin, templates: templatesAdmin, articles: faqAdmin, audit: auditAdmin })[state.adminTab](); body.append(node);
  const emailStatus = overview.emailDeliveryConfigured
    ? tr(`Envio de e-mail habilitado: ${overview.queuedMessages} na fila, ${overview.sentMessages} aceitas pelo servidor SMTP e ${overview.failedMessages} com falha.`, `Email delivery enabled: ${overview.queuedMessages} queued, ${overview.sentMessages} accepted by the SMTP server and ${overview.failedMessages} failed.`, `Envío de correo electrónico habilitado: ${overview.queuedMessages} en cola, ${overview.sentMessages} aceptados por el servidor SMTP y ${overview.failedMessages} con error.`)
    : tr(`E-mail ainda sem transporte configurado. ${overview.recordedMessages} notificações registradas, sem envio automático.`, `Email delivery is not configured. ${overview.recordedMessages} notifications recorded without automatic delivery.`, `El envío de correo electrónico aún no está configurado. Hay ${overview.recordedMessages} notificaciones registradas sin envío automático.`);
  const registrationStatus = overview.publicRegistrationEnabled
    ? tr('Cadastro público habilitado.', 'Public registration enabled.', 'Registro público habilitado.')
    : tr('Cadastro público desativado.', 'Public registration is disabled.');
  return h('div', {}, [metrics, notice(emailStatus + ' ' + registrationStatus + ' ' + licensingNotice + ' ' + tr('Pagamentos automáticos ainda não estão disponíveis.', 'Automatic payments are not available yet.', 'Los pagos automáticos aún no están disponibles.')), tabs, body]);
}
function accountAccessAction(account) {
  if (account.owner) return badge(tr('Proprietário', 'Owner', 'Propietario'));
  const action = button(account.isSuspended ? tr('Reativar', 'Reactivate', 'Reactivar') : tr('Suspender', 'Suspend', 'Suspender'), async () => {
    const message = account.isSuspended
      ? tr('Reativar esta conta? Será necessário entrar novamente.', 'Reactivate this account? A new sign-in will be required.', '¿Reactivar esta cuenta? Será necesario iniciar sesión de nuevo.')
      : tr('Suspender esta conta? As sessões do portal serão encerradas e novas entradas no fórum serão impedidas.', 'Suspend this account? Portal sessions will end and new forum sign-ins will be blocked.', '¿Suspender esta cuenta? Se cerrarán las sesiones del portal y se impedirán nuevos accesos al foro.');
    if (!window.confirm(`${account.userName}\n\n${message}`)) return;
    action.disabled = true;
    try {
      await api(`/admin/accounts/${encodeURIComponent(account.id)}/access`, 'PUT', { suspended: !account.isSuspended });
      feedback(tr('Acesso da conta atualizado.', 'Account access updated.', 'Acceso de la cuenta actualizado.'));
      await render();
    } catch (error) { feedback(error.message, true); }
    finally { action.disabled = false; }
  });
  return action;
}
async function accountsAdmin() {
  const accounts = await api('/admin/accounts');
  const result = h('div'); const invitationResult = h('div');
  result.append(panel(tr('Criar convite', 'Create invitation'), p(state.session.emailDeliveryConfigured
    ? tr('O convidado define a própria senha. Se informar um e-mail, o convite entra na fila de envio automático.', 'The invitee sets their own password. When you enter an email address, the invitation is queued for automatic delivery.')
    : tr('O convidado define a própria senha. Compartilhe o link por um canal privado; o envio por e-mail ainda não está habilitado.', 'The invitee sets their own password. Share the link privately; email delivery is not enabled yet.')), form([
    h('div', { class: 'form-row' }, [field(tr('Usuário', 'Username'), input('username', 'text', '', { minLength: 3, maxLength: 20, pattern: '[A-Za-z0-9_][A-Za-z0-9._-]{1,18}[A-Za-z0-9]', autocomplete: 'off' })), field(tr('Nome', 'Name'), input('displayName', 'text', '', { maxLength: 160 }))]),
    h('div', { class: 'form-row' }, [field(tr('E-mail (opcional)', 'Email (optional)'), input('email', 'email', '', { required: false, maxLength: 256 })), field(tr('Organização (opcional)', 'Organization (optional)'), input('organization', 'text', '', { required: false, maxLength: 200 }))])
  ], tr('Gerar convite', 'Generate invitation'), async (data, node) => {
    const result = await api('/admin/invitations', 'POST', data); node.reset();
    invitationResult.replaceChildren(notice(tr(`Convite de ${result.userName}: válido por 24 horas e uso único. Guarde o nome de usuário junto com o link.`, `Invitation for ${result.userName}: valid for 24 hours and one use. Keep the username with the link.`, `Invitación para ${result.userName}: válida durante 24 horas y de un solo uso. Guarda el nombre de usuario junto con el enlace.`)), field(tr('Link privado', 'Private link'), textarea('invitationLink', result.invitationUrl, { readOnly: true, rows: 3, class: 'private-link' })), button(tr('Copiar link', 'Copy link'), async () => { try { await navigator.clipboard.writeText(result.invitationUrl); feedback(tr('Link copiado.', 'Link copied.')); } catch { feedback(tr('Selecione e copie o link acima.', 'Select and copy the link above.')); } }));
    feedback(result.emailQueued ? tr('Convite criado e colocado na fila de e-mail.', 'Invitation created and queued for email delivery.') : tr('Convite criado. Compartilhe o link privado.', 'Invitation created. Share the private link.'));
  }), invitationResult), panel(tr('Contas do portal', 'Portal accounts'),
    p(tr('Suspender preserva os dados da conta e encerra o acesso ao portal. Sessões já abertas no fórum são administradas no próprio fórum.', 'Suspension preserves account data and ends portal access. Existing forum sessions are managed in the forum itself.', 'La suspensión conserva los datos de la cuenta y cierra el acceso al portal. Las sesiones ya abiertas en el foro se administran en el propio foro.')),
    table([tr('Usuário', 'Username'), tr('Nome', 'Name'), tr('Situação', 'Status'), tr('Criada em', 'Created'), tr('Acesso', 'Access', 'Acceso')], accounts.map(account => [account.userName, account.displayName, badge(account.isSuspended ? tr('Suspensa', 'Suspended', 'Suspendida') : account.invitationAccepted ? tr('Ativa', 'Active') : account.email ? tr('Aguardando ativação', 'Awaiting activation') : tr('Convite pendente', 'Pending invitation')), date(account.createdAtUtc), accountAccessAction(account)]))));
  return result;
}
async function templatesAdmin() {
  const templates = await api('/admin/templates'); const editor = h('div');
  function edit(template) {
    const preview = h('pre', { class: 'template-preview' });
    const node = form([
      h('div', { class: 'content-grid' }, [h('div', {}, [h('h3', {}, 'Português'), field('Assunto', input('subjectPt', 'text', template.subjectPt, { maxLength: 200 })), field('Mensagem', textarea('bodyPt', template.bodyPt, { maxLength: 8000, rows: 8 }))]), h('div', {}, [h('h3', {}, 'English'), field('Subject', input('subjectEn', 'text', template.subjectEn, { maxLength: 200 })), field('Message', textarea('bodyEn', template.bodyEn, { maxLength: 8000, rows: 8 }))]), h('div', {}, [h('h3', {}, 'Español'), field('Asunto', input('subjectEs', 'text', template.subjectEs, { maxLength: 200 })), field('Mensaje', textarea('bodyEs', template.bodyEs, { maxLength: 8000, rows: 8 }))])]),
      p(tr('Variáveis: {{name}}, {{link}}, {{reference}}. Somente texto; nenhum script é executado.', 'Variables: {{name}}, {{link}}, {{reference}}. Text only; no script is executed.')), h('h3', {}, tr('Prévia no idioma atual', 'Preview in current language')), preview
    ], tr('Salvar modelo', 'Save template'), async data => { await api(`/admin/templates/${encodeURIComponent(template.key)}`, 'PUT', data); Object.assign(template, data); feedback(tr('Modelo salvo. O envio permanece desativado.', 'Template saved. Delivery remains disabled.')); });
    const update = () => { const data = values(node); const subject = state.lang === 'en' ? data.subjectEn : state.lang === 'es' ? data.subjectEs : data.subjectPt; const body = state.lang === 'en' ? data.bodyEn : state.lang === 'es' ? data.bodyEs : data.bodyPt; preview.textContent = `${subject}\n\n${body}`.replaceAll('{{name}}', 'Alex').replaceAll('{{reference}}', 'SCADIVO-EJEMPLO').replaceAll('{{link}}', 'https://scadivo.com/portal/'); };
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
      h('div', { class: 'content-grid' }, [h('div', {}, [field('Título (PT)', input('titlePt', 'text', article.titlePt || '', { maxLength: 200 })), field('Texto (PT)', textarea('bodyPt', article.bodyPt || '', { maxLength: 12000, rows: 9 }))]), h('div', {}, [field('Title (EN)', input('titleEn', 'text', article.titleEn || '', { maxLength: 200 })), field('Text (EN)', textarea('bodyEn', article.bodyEn || '', { maxLength: 12000, rows: 9 }))]), h('div', {}, [field('Título (ES)', input('titleEs', 'text', article.titleEs || '', { maxLength: 200 })), field('Texto (ES)', textarea('bodyEs', article.bodyEs || '', { maxLength: 12000, rows: 9 }))])]),
      h('label', { class: 'check-field' }, [published, tr('Publicado no FAQ', 'Published in FAQ')])
    ], tr('Salvar artigo', 'Save article'), async data => { await api(`/admin/faq${article.id ? `/${article.id}` : ''}`, article.id ? 'PUT' : 'POST', { ...data, published: published.checked }); feedback(tr('Artigo salvo.', 'Article saved.')); render(); }));
  }
  const picker = select('article', [['', tr('Novo artigo', 'New article')], ...articles.map(x => [x.id, state.lang === 'en' ? x.titleEn : state.lang === 'es' ? x.titleEs : x.titlePt])], ''); picker.addEventListener('change', () => edit(articles.find(x => x.id === picker.value))); edit();
  return panel(tr('Editar base de conhecimento', 'Edit knowledge base'), field(tr('Artigo', 'Article'), picker), editor);
}
async function auditAdmin() {
  const events = await api('/admin/audit');
  return panel(tr('Últimas 100 ações', 'Latest 100 actions'), p(tr('O registro não contém senhas nem códigos de convite.', 'The log contains no passwords or invitation codes.')), table([tr('Data/hora', 'Date/time'), tr('Ação', 'Action'), tr('Referência', 'Reference')], events.map(event => [date(event.createdAtUtc), event.action, event.resourceId || '—'])));
}

async function start() {
  const fragment = location.hash.slice(1);
  if (fragment.startsWith('community-login=')) {
    try {
      state.communitySso = parseCommunitySso(fragment.slice('community-login='.length));
      sessionStorage.setItem(communitySsoStorageKey, JSON.stringify({ ...state.communitySso, savedAt: Date.now() }));
    } catch {
      try { sessionStorage.removeItem(communitySsoStorageKey); } catch { /* Ignore unavailable storage. */ }
      feedback(tr('O pedido de entrada na comunidade é inválido. Abra novamente o link do fórum.', 'This community sign-in request is invalid. Open the forum link again.'), true);
    }
    history.replaceState(null, '', '/portal/');
  } else if (fragment === 'community-logout') {
    state.communitySso = null;
    try { sessionStorage.removeItem(communitySsoStorageKey); } catch { /* Prevent a stale SSO request from resuming after logout. */ }
  } else if (fragment.startsWith('invite=')) {
    try {
      const encoded = fragment.slice(7).replaceAll('-', '+').replaceAll('_', '/');
      const parsed = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(encoded), x => x.charCodeAt(0))));
      if (typeof parsed.userId !== 'string' || typeof parsed.token !== 'string') throw new Error('Invalid invitation');
      state.invite = { userId: parsed.userId, token: parsed.token };
    } catch { feedback(tr('O link de convite é inválido.', 'This invitation link is invalid.'), true); }
    history.replaceState(null, '', '/portal/#accept-invite');
  } else if (fragment.startsWith('confirm-email=')) {
    try {
      const encoded = fragment.slice('confirm-email='.length).replaceAll('-', '+').replaceAll('_', '/');
      const parsed = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(encoded), x => x.charCodeAt(0))));
      if (typeof parsed.userId !== 'string' || typeof parsed.token !== 'string') throw new Error('Invalid confirmation');
      state.confirmEmail = { userId: parsed.userId, token: parsed.token };
    } catch { feedback(tr('O link de confirmação é inválido.', 'This confirmation link is invalid.'), true); }
    history.replaceState(null, '', '/portal/#confirm-email');
  } else if (fragment.startsWith('reset-password=')) {
    try {
      const encoded = fragment.slice('reset-password='.length).replaceAll('-', '+').replaceAll('_', '/');
      const parsed = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(encoded), x => x.charCodeAt(0))));
      if (typeof parsed.userId !== 'string' || typeof parsed.token !== 'string') throw new Error('Invalid password reset');
      state.resetPassword = { userId: parsed.userId, token: parsed.token };
    } catch { feedback(tr('O link de recuperação é inválido.', 'This recovery link is invalid.'), true); }
    history.replaceState(null, '', '/portal/#reset-password');
  } else if (fragment === 'forgot-password') state.recover = true;
  else if (fragment === 'register') state.register = true;
  else if (labels()[fragment]) state.tab = fragment;
  const communityLogoutRequested = fragment === 'community-logout';
  if (!state.communitySso && !communityLogoutRequested) {
    try {
      const saved = sessionStorage.getItem(communitySsoStorageKey);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (Number.isFinite(parsed.savedAt) && Date.now() - parsed.savedAt >= 0 && Date.now() - parsed.savedAt <= 25 * 60 * 1000)
          state.communitySso = validateCommunitySso(parsed);
        else sessionStorage.removeItem(communitySsoStorageKey);
      }
    } catch { try { sessionStorage.removeItem(communitySsoStorageKey); } catch { /* Ignore unavailable storage. */ } }
  }
  try {
    await session();
    if (communityLogoutRequested) {
      if (state.session?.authenticated) await api('/auth/logout', 'POST', {});
      await session();
      history.replaceState(null, '', '/portal/');
      state.tab = 'home';
      render();
      feedback(tr('Sessão do portal encerrada. Para acessar a comunidade novamente, entre pelo fórum.', 'Portal session ended. To access the community again, sign in from the forum.', 'La sesión del portal se cerró. Para volver a la comunidad, inicia sesión desde el foro.'));
      return;
    }
    if (continueCommunitySso()) return;
    render();
  } catch {
    app.replaceChildren(panel(tr('Portal indisponível', 'Portal unavailable'), p(tr('Não foi possível conectar. Aguarde um momento e tente novamente.', 'Unable to connect. Please wait a moment and try again.')), button(tr('Tentar novamente', 'Try again'), start)));
  }
}
start();
