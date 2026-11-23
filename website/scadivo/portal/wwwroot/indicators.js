'use strict';

const portalIndicators = (() => {
  const number = value => new Intl.NumberFormat(state.lang).format(value);
  const shortDate = value => new Intl.DateTimeFormat(state.lang, { day: 'numeric', month: 'short', timeZone: 'UTC' }).format(new Date(value));
  const percent = (count, total) => new Intl.NumberFormat(state.lang, { style: 'percent', maximumFractionDigits: 1 }).format(total ? count / total : 0);

  function profileTable(rows, total, labels = new Map()) {
    return rows.length ? table([
      tr('Grupo', 'Group', 'Grupo'), tr('Perfis', 'Profiles', 'Perfiles'), tr('Percentual', 'Share', 'Porcentaje')
    ], rows.map(row => [labels.get(row.label) || row.label, number(row.count), percent(row.count, total)]))
      : p(tr('Nenhum perfil disponível.', 'No profiles available.', 'No hay perfiles disponibles.'));
  }

  function trafficChart(rows, max, label) {
    return panel(label, h('div', { class: 'indicator-chart' }, rows.map(({ day, count }) =>
      h('div', { class: 'indicator-chart-row' }, [
        h('time', { datetime: day }, shortDate(day)),
        h('meter', { min: 0, max, value: count, 'aria-label': `${label} · ${shortDate(day)}` }),
        h('strong', {}, number(count))
      ]))));
  }

  function reportView(report) {
    const { profiles, downloads, traffic } = report;
    const siteLabel = tr('Site', 'Website', 'Sitio web');
    const portalLabel = 'Portal';
    const maximum = Math.max(1, ...traffic.flatMap(row => [row.site, row.portal]));
    const affiliations = [...profiles.affiliations];
    if (profiles.otherAffiliations) affiliations.push({
      label: tr('Vínculos com menos de 3 perfis', 'Affiliations with fewer than 3 profiles', 'Vínculos con menos de 3 perfiles'),
      count: profiles.otherAffiliations
    });
    return [
      panel(tr('Visualizações de página', 'Page views', 'Vistas de página'),
        p(tr('Contagem diária em UTC. Recarregar a página conta como nova visualização.', 'Daily counts in UTC. Reloading counts as a new page view.', 'Conteo diario en UTC. Recargar cuenta como una nueva vista.')),
        h('div', { class: 'indicator-grid' }, ['site', 'portal'].map((surface, index) => {
          const label = [siteLabel, portalLabel][index];
          return trafficChart(traffic.map(row => ({ day: row.day, count: row[surface] })), maximum,
            `${label} · ${number(traffic.reduce((total, row) => total + row[surface], 0))}`);
        }))),
      panel(tr('Downloads iniciados por versão', 'Download starts by version', 'Descargas iniciadas por versión'),
        p(tr('Contagem por item do catálogo no período selecionado, no momento do redirecionamento.', 'Counts by catalog item in the selected period, recorded when the download redirects.', 'Conteo por elemento del catálogo en el periodo seleccionado, registrado al redirigir.')),
        downloads.length ? table([tr('Categoria', 'Category', 'Categoría'), tr('Plataforma', 'Platform', 'Plataforma'), tr('Versão', 'Version', 'Versión'), tr('Iniciados', 'Starts', 'Iniciadas')],
          downloads.map(row => [row.category === 'documentation' ? tr('Documentação', 'Documentation', 'Documentación') : tr('Software', 'Software', 'Software'), row.platform || tr('Todos os ambientes', 'All environments', 'Todos los entornos'), row.version, number(row.count)]))
          : p(tr('Nenhum item no catálogo.', 'No catalog entries.', 'No hay elementos en el catálogo.'))),
      panel(tr('Perfil do público', 'Audience profile', 'Perfil del público'),
        p(tr(`${number(profiles.total)} contas ativas e confirmadas com perfil preenchido. Esta distribuição é atual, independente do período.`,
          `${number(profiles.total)} active, confirmed accounts with a completed profile. This breakdown reflects current profiles.`,
          `${number(profiles.total)} cuentas activas y confirmadas con perfil completo. La distribución refleja los perfiles actuales.`)),
        h('div', { class: 'indicator-grid' }, [
          panel(tr('Tipo de perfil', 'Profile type', 'Tipo de perfil'), profileTable(profiles.types, profiles.total, new Map(profileTypeOptions()))),
          panel(tr('Área de atuação', 'Field of work or study', 'Área de actividad'), profileTable(profiles.areas, profiles.total, new Map(activityAreaOptions()))),
          panel(tr('Empresa ou escola', 'Company or school', 'Empresa o centro educativo'), profileTable(affiliations, profiles.total))
        ]))
    ];
  }

  async function admin() {
    const first = await api('/admin/indicators');
    const report = h('div', { 'aria-live': 'polite' }, reportView(first));
    const dateFilter = form([
      field(tr('De (UTC)', 'From (UTC)', 'Desde (UTC)'), input('from', 'date', first.from, { max: first.to })),
      field(tr('Até (UTC)', 'To (UTC)', 'Hasta (UTC)'), input('to', 'date', first.to, { max: first.to }))
    ], tr('Aplicar período', 'Apply date range', 'Aplicar periodo'), async data => {
      const query = new URLSearchParams(data);
      report.replaceChildren(...reportView(await api(`/admin/indicators?${query}`)));
    });
    return h('div', {}, [panel(tr('Indicadores administrativos', 'Administration indicators', 'Indicadores administrativos'), dateFilter), report]);
  }

  return { admin };
})();
