import React, { useEffect, useMemo, useState } from 'react';
import { applyEngineeringPackage, previewEngineeringPackage } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot, RuntimeStationProfile } from './types';

export function RuntimeStationEngineeringWorkspace({ snapshot, onApplied, locale = 'pt-BR' }: {
  snapshot: EngineeringSnapshot;
  onApplied: () => Promise<void>;
  locale?: EngineeringLocale;
}) {
  const [profiles, setProfiles] = useState<RuntimeStationProfile[]>([...(snapshot.package.runtimePresentation?.stationProfiles ?? [])]);
  const [preview, setPreview] = useState<{ canApply: boolean } | null>(null);
  const [previewSignature, setPreviewSignature] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const screens = snapshot.package.screens ?? [];
  const text = locale === 'en'
    ? { title: 'Runtime stations', description: 'Create a reusable set of independent Runtime windows for extended desktops.', add: 'Add station', name: 'Station name', screen: 'Screen', mode: 'Window behavior', navigation: 'Navigation', fixed: 'Fixed screen', addWindow: 'Add window', remove: 'Remove', validate: 'Validate', apply: 'Apply to Working', valid: 'Configuration is valid.', invalid: 'Review the configuration errors.', applied: 'Station profile saved to Working. Publish and activate to use it.', limit: 'A station can have from 1 to 4 windows.' }
    : locale === 'es'
      ? { title: 'Estaciones Runtime', description: 'Cree un conjunto reutilizable de ventanas Runtime independientes para escritorios extendidos.', add: 'Añadir estación', name: 'Nombre de la estación', screen: 'Pantalla', mode: 'Comportamiento', navigation: 'Navegación', fixed: 'Pantalla fija', addWindow: 'Añadir ventana', remove: 'Eliminar', validate: 'Validar', apply: 'Aplicar a Working', valid: 'La configuración es válida.', invalid: 'Revise los errores de configuración.', applied: 'Perfil guardado en Working. Publique y active para usarlo.', limit: 'Una estación admite de 1 a 4 ventanas.' }
      : { title: 'Estações do Runtime', description: 'Monte um conjunto reutilizável de janelas independentes para estações com vários monitores.', add: 'Adicionar estação', name: 'Nome da estação', screen: 'Tela', mode: 'Comportamento da janela', navigation: 'Navegação', fixed: 'Tela fixa', addWindow: 'Adicionar janela', remove: 'Remover', validate: 'Validar', apply: 'Aplicar ao Working', valid: 'Configuração válida.', invalid: 'Revise os erros da configuração.', applied: 'Perfil salvo no Working. Publique e ative para utilizá-lo.', limit: 'Uma estação pode ter de 1 a 4 janelas.' };
  const signature = useMemo(() => JSON.stringify({ profiles, changeVersion: snapshot.workspace.changeVersion }), [profiles, snapshot.workspace.changeVersion]);

  useEffect(() => {
    setProfiles([...(snapshot.package.runtimePresentation?.stationProfiles ?? [])]);
    setPreview(null); setPreviewSignature(''); setMessage('');
  }, [snapshot.workspace.changeVersion, snapshot.package.runtimePresentation]);

  const candidate = () => ({ ...snapshot.package, runtimePresentation: {
    ...(snapshot.package.runtimePresentation ?? { historicalPlaybackEnabled: false, version: 1 }),
    stationProfiles: profiles, version: 1
  }});

  const update = (next: RuntimeStationProfile[]) => { setProfiles(next); setPreview(null); setPreviewSignature(''); setMessage(''); };
  const addProfile = () => update([...profiles, { id: crypto.randomUUID(), name: `${text.title} ${profiles.length + 1}`, windows: [{ screenKey: screens[0]?.key ?? '', mode: 'navigation' }] }]);
  const validate = async () => {
    setBusy(true); setMessage('');
    try {
      const result = await previewEngineeringPackage(candidate());
      const structurallyValid = profiles.every(profile => profile.name.trim() && profile.windows.length >= 1 && profile.windows.length <= 4 && profile.windows.every(window => screens.some(screen => screen.key === window.screenKey)));
      setPreview({ canApply: result.canApply && structurallyValid }); setPreviewSignature(signature);
      setMessage(result.canApply && structurallyValid ? text.valid : text.invalid);
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };
  const apply = async () => {
    if (!preview?.canApply || previewSignature !== signature) return;
    setBusy(true); setMessage('');
    try {
      const result = await applyEngineeringPackage(candidate(), snapshot.workspace.changeVersion);
      const issue = result.issues.find(item => item.isError);
      if (issue) throw new Error(`${issue.code}: ${issue.message}`);
      setMessage(text.applied); await onApplied();
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  return <section className="eng-section" data-testid="runtime-station-editor">
    <header className="eng-section-header"><div><span className="eng-eyebrow">Runtime</span><h1>{text.title}</h1><p>{text.description}</p></div>
      <button type="button" onClick={addProfile} disabled={busy || !screens.length}>{text.add}</button></header>
    {!screens.length ? <div className="eng-panel"><p>Cadastre ao menos uma tela antes de configurar uma estação.</p></div> : null}
    {profiles.map((profile, profileIndex) => <section className="eng-panel" key={profile.id} data-testid="runtime-station-profile">
      <label className="eng-editor-field"><span>{text.name}</span><input value={profile.name} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, name: event.currentTarget.value } : item))}/></label>
      {profile.windows.map((windowProfile, windowIndex) => <div className="eng-editor-field" key={`${profile.id}-${windowIndex}`}>
        <span>{text.screen} {windowIndex + 1}</span>
        <div style={{ display: 'flex', gap: 8 }}>
          <select aria-label={`${text.screen} ${windowIndex + 1}`} value={windowProfile.screenKey} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.map((window, slot) => slot === windowIndex ? { ...window, screenKey: event.currentTarget.value } : window) } : item))}>
            {screens.map(screen => <option key={screen.key} value={screen.key}>{screen.name || screen.key}</option>)}
          </select>
          <select aria-label={`${text.mode} ${windowIndex + 1}`} value={windowProfile.mode} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.map((window, slot) => slot === windowIndex ? { ...window, mode: event.currentTarget.value as 'navigation' | 'fixed' } : window) } : item))}>
            <option value="navigation">{text.navigation}</option><option value="fixed">{text.fixed}</option>
          </select>
          {profile.windows.length > 1 ? <button type="button" onClick={() => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.filter((_, slot) => slot !== windowIndex) } : item))}>{text.remove}</button> : null}
        </div>
      </div>)}
      {profile.windows.length < 4 ? <button type="button" onClick={() => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: [...item.windows, { screenKey: screens[0].key, mode: 'fixed' }] } : item))}>{text.addWindow}</button> : null}
      <button type="button" onClick={() => update(profiles.filter((_, index) => index !== profileIndex))}>{text.remove}</button>
    </section>)}
    {profiles.length ? <div className="branding-editor__actions"><button type="button" disabled={busy} onClick={() => void validate()}>{text.validate}</button><button type="button" disabled={busy || !preview?.canApply || previewSignature !== signature} onClick={() => void apply()}>{text.apply}</button></div> : null}
    {profiles.some(profile => profile.windows.length < 1 || profile.windows.length > 4) ? <p role="note">{text.limit}</p> : null}
    {message ? <p role="status">{message}</p> : null}
  </section>;
}
