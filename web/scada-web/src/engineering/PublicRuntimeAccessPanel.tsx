import { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import {
  authorityPolicyAdministrationApi,
  type AuthorityPolicyDocument,
  type RuntimePublicAccessPolicy,
  type RuntimePublicResourceCatalog
} from './userAdministrationApi';

const copy = {
  'pt-BR': {
    title: 'Runtime público',
    hint: 'Permite abrir a visualização sem login. A autenticação geral continua ativa; visitantes só recebem as telas e TAGs escolhidas e ocupam uma vaga View Only da licença.',
    enabled: 'Permitir visualização pública sem login',
    screen: 'Telas públicas',
    popup: 'Pop-ups públicos',
    tag: 'TAGs que podem ser lidas',
    asset: 'Imagens e arquivos visuais',
    start: 'Tela inicial',
    save: 'Salvar política pública',
    saving: 'Salvando…',
    loading: 'Carregando telas e recursos da revisão ativa…',
    unavailable: 'Não há uma revisão ativa disponível para configurar o Runtime público.',
    disabled: 'A visualização anônima está desligada. O Runtime continua exigindo login.',
    active: 'A visualização pública está ligada para a revisão publicada indicada.',
    readOnly: 'Visitantes não executam comandos, escrevem TAGs, reconhecem alarmes nem acessam Engineering, histórico ou administração.',
    login: 'O login continua disponível para usuários autorizados.',
    help: 'Como configurar e testar',
    noScreens: 'A revisão ativa não possui telas selecionáveis.',
    dirty: 'Alterações ainda não salvas',
    loadError: 'Não foi possível carregar os recursos públicos.'
  },
  en: {
    title: 'Public Runtime',
    hint: 'Allows the Runtime view to open without signing in. Global authentication remains on; visitors receive only selected screens and TAGs and use a licensed View Only seat.',
    enabled: 'Allow public viewing without sign-in',
    screen: 'Public screens',
    popup: 'Public pop-ups',
    tag: 'TAGs visitors may read',
    asset: 'Images and visual files',
    start: 'Start screen',
    save: 'Save public policy',
    saving: 'Saving…',
    loading: 'Loading screens and resources from the active revision…',
    unavailable: 'There is no active revision available for public Runtime setup.',
    disabled: 'Anonymous viewing is off. Runtime still requires sign-in.',
    active: 'Public viewing is on for the published revision shown here.',
    readOnly: 'Visitors cannot execute commands, write TAGs, acknowledge alarms, or access Engineering, history, or administration.',
    login: 'Sign-in remains available to authorized users.',
    help: 'How to configure and test',
    noScreens: 'The active revision has no selectable screens.',
    dirty: 'Unsaved changes',
    loadError: 'Public resources could not be loaded.'
  },
  es: {
    title: 'Runtime público',
    hint: 'Permite abrir la visualización sin iniciar sesión. La autenticación general permanece activa; los visitantes solo reciben las pantallas y TAG seleccionados y ocupan una plaza View Only de la licencia.',
    enabled: 'Permitir visualización pública sin inicio de sesión',
    screen: 'Pantallas públicas',
    popup: 'Ventanas emergentes públicas',
    tag: 'TAG que se pueden leer',
    asset: 'Imágenes y archivos visuales',
    start: 'Pantalla inicial',
    save: 'Guardar política pública',
    saving: 'Guardando…',
    loading: 'Cargando pantallas y recursos de la revisión activa…',
    unavailable: 'No hay una revisión activa disponible para configurar Runtime público.',
    disabled: 'La visualización anónima está desactivada. Runtime sigue requiriendo inicio de sesión.',
    active: 'La visualización pública está activada para la revisión publicada indicada.',
    readOnly: 'Los visitantes no ejecutan comandos, escriben TAG, reconocen alarmas ni acceden a Engineering, historial o administración.',
    login: 'El inicio de sesión sigue disponible para usuarios autorizados.',
    help: 'Cómo configurar y probar',
    noScreens: 'La revisión activa no tiene pantallas seleccionables.',
    dirty: 'Cambios sin guardar',
    loadError: 'No se pudieron cargar los recursos públicos.'
  }
} as const;

type Draft = RuntimePublicAccessPolicy;

function initialDraft(policy: RuntimePublicAccessPolicy | null | undefined): Draft {
  return {
    enabled: policy?.enabled ?? false,
    startScreenId: policy?.startScreenId ?? null,
    screenIds: [...(policy?.screenIds ?? [])],
    popupIds: [...(policy?.popupIds ?? [])],
    tagIds: [...(policy?.tagIds ?? [])],
    visualAssetIds: [...(policy?.visualAssetIds ?? [])],
    projectKey: policy?.projectKey ?? null,
    revision: policy?.revision ?? null
  };
}

export function PublicRuntimeAccessPanel({
  locale,
  policyVersion,
  policy,
  disabled,
  onApplied
}: {
  locale: EngineeringLocale;
  policyVersion: number;
  policy: RuntimePublicAccessPolicy | null | undefined;
  disabled: boolean;
  onApplied: (document: AuthorityPolicyDocument) => void;
}) {
  const text = copy[locale];
  const [catalog, setCatalog] = useState<RuntimePublicResourceCatalog | null>(null);
  const [draft, setDraft] = useState<Draft>(() => initialDraft(policy));
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedVersion, setSavedVersion] = useState(policyVersion);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void authorityPolicyAdministrationApi.publicRuntimeResources().then(value => {
      if (!active) return;
      setCatalog(value);
      setDraft(current => current.enabled ? ({
          ...current,
          projectKey: value.projectKey,
          revision: value.revision,
          startScreenId: current.startScreenId ?? value.startupScreenId ?? null
        }) : current);
    }).catch(reason => {
      if (!active) return;
      setError(reason instanceof Error ? reason.message : text.loadError);
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [policyVersion, text.loadError]);

  useEffect(() => {
    setDraft(initialDraft(policy));
    setSavedVersion(policyVersion);
  }, [policyVersion]);

  const availableSelectedScreens = useMemo(() =>
    draft.screenIds.filter(id => catalog?.screens.some(screen => screen.id === id)),
  [catalog, draft.screenIds]);
  const dirty = savedVersion !== policyVersion || JSON.stringify(draft) !== JSON.stringify(initialDraft(policy));

  const toggleId = (field: 'screenIds' | 'popupIds' | 'tagIds' | 'visualAssetIds', id: string) => {
    setDraft(current => {
      const existing = new Set(current[field]);
      if (existing.has(id)) existing.delete(id); else existing.add(id);
      const nextIds = [...existing];
      const next = { ...current, [field]: nextIds };
      if (field === 'screenIds' && current.startScreenId === id && !existing.has(id))
        next.startScreenId = nextIds[0] ?? null;
      if (field === 'screenIds' && !current.startScreenId && nextIds.length > 0)
        next.startScreenId = nextIds[0];
      return next;
    });
  };

  const apply = async () => {
    if (saving || disabled) return;
    setSaving(true);
    setError(null);
    const payload: RuntimePublicAccessPolicy = draft.enabled
      ? { ...draft, screenIds: availableSelectedScreens }
      : { enabled: false, startScreenId: null, screenIds: [], popupIds: [], tagIds: [], visualAssetIds: [], projectKey: null, revision: null };
    try {
      const applied = await authorityPolicyAdministrationApi.applyPublicRuntime(policyVersion, payload);
      setSavedVersion(applied.version);
      onApplied(applied);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : text.loadError);
    } finally {
      setSaving(false);
    }
  };

  return <section className="authority-policy" data-testid="public-runtime-policy">
    <header className="authority-policy-header">
      <div>
        <h3>{text.title}</h3>
        <p>{text.hint}</p>
        <p><strong>{policy?.enabled ? text.active : text.disabled}</strong></p>
        <p>{text.readOnly} {text.login}</p>
        <a href="/help?topic=security.public-runtime-view">{text.help}</a>
        {catalog ? <small>{catalog.projectKey} · revisão {catalog.revision}</small> : null}
      </div>
    </header>

    {error ? <div className="authority-policy-state error" role="alert">{error}</div> : null}
    {loading ? <div className="authority-policy-state">{text.loading}</div> : null}
    {!loading && !catalog ? <div className="authority-policy-state">{text.unavailable}</div> : null}
    {catalog ? <>
      <label className="authority-descendants">
        <input type="checkbox" checked={draft.enabled} onChange={event => setDraft(current => ({ ...current, enabled: event.target.checked, startScreenId: current.startScreenId ?? catalog.startupScreenId ?? null, projectKey: event.target.checked ? catalog.projectKey : current.projectKey, revision: event.target.checked ? catalog.revision : current.revision }))} />
        {text.enabled}
      </label>
      <label>
        {text.start}
        <select value={draft.startScreenId ?? ''} disabled={!draft.enabled} onChange={event => setDraft(current => ({ ...current, startScreenId: event.target.value || null }))}>
          <option value="">—</option>
          {availableSelectedScreens.map(id => {
            const screen = catalog.screens.find(candidate => candidate.id === id)!;
            return <option key={id} value={id}>{screen.name ?? screen.key ?? id}</option>;
          })}
        </select>
      </label>
      <ResourceChoices title={text.screen} values={catalog.screens} selected={draft.screenIds} disabled={!draft.enabled} empty={text.noScreens} onToggle={id => toggleId('screenIds', id)} />
      <ResourceChoices title={text.popup} values={catalog.popups} selected={draft.popupIds} disabled={!draft.enabled} onToggle={id => toggleId('popupIds', id)} />
      <ResourceChoices title={text.tag} values={catalog.tags.map(tag => ({ id: tag.id, key: tag.key, name: `${tag.name} (${tag.dataType}${tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''})` }))} selected={draft.tagIds} disabled={!draft.enabled} onToggle={id => toggleId('tagIds', id)} />
      <ResourceChoices title={text.asset} values={catalog.visualAssets} selected={draft.visualAssetIds} disabled={!draft.enabled} onToggle={id => toggleId('visualAssetIds', id)} />
      {dirty ? <p role="status">{text.dirty}</p> : null}
      <button type="button" className="user-admin-button primary" disabled={saving || disabled || loading || !dirty} onClick={() => void apply()}>
        {saving ? text.saving : text.save}
      </button>
    </> : null}
  </section>;
}

function ResourceChoices({
  title,
  values,
  selected,
  disabled,
  empty,
  onToggle
}: {
  title: string;
  values: Array<{ id: string; key?: string | null; name?: string | null }>;
  selected: string[];
  disabled: boolean;
  empty?: string;
  onToggle: (id: string) => void;
}) {
  return <fieldset disabled={disabled} style={{ border: '1px solid var(--app-border)', borderRadius: 8, margin: '12px 0', padding: 12 }}>
    <legend>{title}</legend>
    {values.length === 0 ? <small>{empty ?? '—'}</small> : values.map(value => <label key={value.id} style={{ display: 'flex', gap: 8, alignItems: 'center', margin: '6px 0' }}>
      <input type="checkbox" checked={selected.includes(value.id)} onChange={() => onToggle(value.id)} />
      <span>{value.name ?? value.key ?? value.id}</span>
    </label>)}
  </fieldset>;
}
