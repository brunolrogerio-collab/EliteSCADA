import React from 'react';
import type { EngineeringSnapshot, RuntimeHeaderEngineering } from './types';
import type { EngineeringLocale } from './i18n';

export function RuntimeHeaderSettings({ value, onChange, snapshot, locale }: {
  value: RuntimeHeaderEngineering; onChange: (value: RuntimeHeaderEngineering) => void;
  snapshot: EngineeringSnapshot; locale: EngineeringLocale;
}) {
  const pt = locale === 'pt-BR';
  const label = (ptText: string, en: string, es: string) => pt ? ptText : locale === 'es' ? es : en;
  const update = (patch: Partial<RuntimeHeaderEngineering>) => onChange({ ...value, ...patch });
  const links = value.links ?? [];
  return <fieldset className="eng-panel" data-testid="runtime-header-settings">
    <legend>{label('Cabeçalho do Runtime', 'Runtime header', 'Encabezado de Runtime')}</legend>
    <label><input type="checkbox" checked={value.enabled !== false} onChange={e => update({ enabled: e.target.checked })}/>{label('Mostrar cabeçalho', 'Show header', 'Mostrar encabezado')}</label>
    <label>{label('Altura', 'Height', 'Altura')}<input type="number" min={32} max={160} value={value.height ?? 56} onChange={e => update({ height: Number(e.target.value) })}/></label>
    <label>{label('Fundo pelo tema', 'Theme background', 'Fondo del tema')}<input type="checkbox" checked={!value.backgroundColor} onChange={e => update({ backgroundColor: e.target.checked ? null : '#243746' })}/></label>
    {value.backgroundColor && <input aria-label={label('Cor do cabeçalho', 'Header color', 'Color del encabezado')} type="color" value={value.backgroundColor.slice(0, 7)} onChange={e => update({ backgroundColor: e.target.value })}/>}
    <label>{label('Posição do título', 'Title position', 'Posición del título')}<select value={value.titlePosition ?? 'left'} onChange={e => update({ titlePosition: e.target.value as 'left' | 'center' | 'right' })}>
      <option value="left">{label('Esquerda', 'Left', 'Izquierda')}</option><option value="center">{label('Centro', 'Center', 'Centro')}</option><option value="right">{label('Direita', 'Right', 'Derecha')}</option>
    </select></label>
    {(['overviewVisible', 'historyVisible', 'alarmsVisible', 'playbackVisible'] as const).map((key, i) => <label key={key}><input type="checkbox" checked={value[key] !== false} onChange={e => update({ [key]: e.target.checked })}/>{[label('Visão geral', 'Overview', 'Vista general'), label('Histórico', 'History', 'Histórico'), label('Alarmes', 'Alarms', 'Alarmas'), 'Playback'][i]}</label>)}
    <h3>{label('Botões de navegação', 'Navigation buttons', 'Botones de navegación')}</h3>
    {links.map((link, index) => <div key={index} className="eng-panel">
      <input aria-label={label('Texto do botão', 'Button label', 'Texto del botón')} maxLength={128} value={link.label} onChange={e => update({ links: links.map((item, i) => i === index ? { ...item, label: e.target.value } : item) })}/>
      <select aria-label={label('Tela de destino', 'Target screen', 'Pantalla de destino')} value={link.screenKey} onChange={e => update({ links: links.map((item, i) => i === index ? { ...item, screenKey: e.target.value } : item) })}>
        {(snapshot.package.screens ?? []).map(screen => <option key={screen.key} value={screen.key}>{screen.name || screen.key}</option>)}
      </select>
      <select aria-label={label('Imagem do botão', 'Button image', 'Imagen del botón')} value={link.visualAssetId ?? ''} onChange={e => update({ links: links.map((item, i) => i === index ? { ...item, visualAssetId: e.target.value || null } : item) })}>
        <option value="">{label('Somente texto', 'Text only', 'Solo texto')}</option>{(snapshot.package.visualAssets ?? []).filter(asset => asset.id && asset.mediaType.startsWith('image/')).map(asset => <option key={asset.id!} value={asset.id!}>{asset.name}</option>)}
      </select>
      <button type="button" onClick={() => update({ links: links.filter((_, i) => i !== index) })}>{label('Remover', 'Remove', 'Eliminar')}</button>
    </div>)}
    <button type="button" disabled={links.length >= 16 || !snapshot.package.screens?.length} onClick={() => update({ links: [...links, { label: label('Tela', 'Screen', 'Pantalla'), screenKey: snapshot.package.screens![0].key }] })}>{label('Adicionar botão', 'Add button', 'Agregar botón')}</button>
    <p>{label('Com o cabeçalho desligado, o menu do usuário aparece no canto superior direito ao aproximar o cursor, tocar ou navegar pelo teclado.', 'When hidden, the user menu appears at the top right on hover, touch or keyboard focus.', 'Cuando está oculto, el menú de usuario aparece arriba a la derecha al acercar el cursor, tocar o usar el teclado.')}</p>
  </fieldset>;
}
