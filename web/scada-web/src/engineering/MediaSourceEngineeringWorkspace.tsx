import React, { useEffect, useMemo, useState } from 'react';
import {
  configureMediaSourceCredential,
  createMediaSource,
  deleteMediaSource,
  deleteMediaSourceCredential,
  loadMediaSourceCredentialState,
  updateMediaSource,
  type MediaSourceCredentialState
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot, MediaSourceEngineering, MediaSourceProtocolEngineering } from './types';
import { EngineeringResourceOrganizer, engineeringCopyName, uniqueEngineeringKey } from './EngineeringResourceOrganizer';
import './structured-editors.css';
import './media-source-engineering.css';

const emptySource = (): MediaSourceEngineering => ({ key: '', name: '', protocol: 'http', endpoint: '', enabled: true });

export function MediaSourceEngineeringWorkspace({ snapshot, onApplied, locale = 'pt-BR' }: {
  snapshot: EngineeringSnapshot;
  onApplied: () => Promise<void>;
  locale?: EngineeringLocale;
}) {
  const sources = snapshot.package.mediaSources ?? [];
  const [selectedId, setSelectedId] = useState<string | null>(sources[0]?.id ?? null);
  const selected = sources.find(source => source.id === selectedId) ?? null;
  const [draft, setDraft] = useState<MediaSourceEngineering>(selected ?? emptySource());
  const [isNew, setIsNew] = useState(!selected);
  const [credentialStates, setCredentialStates] = useState<Record<string, MediaSourceCredentialState>>({});
  const [credentialMode, setCredentialMode] = useState<'basic' | 'bearer'>('basic');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [bearerToken, setBearerToken] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  const sourceIdentity = useMemo(() => sources.map(source => source.id).filter((id): id is string => Boolean(id)).join('|'), [sources]);
  useEffect(() => {
    let active = true;
    const ids = sources.map(source => source.id).filter((id): id is string => Boolean(id));
    void Promise.all(ids.map(async id => [id, await loadMediaSourceCredentialState(id)] as const))
      .then(entries => { if (active) setCredentialStates(Object.fromEntries(entries)); })
      .catch(error => { if (active) setMessage(error instanceof Error ? error.message : String(error)); });
    return () => { active = false; };
  // The identity string makes this request rerun only when the source set changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sourceIdentity]);

  useEffect(() => {
    if (isNew) return;
    const next = sources.find(source => source.id === selectedId);
    if (next) setDraft(next);
  }, [selectedId, sourceIdentity, isNew, sources]);

  const choose = (id: string | null) => {
    setSelectedId(id);
    setIsNew(id === null);
    setDraft(id === null ? emptySource() : sources.find(source => source.id === id) ?? emptySource());
    setMessage(null);
  };

  const updateDraft = (patch: Partial<MediaSourceEngineering>) => {
    setDraft(current => ({ ...current, ...patch }));
    setMessage(null);
  };

  const pasteSource = (source: MediaSourceEngineering) => {
    const hasUnsavedDraft = JSON.stringify(draft) !== JSON.stringify(selected ?? emptySource());
    if (hasUnsavedDraft && !window.confirm(copy(locale).discardDraftConfirm)) return;
    const key = uniqueEngineeringKey(source.key, sources.map(item => item.key));
    // Credentials live in the host vault, outside the project source model. A
    // copied source intentionally starts without credentials.
    setDraft({ ...source, id: undefined, key, name: engineeringCopyName(source.name || source.key, locale) });
    setSelectedId(null);
    setIsNew(true);
    setMessage(null);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setBusy(true); setMessage(null);
    try {
      if (isNew) {
        const created = await createMediaSource(draft, snapshot.workspace.changeVersion);
        await onApplied();
        setSelectedId(created.id ?? null);
        setIsNew(false);
        setDraft(created);
        setMessage(copy(locale).saved);
      } else {
        await updateMediaSource(draft, snapshot.workspace.changeVersion);
        await onApplied();
        setMessage(copy(locale).saved);
      }
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  const remove = async () => {
    if (!draft.id || !window.confirm(copy(locale).confirmDelete)) return;
    setBusy(true); setMessage(null);
    try {
      await deleteMediaSource(draft.id, snapshot.workspace.changeVersion);
      setCredentialStates(current => { const next = { ...current }; delete next[draft.id!]; return next; });
      await onApplied();
      choose(null);
      setMessage(copy(locale).deleted);
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  const saveCredential = async (id: string) => {
    setBusy(true); setMessage(null);
    try {
      const state = await configureMediaSourceCredential(id, credentialMode === 'basic'
        ? { username, password, bearerToken: null }
        : { username: null, password: null, bearerToken });
      setCredentialStates(current => ({ ...current, [id]: state }));
      setUsername(''); setPassword(''); setBearerToken('');
      setMessage(copy(locale).credentialSaved);
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  const removeCredential = async () => {
    if (!draft.id || !window.confirm(copy(locale).confirmCredentialDelete)) return;
    setBusy(true); setMessage(null);
    try {
      await deleteMediaSourceCredential(draft.id);
      setCredentialStates(current => ({ ...current, [draft.id!]: { configured: false } }));
      setMessage(copy(locale).credentialDeleted);
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  const text = copy(locale);
  const projectKey = snapshot.workspace.projectKey ?? snapshot.workspace.projectName ?? 'workspace';
  return <section className="eng-section media-source-workspace" data-testid="media-source-workspace">
    <header className="eng-section-header"><div><span className="eng-eyebrow">{text.eyebrow}</span><h1>{text.title}</h1><p>{text.description}</p></div>
    </header>
    <div className="eng-editor-layout">
      <aside className="eng-editor-picker" aria-label={text.sources}>
        <header>
          <div className="eng-editor-picker-title">
            <strong>{text.sources}</strong>
            <button type="button" className={isNew ? 'active' : ''} onClick={() => choose(null)} disabled={busy}>+ {text.newSource}</button>
          </div>
          <input type="search" aria-label={text.searchSources} placeholder={text.searchSources} value={query} onChange={event => setQuery(event.currentTarget.value)} />
        </header>
        <EngineeringResourceOrganizer
          projectKey={projectKey}
          kind="mediaSources"
          locale={locale}
          label={text.sources}
          resources={sources
            .filter(source => `${source.name} ${source.key} ${source.protocol} ${source.endpoint}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
            .map(source => ({ identity: `key:${source.key}`, name: source.name || source.key, details: `${source.key} · ${source.protocol.toUpperCase()} · ${source.enabled === false ? text.disabled : text.enabled}`, value: source }))}
          selectedIdentity={selected ? `key:${selected.key}` : null}
          onSelect={identity => {
            const source = sources.find(item => `key:${item.key}` === identity);
            if (source) choose(source.id ?? null);
          }}
          onPaste={pasteSource}
          emptyLabel={text.empty}
        />
      </aside>
      <form className="eng-editor-form-panel" onSubmit={event => void save(event)}>
        <fieldset disabled={busy}>
          <legend>{text.identity}</legend>
          <div className="eng-editor-form-grid">
            <label className="eng-editor-field"><span>{text.name}</span><input data-testid="media-source-name" required maxLength={128} value={draft.name} onChange={event => updateDraft({ name: event.currentTarget.value })}/></label>
            <label className="eng-editor-field"><span>{text.key}</span><input data-testid="media-source-key" required maxLength={128} pattern="[A-Za-z0-9._-]+" value={draft.key} onChange={event => updateDraft({ key: event.currentTarget.value })}/></label>
            <label className="eng-editor-field"><span>{text.protocol}</span><select data-testid="media-source-protocol" value={draft.protocol} onChange={event => updateDraft({ protocol: event.currentTarget.value as MediaSourceProtocolEngineering })}>
              <option value="http">HTTP / HTTPS</option><option value="hls">HLS</option><option value="mjpeg">MJPEG</option><option value="rtsp">RTSP / RTSPS</option>
            </select></label>
            <label className="eng-editor-field"><span>{text.endpoint}</span><input data-testid="media-source-endpoint" required maxLength={2048} value={draft.endpoint} onChange={event => updateDraft({ endpoint: event.currentTarget.value })} placeholder={draft.protocol === 'rtsp' ? 'rtsp://host:554/path' : 'https://host/path'}/></label>
            <label className="media-source-enabled"><input type="checkbox" checked={draft.enabled !== false} onChange={event => updateDraft({ enabled: event.currentTarget.checked })}/><span>{text.enabled}</span></label>
          </div>
          <small>{text.endpointHelp}</small>
          <div className="eng-editor-actions"><button type="submit">{isNew ? text.create : text.save}</button>
            {!isNew && <button type="button" onClick={() => void remove()}>{text.delete}</button>}</div>
        </fieldset>

        {!isNew && draft.id && <fieldset disabled={busy} data-testid="media-source-credentials">
          <legend>{text.credentials}</legend>
          <p>{credentialStates[draft.id]?.configured ? text.credentialConfigured : text.credentialMissing}</p>
          <label className="eng-editor-field"><span>{text.credentialMode}</span><select data-testid="media-source-credential-mode" value={credentialMode} onChange={event => setCredentialMode(event.currentTarget.value as 'basic' | 'bearer')}>
            <option value="basic">Basic authentication</option><option value="bearer">Bearer token</option>
          </select></label>
          {credentialMode === 'basic' ? <>
            <label className="eng-editor-field"><span>{text.username}</span><input data-testid="media-source-username" autoComplete="off" maxLength={256} value={username} onChange={event => setUsername(event.currentTarget.value)}/></label>
            <label className="eng-editor-field"><span>{text.password}</span><input data-testid="media-source-password" type="password" autoComplete="new-password" maxLength={8192} value={password} onChange={event => setPassword(event.currentTarget.value)}/></label>
          </> : <label className="eng-editor-field"><span>{text.bearerToken}</span><input data-testid="media-source-bearer" type="password" autoComplete="new-password" maxLength={8192} value={bearerToken} onChange={event => setBearerToken(event.currentTarget.value)}/></label>}
          <small>{text.credentialHelp}</small>
          <div className="eng-editor-actions"><button type="button" disabled={credentialMode === 'basic' ? !username || !password : !bearerToken} onClick={() => void saveCredential(draft.id!)}>{text.saveCredential}</button>
            {credentialStates[draft.id]?.configured && <button type="button" onClick={() => void removeCredential()}>{text.deleteCredential}</button>}</div>
        </fieldset>}
        {message && <p role="status" aria-live="polite">{message}</p>}
      </form>
    </div>
  </section>;
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Project media', title: 'Media sources', description: 'Configure HTTP, HLS, MJPEG and RTSP sources. Credentials are encrypted separately and never exported with the project.',
    newSource: 'New source', sources: 'Media sources', searchSources: 'Search media sources', empty: 'No media sources yet.', discardDraftConfirm: 'Discard the current new media source draft?', identity: 'Source identity', name: 'Display name', key: 'Stable key', protocol: 'Protocol', endpoint: 'Endpoint', endpointHelp: 'Use an absolute URL without username, password, query parameters or fragments. RTSP is configured here but still needs the server relay before browser playback.',
    enabled: 'Enabled', disabled: 'Disabled', create: 'Create source', save: 'Save changes', delete: 'Delete source', credentials: 'Protected credentials', credentialConfigured: 'A credential is stored in the protected host vault. Its value cannot be read back.', credentialMissing: 'No credential is configured for this host.', credentialMode: 'Authentication type', username: 'Username', password: 'Password', bearerToken: 'Bearer token', credentialHelp: 'Credentials are write-only, encrypted at rest, and excluded from project export. Provision them again on another host after importing the project.', saveCredential: 'Save protected credential', deleteCredential: 'Remove credential', confirmDelete: 'Delete this source and its protected credential?', confirmCredentialDelete: 'Remove the protected credential?', saved: 'Media source saved to Working. Save/publish the project separately to make it active.', deleted: 'Media source deleted from Working.', credentialSaved: 'Protected credential saved.', credentialDeleted: 'Protected credential removed.'
  };
  if (locale === 'es') return {
    eyebrow: 'Medios del proyecto', title: 'Fuentes multimedia', description: 'Configure fuentes HTTP, HLS, MJPEG y RTSP. Las credenciales se cifran por separado y nunca se exportan con el proyecto.',
    newSource: 'Nueva fuente', sources: 'Fuentes multimedia', searchSources: 'Buscar fuentes multimedia', empty: 'Todavía no hay fuentes multimedia.', discardDraftConfirm: '¿Descartar el borrador actual de fuente multimedia?', identity: 'Identidad de fuente', name: 'Nombre visible', key: 'Clave estable', protocol: 'Protocolo', endpoint: 'Dirección', endpointHelp: 'Use una URL absoluta sin usuario, contraseña, parámetros ni fragmentos. RTSP se configura aquí, pero requiere el relay del servidor para reproducirse en el navegador.',
    enabled: 'Habilitada', disabled: 'Deshabilitada', create: 'Crear fuente', save: 'Guardar cambios', delete: 'Eliminar fuente', credentials: 'Credenciales protegidas', credentialConfigured: 'Hay una credencial guardada en la bóveda protegida del host. No se puede volver a leer.', credentialMissing: 'No hay credencial configurada en este host.', credentialMode: 'Tipo de autenticación', username: 'Usuario', password: 'Contraseña', bearerToken: 'Token Bearer', credentialHelp: 'Las credenciales son de solo escritura, cifradas y excluidas de la exportación. Debe configurarlas de nuevo al importar en otro host.', saveCredential: 'Guardar credencial protegida', deleteCredential: 'Eliminar credencial', confirmDelete: '¿Eliminar esta fuente y su credencial protegida?', confirmCredentialDelete: '¿Eliminar la credencial protegida?', saved: 'Fuente guardada en Working. Guarde/publique el proyecto por separado para activarla.', deleted: 'Fuente eliminada de Working.', credentialSaved: 'Credencial protegida guardada.', credentialDeleted: 'Credencial protegida eliminada.'
  };
  return {
    eyebrow: 'Mídia do projeto', title: 'Fontes de mídia', description: 'Configure fontes HTTP, HLS, MJPEG e RTSP. As credenciais são criptografadas à parte e nunca são exportadas com o projeto.',
    newSource: 'Nova fonte', sources: 'Fontes de mídia', searchSources: 'Pesquisar fontes de mídia', empty: 'Ainda não há fontes de mídia.', discardDraftConfirm: 'Descartar o rascunho atual da nova fonte de mídia?', identity: 'Identidade da fonte', name: 'Nome de exibição', key: 'Chave estável', protocol: 'Protocolo', endpoint: 'Endereço', endpointHelp: 'Use uma URL absoluta sem usuário, senha, parâmetros ou fragmentos. RTSP pode ser configurado, mas ainda depende do relay do servidor para tocar no navegador.',
    enabled: 'Habilitada', disabled: 'Desabilitada', create: 'Criar fonte', save: 'Salvar alterações', delete: 'Excluir fonte', credentials: 'Credenciais protegidas', credentialConfigured: 'Há uma credencial salva no cofre protegido do host. O valor não pode ser lido de volta.', credentialMissing: 'Nenhuma credencial configurada neste host.', credentialMode: 'Tipo de autenticação', username: 'Usuário', password: 'Senha', bearerToken: 'Token Bearer', credentialHelp: 'As credenciais são somente de escrita, criptografadas e excluídas da exportação. Configure-as novamente ao importar o projeto em outro host.', saveCredential: 'Salvar credencial protegida', deleteCredential: 'Remover credencial', confirmDelete: 'Excluir esta fonte e sua credencial protegida?', confirmCredentialDelete: 'Remover a credencial protegida?', saved: 'Fonte salva em Working. Salve/publique o projeto separadamente para ativá-la.', deleted: 'Fonte excluída de Working.', credentialSaved: 'Credencial protegida salva.', credentialDeleted: 'Credencial protegida removida.'
  };
}
