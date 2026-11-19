'use strict';

const portalDownloads = (() => {
  let selectedReleaseId = null;
  const platformOptions = () => [['windows-x64', 'Windows x64'], ['linux-x64', 'Linux x64'], ['linux-arm64', 'Linux ARM64'], ['docker', 'Docker / OCI']];
  const platformName = value => platformOptions().find(([id]) => id === value)?.[1] || value;
  const noteKey = locale => locale === 'en' ? 'notesEn' : locale === 'es' ? 'notesEs' : 'notesPt';
  const publishedName = value => value ? tr('Publicado', 'Published', 'Publicado') : tr('Rascunho', 'Draft', 'Borrador');

  function errors() {
    return {
      'download-invalid-origin': tr('Use o link HTTPS de um arquivo no Release oficial do SCADIVO, sem parâmetros ou credenciais.', 'Use an HTTPS file link from the official SCADIVO release, without query parameters or credentials.', 'Utiliza el enlace HTTPS de un archivo del Release oficial de SCADIVO, sin parámetros ni credenciales.'),
      'download-invalid-sha256': tr('O SHA-256 deve conter 64 caracteres hexadecimais.', 'SHA-256 must contain 64 hexadecimal characters.', 'El SHA-256 debe contener 64 caracteres hexadecimales.'),
      'download-not-ready': tr('Para publicar, informe o arquivo oficial, o SHA-256 e as notas nos três idiomas.', 'To publish, provide the official file, SHA-256 and notes in all three languages.', 'Para publicar, indica el archivo oficial, el SHA-256 y las notas en los tres idiomas.'),
      'download-already-exists': tr('Já existe um download com essa versão e plataforma.', 'A download with this version and platform already exists.', 'Ya existe una descarga con esta versión y plataforma.'),
      'release-not-approved': tr('Esse download não está disponível no momento.', 'This download is currently unavailable.', 'Esta descarga no está disponible en este momento.')
    };
  }

  async function customer() {
    const releases = await api('/downloads');
    const list = h('div', { class: 'content-grid download-list' });
    const picker = select('platform', [['', tr('Todas as plataformas', 'All platforms', 'Todas las plataformas')], ...platformOptions()], '', { 'aria-label': tr('Plataforma', 'Platform', 'Plataforma') });
    const update = () => {
      const visible = releases.filter(release => !picker.value || release.platform === picker.value);
      list.replaceChildren(...visible.map(release => panel(`${platformName(release.platform)} · ${release.version}`,
        p(release[noteKey(state.lang)]),
        h('code', { class: 'checksum' }, `SHA-256 ${release.sha256}`),
        h('a', { class: 'button primary', href: `/portal/api/downloads/${release.id}/file` }, tr('Baixar arquivo ↗', 'Download file ↗', 'Descargar archivo ↗')))));
      if (!visible.length) list.append(notice(tr('Nenhum download disponível para esta plataforma.', 'No downloads available for this platform.', 'No hay descargas disponibles para esta plataforma.')));
    };
    picker.addEventListener('change', update); update();
    return panel(tr('Distribuição oficial', 'Official distribution', 'Distribución oficial'),
      p(tr('Escolha sua plataforma e confira as notas da versão. Compare o SHA-256 do arquivo baixado com o informado no catálogo.', 'Choose your platform and read the release notes. Compare the downloaded file’s SHA-256 with the value in the catalog.', 'Elige tu plataforma y consulta las notas de la versión. Compara el SHA-256 del archivo descargado con el valor del catálogo.')),
      releases.length ? field(tr('Plataforma', 'Platform', 'Plataforma'), picker) : notice(tr('Nenhum instalador publicado nesta etapa. Não há links provisórios ou arquivos não validados.', 'No installer is published at this stage. There are no placeholder links or unverified files.', 'Todavía no hay instaladores publicados. No ofrecemos enlaces provisionales ni archivos sin validar.')),
      releases.length ? list : null);
  }

  async function admin() {
    const releases = await api('/admin/downloads');
    const editor = h('div', { class: 'download-editor' });
    function edit(release = {}) {
      selectedReleaseId = release.id || null;
      const notes = { notesPt: release.notesPt || '', notesEn: release.notesEn || '', notesEs: release.notesEs || '' };
      let locale = state.lang;
      const localePicker = select('notesLocale', localeOptions(), locale, { 'aria-label': tr('Idioma das notas', 'Notes language', 'Idioma de las notas') });
      const notesField = textarea('notes', notes[noteKey(locale)], { rows: 7, maxLength: 8000, required: false, 'aria-label': tr('Notas da versão', 'Release notes', 'Notas de la versión') });
      const url = input('officialUrl', 'url', release.officialUrl || '', { required: Boolean(release.published), maxLength: 2000, placeholder: 'https://github.com/…/releases/download/…' });
      const hash = input('sha256', 'text', release.sha256 || '', { required: Boolean(release.published), pattern: '[a-fA-F0-9]{64}', maxLength: 64, class: 'download-hash', spellcheck: false });
      const published = input('published', 'checkbox', '', { required: false, checked: Boolean(release.published) });
      const node = form([
        h('div', { class: 'form-row' }, [
          field(tr('Versão', 'Version', 'Versión'), input('version', 'text', release.version || '', { maxLength: 80, pattern: '[A-Za-z0-9][A-Za-z0-9._+\\-]{0,79}', placeholder: 'v1.0.0' })),
          field(tr('Plataforma', 'Platform', 'Plataforma'), select('platform', [['', tr('Selecione a plataforma', 'Select the platform', 'Selecciona la plataforma')], ...platformOptions()], release.platform || '', { required: true, 'aria-label': tr('Plataforma', 'Platform', 'Plataforma') }))
        ]),
        field(tr('Link do arquivo oficial', 'Official file link', 'Enlace del archivo oficial'), url, tr('O arquivo deve estar em um Release oficial. Este formulário disponibiliza seu link no Portal.', 'The file must be in an official release. This form makes its link available in the Portal.', 'El archivo debe estar en un Release oficial. Este formulario publica su enlace en el Portal.')),
        field('SHA-256', hash, tr('Copie o checksum publicado junto ao artefato aprovado.', 'Copy the checksum published with the approved artifact.', 'Copia el checksum publicado con el artefacto aprobado.')),
        field(tr('Idioma das notas', 'Notes language', 'Idioma de las notas'), localePicker),
        field(tr('Notas da versão', 'Release notes', 'Notas de la versión'), notesField),
        h('label', { class: 'check-field' }, [published, tr('Publicado no catálogo dos usuários', 'Published in the user catalog', 'Publicado en el catálogo de usuarios')]),
        p(tr('Rascunhos ficam visíveis somente para a administração. Para publicar, complete o link, o SHA-256 e as notas em Português, English e Español. Desmarque a publicação para retirar o item do catálogo.', 'Drafts are visible only to administrators. To publish, complete the link, SHA-256 and notes in Português, English and Español. Uncheck publication to withdraw the item from the catalog.', 'Los borradores solo son visibles para administradores. Para publicar, completa el enlace, el SHA-256 y las notas en Português, English y Español. Desmarca la publicación para retirar el elemento del catálogo.'))
      ], tr('Salvar download', 'Save download', 'Guardar descarga'), async data => {
        notes[noteKey(locale)] = notesField.value;
        if (published.checked) {
          const missing = localeOptions().find(([language]) => !notes[noteKey(language)].trim());
          if (missing) {
            locale = missing[0]; localePicker.value = locale; notesField.value = notes[noteKey(locale)]; notesField.focus();
            throw new Error(tr(`Preencha as notas em ${missing[1]} antes de publicar.`, `Complete the ${missing[1]} notes before publishing.`, `Completa las notas en ${missing[1]} antes de publicar.`));
          }
        }
        const result = await api(`/admin/downloads${release.id ? `/${release.id}` : ''}`, release.id ? 'PUT' : 'POST', {
          version: data.version, platform: data.platform, officialUrl: data.officialUrl || '', sha256: data.sha256 || '',
          ...notes, published: published.checked
        });
        selectedReleaseId = result.id;
        feedback(tr('Download salvo.', 'Download saved.', 'Descarga guardada.')); await render();
      });
      notesField.addEventListener('input', () => { notes[noteKey(locale)] = notesField.value; });
      localePicker.addEventListener('change', () => { notes[noteKey(locale)] = notesField.value; locale = localePicker.value; notesField.value = notes[noteKey(locale)]; });
      published.addEventListener('change', () => { url.required = published.checked; hash.required = published.checked; });
      editor.replaceChildren(panel(release.id ? tr('Editar download', 'Edit download', 'Editar descarga') : tr('Novo download', 'New download', 'Nueva descarga'), node));
    }
    const cards = releases.map(release => h('article', { class: 'download-admin-card' }, [
      h('div', {}, [h('h3', {}, `${platformName(release.platform)} · ${release.version}`), badge(publishedName(release.published)), h('p', { class: 'muted' }, `${tr('Criado em', 'Created', 'Creado el')}: ${date(release.createdAtUtc)}`)]),
      button(tr('Editar', 'Edit', 'Editar'), () => { edit(release); editor.scrollIntoView({ behavior: 'smooth', block: 'start' }); })
    ]));
    edit(releases.find(release => release.id === selectedReleaseId));
    return h('div', {}, [
      panel(tr('Downloads do Portal', 'Portal downloads', 'Descargas del Portal'),
        h('div', { class: 'split-toolbar' }, [p(tr('Gerencie as versões disponíveis para contas cadastradas.', 'Manage releases available to registered accounts.', 'Gestiona las versiones disponibles para cuentas registradas.')), button(tr('Novo download', 'New download', 'Nueva descarga'), () => edit())]),
        cards.length ? h('div', { class: 'download-admin-list' }, cards) : notice(tr('Nenhum download cadastrado. Você pode começar por um rascunho.', 'No downloads registered. You can start with a draft.', 'No hay descargas registradas. Puedes empezar con un borrador.'))),
      editor
    ]);
  }

  return { admin, customer, errors };
})();
