import React from 'react';
import type { EngineeringSnapshot, RuntimeHeaderEngineering, RuntimeHeaderTextStyleEngineering } from './types';
import type { EngineeringLocale } from './i18n';
import { listUserVisualAssets } from './visualAssetCatalogModel';

export function RuntimeHeaderSettings({ value, onChange, snapshot, locale }: {
  value: RuntimeHeaderEngineering; onChange: (value: RuntimeHeaderEngineering) => void;
  snapshot: EngineeringSnapshot; locale: EngineeringLocale;
}) {
  const t = headerSettingsText(locale);
  const update = (patch: Partial<RuntimeHeaderEngineering>) => onChange({ ...value, ...patch });
  const links = value.links ?? [];
  const height = Math.max(56, Math.min(168, value.height ?? 56));
  const backgroundUsesTheme = !value.backgroundColor;
  const dateTime = value.dateTime ?? {};
  return <section className="runtime-header-settings" data-testid="runtime-header-settings" aria-labelledby="runtime-header-settings-title">
    <header className="runtime-header-settings__heading">
      <div><span className="eng-eyebrow">{t.eyebrow}</span><h2 id="runtime-header-settings-title">{t.title}</h2>
        <p>{t.description}</p></div>
      <label className="runtime-header-settings__enabled"><input type="checkbox" checked={value.enabled !== false} onChange={event => update({ enabled: event.currentTarget.checked })}/>{t.enabled}</label>
    </header>

    <div className="runtime-header-settings__grid">
      <fieldset className="eng-panel runtime-header-settings__panel">
        <legend>{t.appearance}</legend>
        <label className="runtime-header-settings__range"><span>{t.height} <output>{height}px · {(height / 56).toFixed(1)}×</output></span>
          <input type="range" min={56} max={168} step={4} value={height} onChange={event => update({ height: Number(event.currentTarget.value) })} aria-label={t.height}/>
          <small>{t.heightHint}</small>
        </label>
        <label className="runtime-header-settings__inline"><input type="checkbox" checked={backgroundUsesTheme} onChange={event => update({ backgroundColor: event.currentTarget.checked ? null : '#243746' })}/>
          <span><strong>{t.followTheme}</strong><small>{t.followThemeHint}</small></span>
        </label>
        {!backgroundUsesTheme ? <label>{t.backgroundColor}<input aria-label={t.backgroundColor} type="color" value={value.backgroundColor!.slice(0, 7)} onChange={event => update({ backgroundColor: event.currentTarget.value })}/></label> : null}
      </fieldset>

      <fieldset className="eng-panel runtime-header-settings__panel">
        <legend>{t.layout}</legend>
        <label>{t.projectAlignment}<select value={value.titlePosition ?? 'left'} onChange={event => update({ titlePosition: event.currentTarget.value as 'left' | 'center' | 'right' })}>
          <option value="left">{t.left}</option><option value="center">{t.center}</option><option value="right">{t.right}</option>
        </select></label>
        <HeaderTextStyleEditor label={t.projectTitle} value={value.titleStyle ?? {}} onChange={titleStyle => update({ titleStyle })} locale={locale} defaultSize={18}/>
        <label className="runtime-header-settings__inline"><input type="checkbox" checked={value.showScreenName === true} onChange={event => update({ showScreenName: event.currentTarget.checked })}/>
          <span><strong>{t.showScreenName}</strong><small>{t.showScreenNameHint}</small></span>
        </label>
        {value.showScreenName ? <HeaderTextStyleEditor label={t.screenName} value={value.screenNameStyle ?? {}} onChange={screenNameStyle => update({ screenNameStyle })} locale={locale} defaultSize={12}/> : null}
        <div className="runtime-header-settings__two-column">
          <label>{t.controlsPosition}<select value={value.controlsPosition ?? 'right'} onChange={event => update({ controlsPosition: event.currentTarget.value as 'left' | 'right' })}>
            <option value="left">{t.left}</option><option value="right">{t.right}</option>
          </select></label>
          <label>{t.controlsOrder}<input type="number" min={0} max={20} value={value.controlsOrder ?? 2} onChange={event => update({ controlsOrder: Number(event.currentTarget.value) })}/></label>
        </div>
        <small>{t.controlsPositionHint}</small>
      </fieldset>

      <fieldset className="eng-panel runtime-header-settings__panel">
        <legend>{t.dateTime}</legend>
        <label>{t.display}<select value={dateTime.mode ?? 'off'} onChange={event => update({ dateTime: { ...dateTime, mode: event.currentTarget.value as NonNullable<typeof dateTime.mode> } })}>
          <option value="off">{t.off}</option><option value="time">{t.timeOnly}</option><option value="date">{t.dateOnly}</option><option value="dateTime">{t.dateAndTime}</option>
        </select></label>
        {dateTime.mode && dateTime.mode !== 'off' ? <>
          <div className="runtime-header-settings__two-column">
            <label>{t.position}<select value={dateTime.position ?? 'right'} onChange={event => update({ dateTime: { ...dateTime, position: event.currentTarget.value as 'left' | 'right' } })}>
              <option value="left">{t.left}</option><option value="right">{t.right}</option>
            </select></label>
            <label>{t.order}<input type="number" min={0} max={20} value={dateTime.order ?? 1} onChange={event => update({ dateTime: { ...dateTime, order: Number(event.currentTarget.value) } })}/></label>
          </div>
          {dateTime.mode !== 'time' ? <label>{t.dateFormat}<select value={dateTime.dateFormat ?? 'dd/MM/yyyy'} onChange={event => update({ dateTime: { ...dateTime, dateFormat: event.currentTarget.value as NonNullable<typeof dateTime.dateFormat> } })}>
            <option value="dd/MM/yyyy">{t.dayFirst} · 31/12/2026</option><option value="MM/dd/yyyy">{t.monthFirst} · 12/31/2026</option><option value="yyyy-MM-dd">{t.yearFirst} · 2026-12-31</option>
          </select></label> : null}
          {dateTime.mode !== 'date' ? <label>{t.timeFormat}<select value={dateTime.timeFormat ?? '24h'} onChange={event => update({ dateTime: { ...dateTime, timeFormat: event.currentTarget.value as '24h' | '12h' } })}>
            <option value="24h">24 h</option><option value="12h">12 h</option>
          </select></label> : null}
          <small>{dateTime.mode === 'dateTime' ? t.dateTimeHint : t.dateTimeSingleHint}</small>
        </> : <small>{t.dateTimeOffHint}</small>}
      </fieldset>

      <fieldset className="eng-panel runtime-header-settings__panel">
        <legend>{t.visibleActions}</legend>
        <div className="runtime-header-settings__checks">
          {(['overviewVisible', 'historyVisible', 'alarmsVisible', 'playbackVisible'] as const).map((key, index) => <label key={key}>
            <input type="checkbox" checked={value[key] !== false} onChange={event => update({ [key]: event.currentTarget.checked })}/>{t.actions[index]}
          </label>)}
        </div>
        <small>{t.actionsHint}</small>
        <h3>{t.customLinks}</h3>
        {links.map((link, index) => <div key={index} className="runtime-header-settings__link">
          <label>{t.buttonLabel}<input maxLength={128} value={link.label} onChange={event => update({ links: links.map((item, i) => i === index ? { ...item, label: event.currentTarget.value } : item) })}/></label>
          <label>{t.targetScreen}<select value={link.screenKey} onChange={event => update({ links: links.map((item, i) => i === index ? { ...item, screenKey: event.currentTarget.value } : item) })}>
            {(snapshot.package.screens ?? []).map(screen => <option key={screen.key} value={screen.key}>{screen.name || screen.key}</option>)}
          </select></label>
          <label>{t.buttonImage}<select value={link.visualAssetId ?? ''} onChange={event => update({ links: links.map((item, i) => i === index ? { ...item, visualAssetId: event.currentTarget.value || null } : item) })}>
            <option value="">{t.textOnly}</option>{listUserVisualAssets(snapshot.package.visualAssets).filter(asset => asset.id && asset.mediaType.startsWith('image/')).map(asset => <option key={asset.id!} value={asset.id!}>{asset.name}</option>)}
          </select></label>
          <button type="button" onClick={() => update({ links: links.filter((_, i) => i !== index) })}>{t.remove}</button>
        </div>)}
        <button type="button" disabled={links.length >= 16 || !snapshot.package.screens?.length} onClick={() => update({ links: [...links, { label: t.screenButton, screenKey: snapshot.package.screens![0].key }] })}>{t.addLink}</button>
        <small>{t.userControlHint}</small>
      </fieldset>
    </div>
  </section>;
}

