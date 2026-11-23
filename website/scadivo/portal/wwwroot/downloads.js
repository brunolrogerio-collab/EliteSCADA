'use strict';

const portalDownloads = (() => {
  let selectedReleaseId = null;
  const categoryOptions = () => [['software', tr('Software', 'Software', 'Software')], ['documentation', tr('Documentação', 'Documentation', 'Documentación')]];
  const categoryName = value => categoryOptions().find(([id]) => id === value)?.[1] || value;
  const platformOptions = () => [['windows-x64', 'Windows x64'], ['linux-x64', 'Linux x64'], ['linux-arm64', 'Linux ARM64'], ['docker', 'Docker / OCI']];
  const platformName = value => platformOptions().find(([id]) => id === value)?.[1] || value;
  const titleKey = locale => locale === 'en' ? 'titleEn' : locale === 'es' ? 'titleEs' : 'titlePt';
  const noteKey = locale => locale === 'en' ? 'notesEn' : locale === 'es' ? 'notesEs' : 'notesPt';
  const publishedName = value => value ? tr('Publicado', 'Published', 'Publicado') : tr('Rascunho', 'Draft', 'Borrador');

  function errors() {
    return {
      'download-invalid-origin': tr('Use o link HTTPS de um arquivo no Release oficial do SCADIVO, sem parâmetros ou credenciais.', 'Use an HTTPS file link from the official SCADIVO release, without query parameters or credentials.', 'Utiliza el enlace HTTPS de un archivo del Release oficial de SCADIVO, sin parámetros ni credenciales.'),
      'download-invalid-sha256': tr('O SHA-256 deve conter 64 caracteres hexadecimais.', 'SHA-256 must contain 64 hexadecimal characters.', 'El SHA-256 debe contener 64 caracteres hexadecimales.'),
      'download-not-ready': tr('Para publicar, informe o arquivo oficial, o SHA-256, o título e as informações nos três idiomas.', 'To publish, provide the official file, SHA-256, title and information in all three languages.', 'Para publicar, indica el archivo oficial, el SHA-256, el título y la información en los tres idiomas.'),
      'download-already-exists': tr('Já existe um item com essa versão, plataforma e categoria.', 'An item with this version, platform and category already exists.', 'Ya existe un elemento con esta versión, plataforma y categoría.'),
      'release-not-approved': tr('Esse download não está disponível no momento.', 'This download is currently unavailable.', 'Esta descarga no está disponible en este momento.')
    };
  }

  async function customer() {
    const releases = await api('/downloads');
    const list = h('div', { class: 'download-list' });
    const categoryPicker = select('category', [['', tr('Todas as categorias', 'All categories', 'Todas las categorías')], ...categoryOptions()], '', { 'aria-label': tr('Categoria', 'Category', 'Categoría') });
    const platformPicker = select('platform', [['', tr('Todas as plataformas', 'All platforms', 'Todas las plataformas')], ...platformOptions()], '', { 'aria-label': tr('Plataforma', 'Platform', 'Plataforma') });
    const platformFilter = field(tr('Plataforma', 'Platform', 'Plataforma'), platformPicker);
    const downloadIcon = () => h('svg', { viewBox: '0 0 24 24', width: '19', height: '19', fill: 'none', stroke: 'currentColor', 'stroke-width': '1.8', 'stroke-linecap': 'round', 'stroke-linejoin': 'round', 'aria-hidden': 'true' }, [
      h('path', { d: 'M12 3v12' }), h('path', { d: 'm7 10 5 5 5-5' }), h('path', { d: 'M5 20h14' })
    ]);
    const itemView = release => {
      const title = release[titleKey(state.lang)] || release.version;
      const titleButton = h('summary', {}, title);
      const info = h('div', { class: 'download-item-info' }, [
        p(`${tr('Categoria', 'Category', 'Categoría')}: ${categoryName(release.category)}`),
        p(`${tr('Versão', 'Version', 'Versión')}: ${release.version}`),
        p(release.category === 'documentation'
          ? tr('Aplicável a todos os ambientes.', 'Applies to all environments.', 'Aplicable a todos los entornos.')
          : `${tr('Plataforma', 'Platform', 'Plataforma')}: ${platformName(release.platform)}`),
        p(release[noteKey(state.lang)]),
        h('code', { class: 'checksum' }, `SHA-256 ${release.sha256}`)
      ]);
      return h('article', { class: 'download-item' }, [
        h('details', { class: 'download-item-details' }, [titleButton, info]),
        h('a', { class: 'download-icon-link', href: `/portal/api/downloads/${release.id}/file`,
          'aria-label': tr(`Baixar ${title}`, `Download ${title}`, `Descargar ${title}`),
          title: tr(`Baixar ${title}`, `Download ${title}`, `Descargar ${title}`) }, downloadIcon())
      ]);
    };
    const update = () => {
      const category = categoryPicker.value;
      platformFilter.hidden = category === 'documentation';
      const visible = releases.filter(release => (!category || release.category === category) &&
        (!platformPicker.value || release.category !== 'software' || release.platform === platformPicker.value));
      list.replaceChildren(...visible.map(itemView));
      if (!visible.length) list.append(notice(tr('Nenhum arquivo disponível com esses filtros.', 'No files are available for these filters.', 'No hay archivos disponibles con estos filtros.')));
    };
    categoryPicker.addEventListener('change', update); platformPicker.addEventListener('change', update); update();
    return panel(tr('Distribuição oficial', 'Official distribution', 'Distribución oficial'),
      releases.length ? h('div', { class: 'download-filters' }, [field(tr('Categoria', 'Category', 'Categoría'), categoryPicker), platformFilter]) : notice(tr('Nenhum arquivo publicado. Os arquivos serão exibidos quando forem validados e disponibilizados.', 'No files are published. Files will appear when they are validated and made available.', 'No hay archivos publicados. Los archivos aparecerán cuando estén validados y disponibles.')),
      releases.length ? list : null);
  }

  async function admin() {
    const releases = await api('/admin/downloads');
    const editor = h('div', { class: 'download-editor' });
    function edit(release = {}) {
      selectedReleaseId = release.id || null;
      const category = select('category', categoryOptions(), release.category || 'software', { required: true, 'aria-label': tr('Categoria', 'Category', 'Categoría') });
      const titles = { titlePt: release.titlePt || release.version || '', titleEn: release.titleEn || release.version || '', titleEs: release.titleEs || release.version || '' };
      const notes = { notesPt: release.notesPt || '', notesEn: release.notesEn || '', notesEs: release.notesEs || '' };
      let locale = state.lang;
      const localePicker = select('notesLocale', localeOptions(), locale, { 'aria-label': tr('Idioma dos textos', 'Text language', 'Idioma de los textos') });
      const titleField = input('title', 'text', titles[titleKey(locale)], { maxLength: 160, required: false, 'aria-label': tr('Título', 'Title', 'Título') });
      const notesField = textarea('notes', notes[noteKey(locale)], { rows: 7, maxLength: 8000, required: false, 'aria-label': tr('Notas da versão', 'Release notes', 'Notas de la versión') });
      const platformSelect = select('platform', [['', tr('Selecione a plataforma', 'Select the platform', 'Selecciona la plataforma')], ...platformOptions()], release.platform || '', { required: true, 'aria-label': tr('Plataforma', 'Platform', 'Plataforma') });
      const platformField = field(tr('Plataforma', 'Platform', 'Plataforma'), platformSelect);
      const updateCategoryFields = () => {
        const documentation = category.value === 'documentation';
        platformField.hidden = documentation;
        platformSelect.required = !documentation;
        if (documentation) platformSelect.value = '';
      };
      category.addEventListener('change', updateCategoryFields);
      updateCategoryFields();
      const url = input('officialUrl', 'url', release.officialUrl || '', { required: Boolean(release.published), maxLength: 2000, placeholder: 'https://github.com/…/releases/download/…' });
      const hash = input('sha256', 'text', release.sha256 || '', { required: Boolean(release.published), pattern: '[a-fA-F0-9]{64}', maxLength: 64, class: 'download-hash', spellcheck: false });
      const published = input('published', 'checkbox', '', { required: false, checked: Boolean(release.published) });
      const node = form([
        h('div', { class: 'form-row' }, [
          field(tr('Categoria', 'Category', 'Categoría'), category),
          field(tr('Versão ou identificação', 'Version or identifier', 'Versión o identificador'), input('version', 'text', release.version || '', { maxLength: 80, pattern: '[A-Za-z0-9][A-Za-z0-9._+\\-]{0,79}', placeholder: 'v1.0.0' }))
        ]),
        platformField,
        field(tr('Link do arquivo oficial', 'Official file link', 'Enlace del archivo oficial'), url, tr('O arquivo deve estar em um Release oficial. Este formulário disponibiliza seu link no Portal.', 'The file must be in an official release. This form makes its link available in the Portal.', 'El archivo debe estar en un Release oficial. Este formulario publica su enlace en el Portal.')),
        field('SHA-256', hash, tr('Copie o checksum publicado junto ao artefato aprovado.', 'Copy the checksum published with the approved artifact.', 'Copia el checksum publicado con el artefacto aprobado.')),
        field(tr('Idioma das notas', 'Notes language', 'Idioma de las notas'), localePicker),
        field(tr('Título', 'Title', 'Título'), titleField),
        field(tr('Notas da versão', 'Release notes', 'Notas de la versión'), notesField),
        h('label', { class: 'check-field' }, [published, tr('Publicado no catálogo dos usuários', 'Published in the user catalog', 'Publicado en el catálogo de usuarios')]),
        p(tr('Rascunhos ficam visíveis somente para a administração. Para publicar, complete título, arquivo, SHA-256 e notas em Português, English e Español. Documentação não exige plataforma; software exige uma plataforma. Desmarque a publicação para retirar o item do catálogo.', 'Drafts are visible only to administrators. To publish, provide a title, file, SHA-256 and notes in Português, English and Español. Documentation does not require a platform; software does. Uncheck publication to withdraw the item from the catalog.', 'Los borradores solo son visibles para administradores. Para publicar, completa el título, archivo, SHA-256 y notas en Português, English y Español. La documentación no requiere plataforma; el software sí. Desmarca la publicación para retirar el elemento del catálogo.'))
      ], tr('Salvar download', 'Save download', 'Guardar descarga'), async data => {
        titles[titleKey(locale)] = titleField.value;
        notes[noteKey(locale)] = notesField.value;
        if (published.checked) {
          const missing = localeOptions().find(([language]) => !titles[titleKey(language)].trim() || !notes[noteKey(language)].trim());
          if (missing) {
            locale = missing[0]; localePicker.value = locale;
            titleField.value = titles[titleKey(locale)]; notesField.value = notes[noteKey(locale)];
            (titleField.value.trim() ? notesField : titleField).focus();
            throw new Error(tr(`Preencha o título e as notas em ${missing[1]} antes de publicar.`, `Complete the title and notes in ${missing[1]} before publishing.`, `Completa el título y las notas en ${missing[1]} antes de publicar.`));
          }
        }
        const result = await api(`/admin/downloads${release.id ? `/${release.id}` : ''}`, release.id ? 'PUT' : 'POST', {
          version: data.version, category: data.category, platform: data.category === 'documentation' ? '' : data.platform,
          titlePt: titles.titlePt, titleEn: titles.titleEn, titleEs: titles.titleEs,
          officialUrl: data.officialUrl || '', sha256: data.sha256 || '', ...notes, published: published.checked
        });
        selectedReleaseId = result.id;
        feedback(tr('Download salvo.', 'Download saved.', 'Descarga guardada.')); await render();
      });
      notesField.addEventListener('input', () => { notes[noteKey(locale)] = notesField.value; });
      localePicker.addEventListener('change', () => {
        titles[titleKey(locale)] = titleField.value; notes[noteKey(locale)] = notesField.value;
        locale = localePicker.value; titleField.value = titles[titleKey(locale)]; notesField.value = notes[noteKey(locale)];
      });
      published.addEventListener('change', () => { url.required = published.checked; hash.required = published.checked; });
      editor.replaceChildren(panel(release.id ? tr('Editar download', 'Edit download', 'Editar descarga') : tr('Novo download', 'New download', 'Nueva descarga'), node));
    }
    const cards = releases.map(release => h('article', { class: 'download-admin-card' }, [
      h('div', {}, [h('h3', {}, `${release[titleKey(state.lang)] || release.version} · ${release.version}`), badge(categoryName(release.category)), badge(publishedName(release.published)),
        p(release.category === 'documentation' ? tr('Todos os ambientes', 'All environments', 'Todos los entornos') : platformName(release.platform)),
        p(tr(`${release.downloadStarts ?? 0} downloads iniciados`, `${release.downloadStarts ?? 0} download starts`, `${release.downloadStarts ?? 0} descargas iniciadas`)), h('p', { class: 'muted' }, `${tr('Criado em', 'Created', 'Creado el')}: ${date(release.createdAtUtc)}`)]),
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
