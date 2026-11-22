import React, { useEffect, useMemo, useRef, useState } from 'react';
import { applyEngineeringPackage, previewEngineeringPackage } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot, RuntimeStationProfile } from './types';

export function RuntimeStationEngineeringWorkspace({ snapshot, onApplied, onPendingChangesChange, locale = 'pt-BR' }: {
  snapshot: EngineeringSnapshot;
  onApplied: () => Promise<void>;
  onPendingChangesChange?: (pending: boolean) => void;
  locale?: EngineeringLocale;
}) {
  const incomingProfiles = snapshot.package.runtimePresentation?.stationProfiles ?? [];
  const savedProfilesSignature = useRef(JSON.stringify(incomingProfiles));
  const [profiles, setProfiles] = useState<RuntimeStationProfile[]>(() => [...incomingProfiles]);
  const [preview, setPreview] = useState<{ canApply: boolean } | null>(null);
  const [previewSignature, setPreviewSignature] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [stationConflict, setStationConflict] = useState(false);
  const screens = snapshot.package.screens ?? [];
  const text = locale === 'en'
    ? { title: 'Runtime stations', description: 'Create a reusable set of independent Runtime windows for extended desktops.', add: 'Add station', name: 'Station name', screen: 'Screen', mode: 'Window behavior', navigation: 'Navigation', fixed: 'Fixed screen', addWindow: 'Add window', remove: 'Remove', validate: 'Validate', apply: 'Apply to Working', valid: 'Configuration is valid.', invalid: 'Review the configuration errors.', applied: 'Station profile saved to Working. Save a revision, publish and activate it to use this configuration in Runtime.', limit: 'A station can have from 1 to 4 windows.', lifecycle: 'Working is the editable draft. Apply here, then save a revision, publish it and activate it in Engineering Overview. Runtime uses the Active revision, not Working.', draft: 'Unapplied station changes. Leaving this page discards them.', conflict: 'Another Working snapshot changed the saved station profiles while you were editing. Your draft was preserved; reload the saved profiles before applying, to avoid overwriting another edit.', reload: 'Discard local draft and reload Working', window: 'Window', windowHint: 'Each row launches an independent window. The number is not a physical monitor number.', count: 'windows' }
    : locale === 'es'
      ? { title: 'Estaciones Runtime', description: 'Cree un conjunto reutilizable de ventanas Runtime independientes para escritorios extendidos.', add: 'Añadir estación', name: 'Nombre de la estación', screen: 'Pantalla', mode: 'Comportamiento', navigation: 'Navegación', fixed: 'Pantalla fija', addWindow: 'Añadir ventana', remove: 'Eliminar', validate: 'Validar', apply: 'Aplicar a Working', valid: 'La configuración es válida.', invalid: 'Revise los errores de configuración.', applied: 'Perfil guardado en Working. Guarde una revisión, publíquela y actívela para usar esta configuración en Runtime.', limit: 'Una estación admite de 1 a 4 ventanas.', lifecycle: 'Working es el borrador editable. Aplique aquí, después guarde la revisión, publíquela y actívela en el Resumen de Ingeniería. Runtime utiliza la revisión Active, no Working.', draft: 'Hay cambios de estación sin aplicar. Si sale, se descartarán.', conflict: 'Otro snapshot de Working cambió los perfiles guardados mientras editaba. Se conservó su borrador; vuelva a cargar los perfiles antes de aplicarlos para no sobrescribir otra edición.', reload: 'Descartar borrador local y recargar Working', window: 'Ventana', windowHint: 'Cada fila abre una ventana independiente. El número no identifica un monitor físico.', count: 'ventanas' }
      : { title: 'Estações do Runtime', description: 'Monte um conjunto reutilizável de janelas independentes para estações com vários monitores.', add: 'Adicionar estação', name: 'Nome da estação', screen: 'Tela', mode: 'Comportamento da janela', navigation: 'Navegação', fixed: 'Tela fixa', addWindow: 'Adicionar janela', remove: 'Remover', validate: 'Validar', apply: 'Aplicar ao Working', valid: 'Configuração válida.', invalid: 'Revise os erros da configuração.', applied: 'Perfil salvo no Working. Salve uma revisão, publique e ative para utilizar esta configuração no Runtime.', limit: 'Uma estação pode ter de 1 a 4 janelas.', lifecycle: 'Working é o rascunho editável. Aplique aqui e depois, na Visão geral da Engenharia, salve uma revisão, publique e ative. O Runtime usa a revisão Active, não o Working.', draft: 'Há alterações da estação não aplicadas. Sair desta página as descarta.', conflict: 'Outro snapshot do Working alterou os perfis salvos enquanto você editava. Seu rascunho foi preservado; recarregue os perfis antes de aplicar para não sobrescrever outra edição.', reload: 'Descartar rascunho local e recarregar Working', window: 'Janela', windowHint: 'Cada linha abre uma janela independente. O número não identifica um monitor físico.', count: 'janelas' };
  const hasPendingChanges = JSON.stringify(profiles) !== savedProfilesSignature.current;
  const signature = useMemo(() => JSON.stringify({ profiles, changeVersion: snapshot.workspace.changeVersion }), [profiles, snapshot.workspace.changeVersion]);

  useEffect(() => {
    // An unrelated Working refresh must not erase a draft. If the saved station
    // profiles also changed, fail closed rather than silently replacing either edit.
    const nextSignature = JSON.stringify(snapshot.package.runtimePresentation?.stationProfiles ?? []);
    if (JSON.stringify(profiles) !== savedProfilesSignature.current) {
      setStationConflict(nextSignature !== savedProfilesSignature.current);
    } else {
      savedProfilesSignature.current = nextSignature;
      setProfiles([...(snapshot.package.runtimePresentation?.stationProfiles ?? [])]);
      setStationConflict(false);
    }
    setPreview(null);
    setPreviewSignature('');
    setMessage(previous => previous === text.applied ? previous : '');
    // The snapshot version or presentation changed, not the local form.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [snapshot.workspace.changeVersion, snapshot.package.runtimePresentation]);

  useEffect(() => {
    onPendingChangesChange?.(hasPendingChanges);
    return () => onPendingChangesChange?.(false);
  }, [hasPendingChanges, onPendingChangesChange]);

  useEffect(() => {
    if (!hasPendingChanges) return;
    const confirmDeparture = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ''; };
    window.addEventListener('beforeunload', confirmDeparture);
    return () => window.removeEventListener('beforeunload', confirmDeparture);
  }, [hasPendingChanges]);

  const candidate = () => ({ ...snapshot.package, runtimePresentation: {
    ...(snapshot.package.runtimePresentation ?? { historicalPlaybackEnabled: false, version: 1 }),
    stationProfiles: profiles, version: 1
  } });

  const update = (next: RuntimeStationProfile[]) => {
    setProfiles(next);
    setPreview(null); setPreviewSignature(''); setMessage('');
  };
  const resetFromWorking = () => {
    const saved = snapshot.package.runtimePresentation?.stationProfiles ?? [];
    savedProfilesSignature.current = JSON.stringify(saved);
    setProfiles([...saved]);
    setStationConflict(false);
    setPreview(null); setPreviewSignature(''); setMessage('');
  };
  const addProfile = () => update([...profiles, { id: crypto.randomUUID(), name: `${text.title} ${profiles.length + 1}`, windows: [{ screenKey: screens[0]?.key ?? '', mode: 'navigation' }] }]);
  const validate = async () => {
    if (stationConflict) return;
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
    if (stationConflict || !preview?.canApply || previewSignature !== signature) return;
    setBusy(true); setMessage('');
    try {
      const result = await applyEngineeringPackage(candidate(), snapshot.workspace.changeVersion);
      const issue = result.issues.find(item => item.isError);
      if (issue) throw new Error(`${issue.code}: ${issue.message}`);
      savedProfilesSignature.current = JSON.stringify(profiles);
      setStationConflict(false);
      setMessage(text.applied);
      await onApplied();
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  return <section className="eng-section" data-testid="runtime-station-editor">
    <header className="eng-section-header"><div><span className="eng-eyebrow">Runtime</span><h1>{text.title}</h1><p>{text.description}</p></div>
      <button type="button" onClick={addProfile} disabled={busy || stationConflict || !screens.length}>{text.add}</button></header>
    <p data-testid="runtime-station-lifecycle-guidance">{text.lifecycle}</p>
    {!screens.length ? <div className="eng-panel"><p>{locale === 'en' ? 'Create at least one screen before configuring a station.' : locale === 'es' ? 'Cree al menos una pantalla antes de configurar una estación.' : 'Cadastre ao menos uma tela antes de configurar uma estação.'}</p></div> : null}
    {stationConflict ? <div role="alert"><p>{text.conflict}</p><button type="button" onClick={resetFromWorking} disabled={busy}>{text.reload}</button></div> : null}
    {hasPendingChanges && !stationConflict ? <p role="note" data-testid="runtime-station-draft-warning">{text.draft}</p> : null}
    {profiles.map((profile, profileIndex) => <section className="eng-panel" key={profile.id} data-testid="runtime-station-profile">
      <label className="eng-editor-field"><span>{text.name}</span><input value={profile.name} disabled={busy || stationConflict} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, name: event.currentTarget.value } : item))}/></label>
      <p>{profile.windows.length} {text.count}. {text.windowHint}</p>
      {profile.windows.map((windowProfile, windowIndex) => <div className="eng-editor-field" key={`${profile.id}-${windowIndex}`}>
        <strong>{text.window} {windowIndex + 1}</strong>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          <select aria-label={`${text.screen} ${windowIndex + 1}`} disabled={busy || stationConflict} value={windowProfile.screenKey} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.map((window, slot) => slot === windowIndex ? { ...window, screenKey: event.currentTarget.value } : window) } : item))}>
            {screens.map(screen => <option key={screen.key} value={screen.key}>{screen.name || screen.key}</option>)}
          </select>
          <select aria-label={`${text.mode} ${windowIndex + 1}`} disabled={busy || stationConflict} value={windowProfile.mode} onChange={event => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.map((window, slot) => slot === windowIndex ? { ...window, mode: event.currentTarget.value as 'navigation' | 'fixed' } : window) } : item))}>
            <option value="navigation">{text.navigation}</option><option value="fixed">{text.fixed}</option>
          </select>
          {profile.windows.length > 1 ? <button type="button" disabled={busy || stationConflict} onClick={() => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: item.windows.filter((_, slot) => slot !== windowIndex) } : item))}>{text.remove}</button> : null}
        </div>
      </div>)}
      {profile.windows.length < 4 ? <button type="button" disabled={busy || stationConflict} onClick={() => update(profiles.map((item, index) => index === profileIndex ? { ...item, windows: [...item.windows, { screenKey: screens[0].key, mode: 'fixed' }] } : item))}>{text.addWindow}</button> : null}
      <button type="button" disabled={busy || stationConflict} onClick={() => update(profiles.filter((_, index) => index !== profileIndex))}>{text.remove}</button>
    </section>)}
    <div className="branding-editor__actions"><button type="button" disabled={busy || stationConflict} onClick={() => void validate()}>{text.validate}</button><button type="button" disabled={busy || stationConflict || !preview?.canApply || previewSignature !== signature} onClick={() => void apply()}>{text.apply}</button></div>
    {profiles.some(profile => profile.windows.length < 1 || profile.windows.length > 4) ? <p role="note">{text.limit}</p> : null}
    {message ? <p role="status">{message}</p> : null}
  </section>;
}