function HeaderTextStyleEditor({ label, value, onChange, locale, defaultSize }: {
  label: string; value: RuntimeHeaderTextStyleEngineering; onChange: (value: RuntimeHeaderTextStyleEngineering) => void;
  locale: EngineeringLocale; defaultSize: number;
}) {
  const t = headerSettingsText(locale);
  const patch = (valuePatch: Partial<RuntimeHeaderTextStyleEngineering>) => onChange({ ...value, ...valuePatch });
  return <fieldset className="runtime-header-settings__text-style">
    <legend>{label}</legend>
    <label>{t.fontFamily}<select value={value.fontFamily ?? 'system-ui'} onChange={event => patch({ fontFamily: event.currentTarget.value })}>
      <option value="system-ui">{t.systemFont}</option><option value="Arial, sans-serif">Arial</option><option value="Verdana, sans-serif">Verdana</option><option value="Georgia, serif">Georgia</option><option value="monospace">{t.monospaceFont}</option>
    </select></label>
    <div className="runtime-header-settings__two-column">
      <label>{t.fontSize}<input type="number" min={8} max={48} value={value.fontSize ?? defaultSize} onChange={event => patch({ fontSize: Number(event.currentTarget.value) })}/></label>
      <label>{t.weight}<select value={value.fontWeight ?? 600} onChange={event => patch({ fontWeight: event.currentTarget.value as RuntimeHeaderTextStyleEngineering['fontWeight'] })}>
        <option value="normal">{t.regular}</option><option value={500}>500</option><option value={600}>600</option><option value={700}>700</option><option value="bold">{t.bold}</option>
      </select></label>
    </div>
    <label className="runtime-header-settings__inline"><input type="checkbox" checked={!value.color} onChange={event => patch({ color: event.currentTarget.checked ? null : '#1f2937' })}/>
      <span><strong>{t.followThemeText}</strong><small>{t.followThemeTextHint}</small></span>
    </label>
    {value.color ? <label>{t.textColor}<input type="color" value={value.color.slice(0, 7)} onChange={event => patch({ color: event.currentTarget.value })}/></label> : null}
  </fieldset>;
}

