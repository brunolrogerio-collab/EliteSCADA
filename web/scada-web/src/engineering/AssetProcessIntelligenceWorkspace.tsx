import { useCallback, useEffect, useMemo, useState } from 'react';
import { hasWorkspaceCapability, useEffectiveCapabilities } from '../auth/effectiveCapabilities';
import type { EngineeringLocale } from './i18n';
import type {
  AssetProcessIntelligenceAssignmentEngineering,
  AssetProcessIntelligenceProcessStageEngineering,
  AssetProcessIntelligenceProfileEngineering,
  AssetProcessIntelligenceSignalEngineering,
  TagEngineering
} from './types';
import './asset-process-intelligence.css';

type ScopeKind = 'Equipment' | 'Location';
type Scope = { kind: ScopeKind; scopeId: string; active: boolean; executionArmed?: boolean };
type Resource = { kind: ScopeKind; scopeId: string; path?: string | null; name: string };
type Configuration = {
  version: number;
  scopes: Scope[];
  profiles: AssetProcessIntelligenceProfileEngineering[];
  assignments: AssetProcessIntelligenceAssignmentEngineering[];
  processStages: AssetProcessIntelligenceProcessStageEngineering[];
};
type Snapshot = {
  configuration: Configuration;
  resources: Resource[];
  tags: TagEngineering[];
  licensed: boolean;
  execution: {
    stage: string;
    workerRegistered: boolean;
    queueDepth: number;
    activePermitCount: number;
    projectKey: string | null;
    activeRevision: number | null;
    workspaceDirty: boolean;
  };
};

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const EMPTY_CONFIGURATION: Configuration = { version: 1, scopes: [], profiles: [], assignments: [], processStages: [] };

export function AssetProcessIntelligenceWorkspace({
  locale,
  tags: packageTags,
  onSaved
}: {
  locale: EngineeringLocale;
  tags: TagEngineering[];
  onSaved?: () => Promise<void>;
}) {
  const { capabilities } = useEffectiveCapabilities();
  const canEngineer = hasWorkspaceCapability(capabilities, 'EngineeringModify');
  const canManage = hasWorkspaceCapability(capabilities, 'AssetProcessIntelligenceManage');
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [profiles, setProfiles] = useState<AssetProcessIntelligenceProfileEngineering[]>([]);
  const [assignments, setAssignments] = useState<AssetProcessIntelligenceAssignmentEngineering[]>([]);
  const [processStages, setProcessStages] = useState<AssetProcessIntelligenceProcessStageEngineering[]>([]);
  const [selectedProfileId, setSelectedProfileId] = useState('');
  const [selectedStageId, setSelectedStageId] = useState('');
  const [assignmentEquipmentId, setAssignmentEquipmentId] = useState('');
  const [assignmentProfileId, setAssignmentProfileId] = useState('');
  const [stageAssignmentId, setStageAssignmentId] = useState('');
  const [newSignalKey, setNewSignalKey] = useState('');
  const [newSignalRole, setNewSignalRole] = useState('process');
  const [newSignalTagId, setNewSignalTagId] = useState('');
  const [newSignalRequired, setNewSignalRequired] = useState(true);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [configSaving, setConfigSaving] = useState(false);
  const [transitioning, setTransitioning] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const copy = useMemo(() => text(locale), [locale]);
  const load = useCallback(async (adoptConfiguration = false) => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes`, {
        headers: { accept: 'application/json' }, credentials: 'include'
      });
      if (!response.ok) throw await responseError(response);
      const next = await response.json() as Snapshot;
      next.configuration ??= EMPTY_CONFIGURATION;
      next.configuration.scopes ??= [];
      next.configuration.profiles ??= [];
      next.configuration.assignments ??= [];
      next.configuration.processStages ??= [];
      next.tags ??= [];
      setSnapshot(next);
      setSelected(new Set(next.configuration.scopes.map(scopeKey)));
      if (adoptConfiguration) {
        setProfiles(next.configuration.profiles);
        setAssignments(next.configuration.assignments);
        setProcessStages(next.configuration.processStages);
        setSelectedProfileId(current => next.configuration.profiles.some(item => item.id === current) ? current : next.configuration.profiles[0]?.id ?? '');
        setSelectedStageId(current => next.configuration.processStages.some(item => item.id === current) ? current : next.configuration.processStages[0]?.id ?? '');
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(true); }, [load]);

  const saveScopes = async () => {
    if (!snapshot) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const scopes = snapshot.resources
        .filter(resource => selected.has(scopeKey(resource)))
        .map(resource => ({ kind: resource.kind === 'Equipment' ? 0 : 1, scopeId: resource.scopeId }));
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes`, {
        method: 'PUT', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ scopes })
      });
      if (!response.ok) throw await responseError(response);
      setMessage(copy.saved);
      await onSaved?.();
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setSaving(false);
    }
  };

  const saveConfiguration = async () => {
    setConfigSaving(true);
    setError(null);
    setMessage(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/configuration`, {
        method: 'PUT', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({ profiles, assignments, processStages })
      });
      if (!response.ok) throw await responseError(response);
      setMessage(copy.configurationSaved);
      await onSaved?.();
      await load(true);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setConfigSaving(false);
    }
  };

  const setActivation = async (scope: Scope, active: boolean) => {
    const key = scopeKey(scope);
    setTransitioning(key);
    setError(null);
    setMessage(null);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/scopes/${scope.kind.toLowerCase()}/${encodeURIComponent(scope.scopeId)}/${active ? 'activate' : 'pause'}`, {
        method: 'POST', credentials: 'include', headers: { accept: 'application/json' }
      });
      if (!response.ok) throw await responseError(response);
      setMessage(active ? copy.activated : copy.paused);
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setTransitioning(null);
    }
  };

  const configured = snapshot?.configuration.scopes ?? [];
  const dirtySelection = snapshot !== null && (
    snapshot.resources.filter(resource => selected.has(scopeKey(resource))).length !== configured.length ||
    configured.some(scope => !selected.has(scopeKey(scope)))
  );
  const initialConfig = snapshot?.configuration ?? EMPTY_CONFIGURATION;
  const dirtyConfiguration = JSON.stringify({ profiles, assignments, processStages }) !== JSON.stringify({
    profiles: initialConfig.profiles, assignments: initialConfig.assignments, processStages: initialConfig.processStages
  });
  const selectedProfile = profiles.find(profile => profile.id === selectedProfileId) ?? null;
  const selectedStage = processStages.find(stage => stage.id === selectedStageId) ?? null;
  const tags = snapshot?.tags?.length ? snapshot.tags : packageTags;
  const equipment = (snapshot?.resources ?? []).filter(resource => resource.kind === 'Equipment');
  const tagsById = new Map(tags.filter(tag => tag.id).map(tag => [tag.id!.toLowerCase(), tag]));
  const resourcesByKey = new Map((snapshot?.resources ?? []).map(resource => [scopeKey(resource), resource]));
  const profileById = new Map(profiles.map(profile => [profile.id, profile]));
  const assignmentById = new Map(assignments.map(assignment => [assignment.id, assignment]));

  const createProfile = () => {
    const nextNumber = profiles.length + 1;
    const profile: AssetProcessIntelligenceProfileEngineering = {
      id: crypto.randomUUID(), key: `profile-${nextNumber}`, name: `${copy.defaultProfile} ${nextNumber}`, signals: []
    };
    setProfiles(current => [...current, profile]);
    setSelectedProfileId(profile.id);
  };

  const updateProfile = (changes: Partial<AssetProcessIntelligenceProfileEngineering>) => {
    if (!selectedProfile) return;
    setProfiles(current => current.map(profile => profile.id === selectedProfile.id ? { ...profile, ...changes } : profile));
  };

  const addSignal = () => {
    if (!selectedProfile || !newSignalTagId) return;
    const signal: AssetProcessIntelligenceSignalEngineering = {
      id: crypto.randomUUID(), key: newSignalKey.trim() || slug(copy.defaultSignal),
      signalRole: newSignalRole.trim(), tagId: newSignalTagId, required: newSignalRequired
    };
    updateProfile({ signals: [...selectedProfile.signals, signal] });
    setNewSignalKey('');
    setNewSignalTagId('');
  };

  const deleteProfile = () => {
    if (!selectedProfile) return;
    const removedAssignments = new Set(assignments.filter(item => item.profileId === selectedProfile.id).map(item => item.id));
    setProfiles(current => current.filter(item => item.id !== selectedProfile.id));
    setAssignments(current => current.filter(item => item.profileId !== selectedProfile.id));
    setProcessStages(current => current.map(stage => compactLinks({ ...stage, links: stage.links.filter(link => !removedAssignments.has(link.assignmentId)) })));
    setSelectedProfileId('');
  };

  const addAssignment = () => {
    if (!assignmentEquipmentId || !assignmentProfileId) return;
    const id = crypto.randomUUID();
    setAssignments(current => [...current, { id, equipmentId: assignmentEquipmentId, profileId: assignmentProfileId }]);
    setAssignmentEquipmentId('');
    setAssignmentProfileId('');
  };

  const removeAssignment = (id: string) => {
    setAssignments(current => current.filter(item => item.id !== id));
    setProcessStages(current => current.map(stage => compactLinks({ ...stage, links: stage.links.filter(link => link.assignmentId !== id) })));
  };

  const addStage = () => {
    const nextNumber = processStages.length + 1;
    const stage: AssetProcessIntelligenceProcessStageEngineering = {
      id: crypto.randomUUID(), key: `stage-${nextNumber}`, name: `${copy.defaultStage} ${nextNumber}`,
      order: processStages.length, links: []
    };
    setProcessStages(current => [...current, stage]);
    setSelectedStageId(stage.id);
  };

  const updateStage = (changes: Partial<AssetProcessIntelligenceProcessStageEngineering>) => {
    if (!selectedStage) return;
    setProcessStages(current => current.map(stage => stage.id === selectedStage.id ? { ...stage, ...changes } : stage));
  };

  const addAssignmentToStage = () => {
    if (!selectedStage || !stageAssignmentId || selectedStage.links.some(link => link.assignmentId === stageAssignmentId)) return;
    updateStage({ links: [...selectedStage.links, { assignmentId: stageAssignmentId, order: selectedStage.links.length }] });
    setStageAssignmentId('');
  };

  const reorderStage = (stageId: string, offset: number) => {
    setProcessStages(current => {
      const ordered = [...current].sort((a, b) => a.order - b.order || a.key.localeCompare(b.key));
      const from = ordered.findIndex(item => item.id === stageId);
      const to = from + offset;
      if (from < 0 || to < 0 || to >= ordered.length) return current;
      [ordered[from], ordered[to]] = [ordered[to], ordered[from]];
      return ordered.map((stage, order) => ({ ...stage, order }));
    });
  };

  const reorderStageLink = (stageId: string, assignmentId: string, offset: number) => {
    setProcessStages(current => current.map(stage => {
      if (stage.id !== stageId) return stage;
      const links = [...stage.links].sort((a, b) => a.order - b.order || a.assignmentId.localeCompare(b.assignmentId));
      const from = links.findIndex(item => item.assignmentId === assignmentId);
      const to = from + offset;
      if (from < 0 || to < 0 || to >= links.length) return stage;
      [links[from], links[to]] = [links[to], links[from]];
      return { ...stage, links: links.map((link, order) => ({ ...link, order })) };
    }));
  };

  return <section className="eng-section asset-process-intelligence" data-testid="asset-process-intelligence-workspace">
    <header className="eng-section-header">
      <div><span className="eng-eyebrow">{copy.eyebrow}</span><h1>{copy.title}</h1><p>{copy.description}</p></div>
      {snapshot ? <div className="eng-section-meta"><strong>{snapshot.execution.stage}</strong><span>{snapshot.execution.activePermitCount} {copy.permits}</span></div> : null}
    </header>

    <section className="eng-panel asset-process-intelligence__stage" aria-live="polite">
      <strong>{snapshot?.execution.workerRegistered ? copy.workerAvailable : copy.noWorker}</strong>
      <p>{copy.stageHint}</p>
      {snapshot?.execution.workspaceDirty ? <p role="status">{copy.workingChanges} <a href="/engineering">{copy.openLifecycle}</a></p> : null}
      <div className="asset-process-intelligence__status">
        <span>{copy.entitlement}: <strong>{snapshot?.licensed ? copy.entitled : copy.notEntitled}</strong></span>
        <span>{copy.project}: <strong>{snapshot?.execution.projectKey ?? copy.noProject}</strong></span>
        <span>{copy.revision}: <strong>{snapshot?.execution.activeRevision ?? '—'}</strong></span>
        <span>{copy.queue}: <strong>{snapshot?.execution.queueDepth ?? 0}</strong></span>
      </div>
    </section>

    {error ? <div className="eng-state-card error" role="alert">{error}</div> : null}
    {message ? <div className="eng-state-card" role="status">{message}</div> : null}
    {loading ? <div className="eng-state-card"><strong>{copy.loading}</strong></div> : null}
    {!loading && snapshot && <>
      <section className="eng-panel asset-process-intelligence__scope-panel">
        <header><div><h2>{copy.eligibleScopes}</h2><p>{copy.scopeHint}</p></div>
          <button type="button" className="secondary" onClick={() => void load()}>{copy.refresh}</button>
        </header>
        {snapshot.resources.length === 0 ? <div className="eng-empty"><strong>{copy.noResources}</strong></div> :
          <div className="asset-process-intelligence__list">
            {snapshot.resources.map(resource => {
              const key = scopeKey(resource);
              const scope = configured.find(item => scopeKey(item) === key);
              const active = scope?.active === true;
              const armed = scope?.executionArmed === true;
              const canPause = active && armed;
              return <article key={key} className="asset-process-intelligence__row">
                <label>
                  <input type="checkbox" checked={selected.has(key)} disabled={!canEngineer || active || saving || transitioning !== null}
                    onChange={event => setSelected(current => {
                      const next = new Set(current);
                      if (event.currentTarget.checked) next.add(key); else next.delete(key);
                      return next;
                    })}/>
                  <span><strong>{resource.name}</strong><small>{resource.kind} · {resource.path ?? resource.scopeId}</small></span>
                </label>
                <div className="asset-process-intelligence__controls">
                  {!scope ? <span>{copy.notConfigured}</span> : <>
                    <span>{active ? copy.configuredActive : copy.statusPaused}</span>
                    <span>{armed ? copy.permitArmed : active ? copy.permitClosed : copy.noPermit}</span>
                    <button type="button" className="secondary" disabled={!canEngineer || !canManage || (!snapshot.licensed && !canPause) || (snapshot.execution.workspaceDirty && !canPause) || transitioning !== null}
                      onClick={() => void setActivation(scope, !canPause)}>
                      {transitioning === key ? copy.working : canPause ? copy.pause : copy.activate}
                    </button>
                  </>}
                </div>
              </article>;
            })}
          </div>}
        <footer className="asset-process-intelligence__footer">
          {!canEngineer || !canManage ? <span>{copy.authorizationHint}</span> : null}
          <button type="button" disabled={!canEngineer || !dirtySelection || saving || transitioning !== null} onClick={() => void saveScopes()}>
            {saving ? copy.saving : copy.saveScopes}
          </button>
        </footer>
      </section>

      <section className="eng-panel asset-process-intelligence__authoring" data-testid="intelligence-stage2-authoring">
        <header className="asset-process-intelligence__authoring-header">
          <div><h2>{copy.stage2Title}</h2><p>{copy.stage2Hint}</p></div>
          <button type="button" disabled={!canEngineer || !dirtyConfiguration || configSaving} onClick={() => void saveConfiguration()}>
            {configSaving ? copy.saving : copy.saveConfiguration}
          </button>
        </header>

        <section className="asset-process-intelligence__authoring-section" aria-labelledby="intelligence-profiles-heading">
          <div className="asset-process-intelligence__section-heading"><div><h3 id="intelligence-profiles-heading">{copy.profiles}</h3><p>{copy.profilesHint}</p></div>
            <button type="button" className="secondary" disabled={!canEngineer} onClick={createProfile}>{copy.newProfile}</button>
          </div>
          {profiles.length === 0 ? <div className="eng-empty"><strong>{copy.noProfiles}</strong></div> : <div className="asset-process-intelligence__split">
            <div className="asset-process-intelligence__choice-list" role="listbox" aria-label={copy.profiles}>
              {profiles.map(profile => <button type="button" role="option" aria-selected={profile.id === selectedProfileId} key={profile.id} onClick={() => setSelectedProfileId(profile.id)}>
                <strong>{profile.name}</strong><small>{profile.key} · {profile.signals.length} {copy.signals.toLowerCase()}</small>
              </button>)}
            </div>
            {selectedProfile ? <div className="asset-process-intelligence__editor">
              <div className="asset-process-intelligence__field-grid">
                <label>{copy.profileKey}<input value={selectedProfile.key} disabled={!canEngineer} onChange={event => updateProfile({ key: event.currentTarget.value })}/></label>
                <label>{copy.profileName}<input value={selectedProfile.name} disabled={!canEngineer} onChange={event => updateProfile({ name: event.currentTarget.value })}/></label>
              </div>
              <div className="asset-process-intelligence__section-heading"><div><h4>{copy.signals}</h4><p>{copy.signalsHint}</p></div></div>
              {selectedProfile.signals.length === 0 ? <div className="eng-empty"><strong>{copy.noSignals}</strong></div> : <div className="asset-process-intelligence__list">
                {selectedProfile.signals.map(signal => {
                  const tag = tagsById.get(signal.tagId.toLowerCase());
                  return <article className="asset-process-intelligence__row" key={signal.id}>
                    <span><strong>{signal.key} · {signal.signalRole}</strong><small>{tag ? `${tag.path} · ${tag.dataType}${tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}` : `${copy.missingTag}: ${signal.tagId}`}</small></span>
                    <span className={tag ? '' : 'asset-process-intelligence__missing'}>{signal.required ? copy.required : copy.optional}</span>
                    <button type="button" className="secondary" disabled={!canEngineer} onClick={() => updateProfile({ signals: selectedProfile.signals.filter(item => item.id !== signal.id) })}>{copy.remove}</button>
                  </article>;
                })}
              </div>}
              <div className="asset-process-intelligence__signal-form">
                <label>{copy.signalKey}<input value={newSignalKey} disabled={!canEngineer} onChange={event => setNewSignalKey(event.currentTarget.value)} placeholder={copy.signalKeyPlaceholder}/></label>
                <label>{copy.signalRole}<input value={newSignalRole} disabled={!canEngineer} onChange={event => setNewSignalRole(event.currentTarget.value)}/></label>
                <label>{copy.tagReference}<select value={newSignalTagId} disabled={!canEngineer || tags.length === 0} onChange={event => setNewSignalTagId(event.currentTarget.value)}>
                  <option value="">{tags.length ? copy.chooseTag : copy.noTags}</option>
                  {tags.filter(tag => tag.id).map(tag => <option key={tag.id} value={tag.id}>{tag.path} · {tag.dataType}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</option>)}
                </select></label>
                <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={newSignalRequired} disabled={!canEngineer} onChange={event => setNewSignalRequired(event.currentTarget.checked)}/>{copy.required}</label>
                <button type="button" className="secondary" disabled={!canEngineer || !newSignalTagId} onClick={addSignal}>{copy.addSignal}</button>
              </div>
              <button type="button" className="secondary asset-process-intelligence__danger" disabled={!canEngineer} onClick={deleteProfile}>{copy.deleteProfile}</button>
            </div> : null}
          </div>}
        </section>

        <section className="asset-process-intelligence__authoring-section" aria-labelledby="intelligence-assignments-heading">
          <div className="asset-process-intelligence__section-heading"><div><h3 id="intelligence-assignments-heading">{copy.assignments}</h3><p>{copy.assignmentsHint}</p></div></div>
          {equipment.length === 0 || profiles.length === 0 ? <div className="eng-empty"><strong>{equipment.length === 0 ? copy.noEquipment : copy.noProfiles}</strong></div> : <div className="asset-process-intelligence__assignment-form">
            <label>{copy.equipment}<select value={assignmentEquipmentId} disabled={!canEngineer} onChange={event => setAssignmentEquipmentId(event.currentTarget.value)}>
              <option value="">{copy.chooseEquipment}</option>{equipment.map(item => <option key={item.scopeId} value={item.scopeId}>{item.path ? `${item.path} · ` : ''}{item.name}</option>)}
            </select></label>
            <label>{copy.profile}<select value={assignmentProfileId} disabled={!canEngineer} onChange={event => setAssignmentProfileId(event.currentTarget.value)}>
              <option value="">{copy.chooseProfile}</option>{profiles.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select></label>
            <button type="button" className="secondary" disabled={!canEngineer || !assignmentEquipmentId || !assignmentProfileId || assignments.some(item => item.equipmentId === assignmentEquipmentId && item.profileId === assignmentProfileId)} onClick={addAssignment}>{copy.assignProfile}</button>
          </div>}
          {assignments.length === 0 ? <div className="eng-empty"><strong>{copy.noAssignments}</strong></div> : <div className="asset-process-intelligence__list">
            {assignments.map(assignment => {
              const equipmentResource = resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`);
              const profile = profileById.get(assignment.profileId);
              return <article className="asset-process-intelligence__row" key={assignment.id}>
                <span><strong>{equipmentResource?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</strong><small>{equipmentResource?.path ?? assignment.equipmentId} · {profile?.key ?? assignment.profileId}</small></span>
                {(!equipmentResource || !profile) ? <span className="asset-process-intelligence__missing">{copy.referenceMissing}</span> : null}
                <button type="button" className="secondary" disabled={!canEngineer} onClick={() => removeAssignment(assignment.id)}>{copy.remove}</button>
              </article>;
            })}
          </div>}
        </section>

        <section className="asset-process-intelligence__authoring-section" aria-labelledby="intelligence-stages-heading">
          <div className="asset-process-intelligence__section-heading"><div><h3 id="intelligence-stages-heading">{copy.processStages}</h3><p>{copy.processStagesHint}</p></div>
            <button type="button" className="secondary" disabled={!canEngineer} onClick={addStage}>{copy.newStage}</button>
          </div>
          {processStages.length === 0 ? <div className="eng-empty"><strong>{copy.noStages}</strong></div> : <div className="asset-process-intelligence__split">
            <div className="asset-process-intelligence__choice-list" role="listbox" aria-label={copy.processStages}>
              {[...processStages].sort((a, b) => a.order - b.order).map((stage, index) => <div className="asset-process-intelligence__stage-choice" key={stage.id}>
                <button type="button" role="option" aria-selected={stage.id === selectedStageId} onClick={() => setSelectedStageId(stage.id)}><strong>{stage.order + 1}. {stage.name}</strong><small>{stage.key}</small></button>
                <span><button type="button" className="secondary" aria-label={copy.moveUp} disabled={!canEngineer || index === 0} onClick={() => reorderStage(stage.id, -1)}>↑</button><button type="button" className="secondary" aria-label={copy.moveDown} disabled={!canEngineer || index === processStages.length - 1} onClick={() => reorderStage(stage.id, 1)}>↓</button></span>
              </div>)}
            </div>
            {selectedStage ? <div className="asset-process-intelligence__editor">
              <div className="asset-process-intelligence__field-grid">
                <label>{copy.stageKey}<input value={selectedStage.key} disabled={!canEngineer} onChange={event => updateStage({ key: event.currentTarget.value })}/></label>
                <label>{copy.stageName}<input value={selectedStage.name} disabled={!canEngineer} onChange={event => updateStage({ name: event.currentTarget.value })}/></label>
              </div>
              {selectedStage.links.length === 0 ? <div className="eng-empty"><strong>{copy.noStageAssignments}</strong></div> : <div className="asset-process-intelligence__list">
                {[...selectedStage.links].sort((a, b) => a.order - b.order).map((link, index) => {
                  const assignment = assignmentById.get(link.assignmentId);
                  const equipmentResource = assignment ? resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`) : undefined;
                  const profile = assignment ? profileById.get(assignment.profileId) : undefined;
                  return <article className="asset-process-intelligence__row" key={`${selectedStage.id}:${link.assignmentId}`}>
                    <span><strong>{link.order + 1}. {equipmentResource?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</strong><small>{assignment ? `${equipmentResource?.path ?? assignment.equipmentId} · ${profile?.key ?? assignment.profileId}` : `${copy.referenceMissing}: ${link.assignmentId}`}</small></span>
                    {(!assignment || !equipmentResource || !profile) ? <span className="asset-process-intelligence__missing">{copy.referenceMissing}</span> : null}
                    <span><button type="button" className="secondary" aria-label={copy.moveUp} disabled={!canEngineer || index === 0} onClick={() => reorderStageLink(selectedStage.id, link.assignmentId, -1)}>↑</button><button type="button" className="secondary" aria-label={copy.moveDown} disabled={!canEngineer || index === selectedStage.links.length - 1} onClick={() => reorderStageLink(selectedStage.id, link.assignmentId, 1)}>↓</button><button type="button" className="secondary" disabled={!canEngineer} onClick={() => updateStage({ links: compactLinks({ ...selectedStage, links: selectedStage.links.filter(item => item.assignmentId !== link.assignmentId) }).links })}>{copy.remove}</button></span>
                  </article>;
                })}
              </div>}
              <div className="asset-process-intelligence__assignment-form">
                <label>{copy.stageAssignment}<select value={stageAssignmentId} disabled={!canEngineer || assignments.length === 0} onChange={event => setStageAssignmentId(event.currentTarget.value)}>
                  <option value="">{assignments.length ? copy.chooseAssignment : copy.noAssignments}</option>
                  {assignments.filter(item => !selectedStage.links.some(link => link.assignmentId === item.id)).map(item => {
                    const equip = resourcesByKey.get(`Equipment:${item.equipmentId.toLowerCase()}`);
                    const profile = profileById.get(item.profileId);
                    return <option key={item.id} value={item.id}>{equip?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</option>;
                  })}
                </select></label>
                <button type="button" className="secondary" disabled={!canEngineer || !stageAssignmentId} onClick={addAssignmentToStage}>{copy.addToStage}</button>
              </div>
            </div> : null}
          </div>}
        </section>
        <footer className="asset-process-intelligence__authoring-footer">
          <span>{copy.staticOnly}</span>
          <button type="button" disabled={!canEngineer || !dirtyConfiguration || configSaving} onClick={() => void saveConfiguration()}>
            {configSaving ? copy.saving : copy.saveConfiguration}
          </button>
        </footer>
      </section>
    </>}
  </section>;
}