function headerSettingsText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Runtime presentation', title: 'Runtime header', description: 'Configure the operator bar independently from the application logo and brand.', enabled: 'Show Runtime header',
    appearance: 'Bar appearance', height: 'Header height', heightHint: 'From the current compact height up to three times taller.', followTheme: 'Use the active light/dark theme', followThemeHint: 'The bar and its controls follow the operator’s selected theme.', backgroundColor: 'Custom bar color',
    layout: 'Project and screen titles', projectAlignment: 'Project title alignment', left: 'Left', center: 'Center', right: 'Right', projectTitle: 'Project title typography', showScreenName: 'Show the current screen name below the project title', showScreenNameHint: 'The screen display name updates as Runtime navigation changes screens.', screenName: 'Screen name typography', controlsPosition: 'Navigation and operator buttons', controlsOrder: 'Order', controlsPositionHint: 'Moves the Overview, History, Alarms, Playback, full-screen and custom screen buttons as a group. The user menu stays at the far right.',
    dateTime: 'Clock and date', display: 'Display', off: 'Hidden', timeOnly: 'Time only', dateOnly: 'Date only', dateAndTime: 'Time above date', position: 'Side', order: 'Order within side', dateFormat: 'Date format', dayFirst: 'Day / month / year', monthFirst: 'Month / day / year', yearFirst: 'Year / month / day', timeFormat: 'Time format', dateTimeHint: 'When both are shown, time is placed above the date.', dateTimeSingleHint: 'Shown on the selected side of the header.', dateTimeOffHint: 'The header will not reserve space for a clock.',
    visibleActions: 'Available Runtime buttons', actions: ['Overview', 'History browser', 'Alarms', 'Historical playback'], actionsHint: 'The Runtime user menu is fixed at the far right and is not moved with these buttons.', customLinks: 'Custom screen buttons', buttonLabel: 'Button label', targetScreen: 'Destination screen', buttonImage: 'Button icon', textOnly: 'Text only', remove: 'Remove', screenButton: 'Screen', addLink: 'Add screen button', userControlHint: 'User summary and session controls remain fixed on the right in the compact/full-screen Runtime header.',
    fontFamily: 'Font family', systemFont: 'System default', monospaceFont: 'Monospace', fontSize: 'Font size', weight: 'Weight', regular: 'Regular', bold: 'Bold', followThemeText: 'Use theme text color', followThemeTextHint: 'Text color changes with the active light/dark theme.', textColor: 'Text color'
  };
  if (locale === 'es') return {
    eyebrow: 'Presentación de Runtime', title: 'Encabezado de Runtime', description: 'Configure la barra del operador separada del logotipo e identidad de la aplicación.', enabled: 'Mostrar encabezado de Runtime',
    appearance: 'Apariencia de la barra', height: 'Altura del encabezado', heightHint: 'Desde la altura compacta actual hasta tres veces más alta.', followTheme: 'Usar el tema claro/oscuro activo', followThemeHint: 'La barra y sus controles siguen el tema seleccionado por el operador.', backgroundColor: 'Color personalizado de la barra',
    layout: 'Títulos del proyecto y pantalla', projectAlignment: 'Alineación del título del proyecto', left: 'Izquierda', center: 'Centro', right: 'Derecha', projectTitle: 'Tipografía del proyecto', showScreenName: 'Mostrar el nombre de la pantalla bajo el proyecto', showScreenNameHint: 'El nombre cambia al navegar entre pantallas.', screenName: 'Tipografía de la pantalla', controlsPosition: 'Botones de navegación y operación', controlsOrder: 'Orden', controlsPositionHint: 'Mueve juntos los botones Overview, Historial, Alarmas, Playback, pantalla completa y pantallas personalizadas. El menú de usuario permanece a la derecha.',
    dateTime: 'Fecha y hora', display: 'Mostrar', off: 'Ocultar', timeOnly: 'Solo hora', dateOnly: 'Solo fecha', dateAndTime: 'Hora sobre fecha', position: 'Lado', order: 'Orden en el lado', dateFormat: 'Formato de fecha', dayFirst: 'Día / mes / año', monthFirst: 'Mes / día / año', yearFirst: 'Año / mes / día', timeFormat: 'Formato de hora', dateTimeHint: 'Al mostrar ambas, la hora aparece encima de la fecha.', dateTimeSingleHint: 'Aparece en el lado seleccionado del encabezado.', dateTimeOffHint: 'El encabezado no reservará espacio para el reloj.',
    visibleActions: 'Botones de Runtime disponibles', actions: ['Overview', 'Historial', 'Alarmas', 'Playback histórico'], actionsHint: 'El menú de usuario permanece fijo a la derecha.', customLinks: 'Botones personalizados de pantalla', buttonLabel: 'Etiqueta del botón', targetScreen: 'Pantalla de destino', buttonImage: 'Icono del botón', textOnly: 'Solo texto', remove: 'Eliminar', screenButton: 'Pantalla', addLink: 'Agregar botón de pantalla', userControlHint: 'El resumen de usuario permanece fijo a la derecha en el encabezado compacto/a pantalla completa.',
    fontFamily: 'Familia tipográfica', systemFont: 'Predeterminada del sistema', monospaceFont: 'Monoespaciada', fontSize: 'Tamaño', weight: 'Peso', regular: 'Normal', bold: 'Negrita', followThemeText: 'Usar color de texto del tema', followThemeTextHint: 'El color sigue el tema claro/oscuro.', textColor: 'Color del texto'
  };
  return {
    eyebrow: 'Apresentação do Runtime', title: 'Cabeçalho do Runtime', description: 'Configure a barra do operador separadamente da marca e do logotipo do aplicativo.', enabled: 'Exibir cabeçalho do Runtime',
    appearance: 'Aparência da barra', height: 'Altura do cabeçalho', heightHint: 'Do tamanho compacto atual até três vezes mais alto.', followTheme: 'Usar o tema claro/escuro ativo', followThemeHint: 'A barra e seus controles acompanham o tema escolhido pelo operador.', backgroundColor: 'Cor personalizada da barra',
    layout: 'Títulos do projeto e da tela', projectAlignment: 'Alinhamento do título do projeto', left: 'Esquerda', center: 'Centro', right: 'Direita', projectTitle: 'Tipografia do título do projeto', showScreenName: 'Exibir o nome da tela abaixo do título do projeto', showScreenNameHint: 'O nome acompanha a tela aberta durante a navegação no Runtime.', screenName: 'Tipografia do nome da tela', controlsPosition: 'Posição dos botões de operação', controlsOrder: 'Ordem', controlsPositionHint: 'Move em conjunto Visão geral, Histórico, Alarmes, Playback, tela cheia e botões de telas personalizadas. O menu de usuário permanece fixo à direita.',
    dateTime: 'Data e hora', display: 'Exibição', off: 'Ocultar', timeOnly: 'Somente hora', dateOnly: 'Somente data', dateAndTime: 'Hora acima da data', position: 'Lado do cabeçalho', order: 'Ordem no lado', dateFormat: 'Formato da data', dayFirst: 'Dia / mês / ano', monthFirst: 'Mês / dia / ano', yearFirst: 'Ano / mês / dia', timeFormat: 'Formato da hora', dateTimeHint: 'Quando ambas aparecem, a hora fica acima da data.', dateTimeSingleHint: 'O item aparece no lado escolhido do cabeçalho.', dateTimeOffHint: 'O cabeçalho não reserva espaço para relógio.',
    visibleActions: 'Botões disponíveis no Runtime', actions: ['Visão geral', 'Browser de histórico', 'Alarmes', 'Playback histórico'], actionsHint: 'O botão de resumo do usuário permanece fixo no lado direito.', customLinks: 'Botões para telas personalizadas', buttonLabel: 'Texto do botão', targetScreen: 'Tela de destino', buttonImage: 'Ícone do botão', textOnly: 'Somente texto', remove: 'Remover', screenButton: 'Tela', addLink: 'Adicionar botão de tela', userControlHint: 'O resumo do usuário e os controles da sessão ficam fixos à direita no cabeçalho compacto/tela cheia.',
    fontFamily: 'Família da fonte', systemFont: 'Padrão do sistema', monospaceFont: 'Monoespaçada', fontSize: 'Tamanho da fonte', weight: 'Peso', regular: 'Normal', bold: 'Negrito', followThemeText: 'Usar cor de texto do tema', followThemeTextHint: 'A cor do texto acompanha o tema claro/escuro ativo.', textColor: 'Cor do texto'
  };
}