function compactLinks(stage: AssetProcessIntelligenceProcessStageEngineering): AssetProcessIntelligenceProcessStageEngineering {
  return { ...stage, links: [...stage.links].sort((a, b) => a.order - b.order).map((link, order) => ({ ...link, order })) };
}

function scopeKey(scope: { kind: ScopeKind; scopeId: string }) { return `${scope.kind}:${scope.scopeId.toLowerCase()}`; }
function slug(value: string) {
  return value.normalize('NFKD').replace(/[\u0300-\u036f]/g, '').toLowerCase().replace(/[^a-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 64) || 'signal';
}

async function responseError(response: Response): Promise<Error> {
  let detail = `${response.status} ${response.statusText}`;
  try {
    const payload = await response.json() as { error?: unknown };
    if (typeof payload.error === 'string') detail = payload.error;
  } catch { /* keep status text */ }
  return new Error(detail);
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Engineering · Stage 1/2', title: 'Asset & Process Intelligence',
    description: 'Configure eligible scopes and authored process metadata. No analysis or runtime values are read.',
    noWorker: 'No analytics worker is registered in this Stage 1.', workerAvailable: 'Analytics worker registered',
    stageHint: 'Activation publishes the scope against the current active project revision. It does not run analysis yet.', workingChanges: 'Working changes must be reviewed and published before activation.', openLifecycle: 'Open project lifecycle',
    entitlement: 'Signed entitlement', entitled: 'Available', notEntitled: 'Required to activate', project: 'Project', revision: 'Active revision', queue: 'Queue', permits: 'execution permits',
    noProject: 'No active project', eligibleScopes: 'Eligible scopes', scopeHint: 'Select stable Equipment and Location identities. Saving changes Working only; publish it before activation.',
    noResources: 'No stable Equipment or Location resources are available.', refresh: 'Refresh', notConfigured: 'Not selected', configuredActive: 'Configured active', statusPaused: 'Paused', permitArmed: 'Process permit armed', permitClosed: 'Configured active · process permit closed; explicitly activate after authorization is restored (or after restart, if applicable)', noPermit: 'No process permit', activate: 'Activate', pause: 'Pause', working: 'Applying…', saveScopes: 'Save eligible scopes', saving: 'Saving…', saved: 'Eligible scopes saved.', activated: 'Scope activated for the current revision.', paused: 'Scope paused; pending work was cancelled.', authorizationHint: 'EngineeringModify and a matching resource-scoped AssetProcessIntelligenceManage grant are required.', loading: 'Loading scopes…',
    stage2Title: 'Process configuration', stage2Hint: 'Create reusable profiles from stable TAG identities, assign them to Equipment and order the process stages. These edits are saved to Working.',
    profiles: 'Reusable profiles', profilesHint: 'A profile can be reused by multiple Equipment assignments.', newProfile: 'New profile', noProfiles: 'No profiles yet. Create one to define static signal references.', defaultProfile: 'Profile', defaultSignal: 'Signal', profileKey: 'Stable key', profileName: 'Display name', signals: 'Signal references', signalsHint: 'References are by TAG ID; type and unit are shown from the canonical TAG.', noSignals: 'No signals in this profile.', signalKey: 'Signal key', signalKeyPlaceholder: 'e.g. temperature', signalRole: 'Domain role', tagReference: 'TAG reference', chooseTag: 'Choose a TAG', noTags: 'No stable TAGs available', addSignal: 'Add signal', required: 'Required', optional: 'Optional', missingTag: 'Missing TAG reference', remove: 'Remove', deleteProfile: 'Delete profile and its assignments',
    assignments: 'Equipment assignments', assignmentsHint: 'Assign a reusable profile to each stable Equipment identity. One Equipment may have multiple profile assignments.', equipment: 'Equipment', profile: 'Profile', chooseEquipment: 'Choose Equipment', chooseProfile: 'Choose a profile', assignProfile: 'Assign profile', noEquipment: 'No stable Equipment is available.', noAssignments: 'No assignments yet.', missingEquipment: 'Missing Equipment', missingProfile: 'Missing profile', referenceMissing: 'Reference is missing',
    processStages: 'Ordered process stages', processStagesHint: 'Arrange assignment links into stages; order is explicit and stable.', newStage: 'New stage', noStages: 'No process stages yet.', defaultStage: 'Stage', stageKey: 'Stable key', stageName: 'Display name', noStageAssignments: 'No assignments in this stage.', stageAssignment: 'Assignment', chooseAssignment: 'Choose an assignment', addToStage: 'Add to stage', moveUp: 'Move up', moveDown: 'Move down', staticOnly: 'Configuration-only: no TAG reads, samples, Historian queries or inferred values are stored.', saveConfiguration: 'Save process configuration', configurationSaved: 'Process configuration saved to Working.'
  } as const;
  if (locale === 'es') return {
    eyebrow: 'Ingeniería · Etapa 1/2', title: 'Inteligencia de Activos y Procesos',
    description: 'Configure alcances elegibles y metadatos del proceso. No se leen valores de ejecución ni se analiza.',
    noWorker: 'Esta etapa aún no registra ningún worker analítico.', workerAvailable: 'Worker analítico registrado',
    stageHint: 'La activación publica el alcance contra la revisión activa del proyecto. Todavía no ejecuta análisis.', workingChanges: 'Revise y publique los cambios de trabajo antes de activar.', openLifecycle: 'Abrir ciclo del proyecto',
    entitlement: 'Licencia firmada', entitled: 'Disponible', notEntitled: 'Necesaria para activar', project: 'Proyecto', revision: 'Revisión activa', queue: 'Cola', permits: 'permisos de ejecución',
    noProject: 'Sin proyecto activo', eligibleScopes: 'Alcances elegibles', scopeHint: 'Seleccione identidades estables. Guardar modifica solo Working; publique antes de activar.',
    noResources: 'No hay recursos estables de equipo o ubicación.', refresh: 'Actualizar', notConfigured: 'No seleccionado', configuredActive: 'Activo configurado', statusPaused: 'Pausado', permitArmed: 'Permiso de proceso habilitado', permitClosed: 'Activo configurado · permiso de proceso cerrado; actívelo explícitamente tras restaurar la autorización (o tras reiniciar, si corresponde)', noPermit: 'Sin permiso de proceso', activate: 'Activar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Guardar alcances', saving: 'Guardando…', saved: 'Alcances guardados.', activated: 'Alcance activado para la revisión actual.', paused: 'Alcance pausado; se canceló el trabajo pendiente.', authorizationHint: 'Se requiere EngineeringModify y un permiso AssetProcessIntelligenceManage para ese recurso.', loading: 'Cargando alcances…',
    stage2Title: 'Configuración de procesos', stage2Hint: 'Cree perfiles reutilizables con identidades TAG estables, asígnelos a equipos y ordene las etapas. Los cambios se guardan en Working.',
    profiles: 'Perfiles reutilizables', profilesHint: 'Un perfil puede reutilizarse en varias asignaciones de equipos.', newProfile: 'Nuevo perfil', noProfiles: 'Aún no hay perfiles. Cree uno para definir referencias estáticas.', defaultProfile: 'Perfil', defaultSignal: 'Señal', profileKey: 'Clave estable', profileName: 'Nombre', signals: 'Referencias de señales', signalsHint: 'Las referencias usan el ID del TAG; tipo y unidad provienen del TAG canónico.', noSignals: 'Este perfil no tiene señales.', signalKey: 'Clave de señal', signalKeyPlaceholder: 'p. ej. temperatura', signalRole: 'Rol de dominio', tagReference: 'Referencia TAG', chooseTag: 'Elija un TAG', noTags: 'No hay TAGs estables', addSignal: 'Añadir señal', required: 'Obligatoria', optional: 'Opcional', missingTag: 'Falta la referencia TAG', remove: 'Eliminar', deleteProfile: 'Eliminar perfil y sus asignaciones',
    assignments: 'Asignaciones de equipo', assignmentsHint: 'Asigne un perfil reutilizable a cada identidad estable de equipo. Un equipo puede tener varios perfiles.', equipment: 'Equipo', profile: 'Perfil', chooseEquipment: 'Elija un equipo', chooseProfile: 'Elija un perfil', assignProfile: 'Asignar perfil', noEquipment: 'No hay equipos estables.', noAssignments: 'Aún no hay asignaciones.', missingEquipment: 'Equipo inexistente', missingProfile: 'Perfil inexistente', referenceMissing: 'Falta la referencia',
    processStages: 'Etapas ordenadas', processStagesHint: 'Organice los enlaces de asignación; el orden es explícito y estable.', newStage: 'Nueva etapa', noStages: 'Aún no hay etapas.', defaultStage: 'Etapa', stageKey: 'Clave estable', stageName: 'Nombre', noStageAssignments: 'No hay asignaciones en esta etapa.', stageAssignment: 'Asignación', chooseAssignment: 'Elija una asignación', addToStage: 'Añadir a etapa', moveUp: 'Mover arriba', moveDown: 'Mover abajo', staticOnly: 'Solo configuración: no se leen TAGs, valores, Historian ni se guardan inferencias.', saveConfiguration: 'Guardar configuración', configurationSaved: 'Configuración guardada en Working.'
  } as const;
  return {
    eyebrow: 'Engenharia · Stage 1/2', title: 'Inteligência de Ativos e Processos',
    description: 'Configure escopos elegíveis e metadados de processo. Nenhum valor de runtime é lido nem analisado.',
    noWorker: 'Esta Stage 1 ainda não registra nenhum worker analítico.', workerAvailable: 'Worker analítico registrado',
    stageHint: 'A ativação publica o escopo na revisão ativa do projeto. Ainda não executa análises.', workingChanges: 'Revise e publique as alterações de Working antes de ativar.', openLifecycle: 'Abrir ciclo do projeto',
    entitlement: 'Licença assinada', entitled: 'Disponível', notEntitled: 'Necessária para ativar', project: 'Projeto', revision: 'Revisão ativa', queue: 'Fila', permits: 'permissões de execução',
    noProject: 'Nenhum projeto ativo', eligibleScopes: 'Escopos elegíveis', scopeHint: 'Selecione identidades estáveis. Salvar altera apenas o Working; publique antes de ativar.',
    noResources: 'Não há equipamentos ou locais com identidade estável.', refresh: 'Atualizar', notConfigured: 'Não selecionado', configuredActive: 'Ativo na configuração', statusPaused: 'Pausado', permitArmed: 'Permissão de processo armada', permitClosed: 'Ativo configurado · permissão de processo fechada; ative explicitamente após restaurar a autorização (ou após reiniciar, se aplicável)', noPermit: 'Sem permissão de processo', activate: 'Ativar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Salvar escopos elegíveis', saving: 'Salvando…', saved: 'Escopos elegíveis salvos.', activated: 'Escopo ativado para a revisão atual.', paused: 'Escopo pausado; o trabalho pendente foi cancelado.', authorizationHint: 'São necessários EngineeringModify e AssetProcessIntelligenceManage com escopo para este recurso.', loading: 'Carregando escopos…',
    stage2Title: 'Configuração de processos', stage2Hint: 'Crie perfis reutilizáveis com referências estáveis de TAG, associe-os a equipamentos e ordene as etapas. As edições são salvas no Working.',
    profiles: 'Perfis reutilizáveis', profilesHint: 'Um perfil pode ser reutilizado em várias associações de equipamentos.', newProfile: 'Novo perfil', noProfiles: 'Nenhum perfil ainda. Crie um para definir referências estáticas de sinais.', defaultProfile: 'Perfil', defaultSignal: 'Sinal', profileKey: 'Chave estável', profileName: 'Nome de exibição', signals: 'Referências de sinais', signalsHint: 'As referências usam o ID estável do TAG; tipo e unidade vêm do TAG canônico.', noSignals: 'Este perfil não possui sinais.', signalKey: 'Chave do sinal', signalKeyPlaceholder: 'ex.: temperatura', signalRole: 'Papel de domínio', tagReference: 'Referência do TAG', chooseTag: 'Escolha um TAG', noTags: 'Nenhum TAG estável disponível', addSignal: 'Adicionar sinal', required: 'Obrigatório', optional: 'Opcional', missingTag: 'Referência de TAG ausente', remove: 'Remover', deleteProfile: 'Excluir perfil e associações',
    assignments: 'Associações de equipamentos', assignmentsHint: 'Associe um perfil reutilizável a cada identidade estável de equipamento. Um equipamento pode usar vários perfis.', equipment: 'Equipamento', profile: 'Perfil', chooseEquipment: 'Escolha um equipamento', chooseProfile: 'Escolha um perfil', assignProfile: 'Associar perfil', noEquipment: 'Nenhum equipamento estável disponível.', noAssignments: 'Nenhuma associação ainda.', missingEquipment: 'Equipamento ausente', missingProfile: 'Perfil ausente', referenceMissing: 'Referência ausente',
    processStages: 'Etapas ordenadas do processo', processStagesHint: 'Organize associações nas etapas; a ordem é explícita e estável.', newStage: 'Nova etapa', noStages: 'Nenhuma etapa de processo ainda.', defaultStage: 'Etapa', stageKey: 'Chave estável', stageName: 'Nome de exibição', noStageAssignments: 'Nenhuma associação nesta etapa.', stageAssignment: 'Associação', chooseAssignment: 'Escolha uma associação', addToStage: 'Adicionar à etapa', moveUp: 'Mover para cima', moveDown: 'Mover para baixo', staticOnly: 'Somente configuração: sem leitura de TAG, valores, consultas ao Historian ou inferências.', saveConfiguration: 'Salvar configuração de processo', configurationSaved: 'Configuração de processo salva no Working.'
  } as const;
}
