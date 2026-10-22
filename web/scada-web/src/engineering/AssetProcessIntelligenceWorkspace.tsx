import { useCallback, useEffect, useMemo, useState } from 'react';
import { hasRuntimeCapability, hasWorkspaceCapability, useEffectiveCapabilities } from '../auth/effectiveCapabilities';
import type { EngineeringLocale } from './i18n';
import type {
  AssetProcessIntelligenceAssignmentEngineering,
  AssetProcessIntelligenceOperatingContextPredicateEngineering,
  AssetProcessIntelligenceOperatingContextPredicateKind,
  AssetProcessIntelligenceOperatingContextProfileEngineering,
  AssetProcessIntelligenceProcessStageEngineering,
  AssetProcessIntelligenceProfileEngineering,
  AssetProcessIntelligenceSignalSemanticBinding,
  AssetProcessIntelligenceSignalEngineering,
  ScreenEngineering,
  TagEngineering
} from './types';
import { loadEngineeringSemanticCatalog, type EngineeringSemanticCatalogResponse, type VersionedSemanticCatalog } from './semanticCatalogApi';
import {
  generateIntelligenceDashboardDraft,
  generateIntelligenceProfileCoverageScreenDraft,
  generateIntelligencePerformanceScreenDraft,
  type IntelligenceDashboardAssignmentSource,
  type IntelligenceDashboardSource,
  type IntelligencePerformanceDashboardSelection,
  type IntelligenceProfileCoverageDashboardSelection
} from './intelligenceDashboardGenerator';
import { AssetProcessIntelligenceCoveragePanel } from './AssetProcessIntelligenceCoveragePanel';
import { AssetProcessIntelligencePerformancePanel } from './AssetProcessIntelligencePerformancePanel';
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
  operatingContextProfiles: AssetProcessIntelligenceOperatingContextProfileEngineering[];
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
const EMPTY_CONFIGURATION: Configuration = { version: 1, scopes: [], profiles: [], assignments: [], processStages: [], operatingContextProfiles: [] };

export function AssetProcessIntelligenceWorkspace({
  locale,
  tags: packageTags,
  screens = [],
  onSaved,
  onCreateDashboardDraft
}: {
  locale: EngineeringLocale;
  tags: TagEngineering[];
  screens?: readonly ScreenEngineering[];
  onSaved?: () => Promise<void>;
  onCreateDashboardDraft?: (draft: ScreenEngineering) => void;
}) {
  const { capabilities, loading: capabilitiesLoading, error: capabilitiesError } = useEffectiveCapabilities();
  const canEngineer = hasWorkspaceCapability(capabilities, 'EngineeringModify');
  const canManage = hasWorkspaceCapability(capabilities, 'AssetProcessIntelligenceManage');
  const canUseTrend = hasRuntimeCapability(capabilities, 'TrendUse');
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [semanticCatalog, setSemanticCatalog] = useState<EngineeringSemanticCatalogResponse | null>(null);
  const [semanticContractVersion, setSemanticContractVersion] = useState(1);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [profiles, setProfiles] = useState<AssetProcessIntelligenceProfileEngineering[]>([]);
  const [assignments, setAssignments] = useState<AssetProcessIntelligenceAssignmentEngineering[]>([]);
  const [processStages, setProcessStages] = useState<AssetProcessIntelligenceProcessStageEngineering[]>([]);
  const [operatingContextProfiles, setOperatingContextProfiles] = useState<AssetProcessIntelligenceOperatingContextProfileEngineering[]>([]);
  const [selectedContextProfileId, setSelectedContextProfileId] = useState('');
  const [contextAssignmentId, setContextAssignmentId] = useState('');
  const [contextPredicateTagId, setContextPredicateTagId] = useState('');
  const [contextPredicateKind, setContextPredicateKind] = useState<AssetProcessIntelligenceOperatingContextPredicateKind>('NumericInterval');
  const [contextPredicateMinimum, setContextPredicateMinimum] = useState('');
  const [contextPredicateMaximum, setContextPredicateMaximum] = useState('');
  const [contextPredicateExact, setContextPredicateExact] = useState('');
  const [contextPredicateBoolean, setContextPredicateBoolean] = useState('true');
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
  const unitCopy = useMemo(() => contextUnitText(locale), [locale]);
  const load = useCallback(async (adoptConfiguration = false) => {
    setLoading(true);
    setError(null);
    try {
      const [response, nextSemanticCatalog] = await Promise.all([fetch(`${API}/api/engineering/asset-process-intelligence/scopes`, {
        headers: { accept: 'application/json' }, credentials: 'include'
      }), loadEngineeringSemanticCatalog()]);
      if (!response.ok) throw await responseError(response);
      const next = await response.json() as Snapshot;
      next.configuration ??= EMPTY_CONFIGURATION;
      next.configuration.scopes ??= [];
      next.configuration.profiles ??= [];
      next.configuration.assignments ??= [];
      next.configuration.processStages ??= [];
      next.configuration.operatingContextProfiles ??= [];
      next.tags ??= [];
      setSnapshot(next);
      setSemanticCatalog(nextSemanticCatalog);
      setSelected(new Set(next.configuration.scopes.map(scopeKey)));
      if (adoptConfiguration) {
        setProfiles(next.configuration.profiles);
        setSemanticContractVersion(next.configuration.profiles.flatMap(profile => profile.signals)
          .reduce((version, signal) => Math.max(version, signal.semanticBinding?.catalogVersion ?? 1), 1));
        setAssignments(next.configuration.assignments);
        setProcessStages(next.configuration.processStages);
        setOperatingContextProfiles(next.configuration.operatingContextProfiles);
        setSelectedContextProfileId(current => next.configuration.operatingContextProfiles.some(item => item.id === current) ? current : next.configuration.operatingContextProfiles[0]?.id ?? '');
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
        body: JSON.stringify({
          // Keep the highest loaded/selected annotation version for this edit session.
          // Clearing the last v2 annotation must still declare an explicit v2 write.
          semanticContractVersion: Math.max(semanticContractVersion, profiles.flatMap(profile => profile.signals)
            .reduce((version, signal) => Math.max(version, signal.semanticBinding?.catalogVersion ?? 1), 1)),
          profiles, assignments, processStages,
          operatingContextProfiles: operatingContextProfiles.map(({ id, key, displayLabel, assignmentId, predicates }) => ({ id, key, displayLabel, assignmentId, predicates })),
          expectedOperatingContextProfileVersions: (snapshot?.configuration.operatingContextProfiles ?? []).map(({ id, version }) => ({ id, version }))
        })
      });
      if (!response.ok) throw await responseError(response);
      setMessage(copy.configurationSaved);
      await onSaved?.();
      await load(true);
    } catch (reason) {
      const detail = reason instanceof Error ? reason.message : String(reason);
      setError(detail === 'INTELLIGENCE_OPERATING_CONTEXT_PROFILE_VERSION_CONFLICT' ? copy.contextVersionConflict : detail);
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
  const dirtyConfiguration = JSON.stringify({ profiles, assignments, processStages, operatingContextProfiles }) !== JSON.stringify({
    profiles: initialConfig.profiles, assignments: initialConfig.assignments, processStages: initialConfig.processStages,
    operatingContextProfiles: initialConfig.operatingContextProfiles
  });
  const semanticAnnotationsComplete = profiles.every(profile => profile.signals.every(signal =>
    !signal.semanticBinding || isSemanticBindingComplete(signal.semanticBinding, semanticCatalog?.semanticCatalogs ?? [])));
  const semanticRoles = (semanticCatalog?.semanticCatalogs ?? []).flatMap(catalog =>
    catalog.roles.map(role => ({ ...role, catalogVersion: catalog.catalogVersion })));
  const selectedProfile = profiles.find(profile => profile.id === selectedProfileId) ?? null;
  const selectedStage = processStages.find(stage => stage.id === selectedStageId) ?? null;
  const selectedContextProfile = operatingContextProfiles.find(profile => profile.id === selectedContextProfileId) ?? null;
  const tags = snapshot?.tags?.length ? snapshot.tags : packageTags;
  const equipment = (snapshot?.resources ?? []).filter(resource => resource.kind === 'Equipment');
  const tagsById = new Map(tags.filter(tag => tag.id).map(tag => [tag.id!.toLowerCase(), tag]));
  const resourcesByKey = new Map((snapshot?.resources ?? []).map(resource => [scopeKey(resource), resource]));
  const profileById = new Map(profiles.map(profile => [profile.id, profile]));
  const assignmentById = new Map(assignments.map(assignment => [assignment.id, assignment]));
  const selectedContextAssignment = selectedContextProfile ? assignmentById.get(selectedContextProfile.assignmentId) ?? null : null;
  const selectedContextMeasurementProfile = selectedContextAssignment ? profileById.get(selectedContextAssignment.profileId) ?? null : null;
  const contextSignalTags = (selectedContextMeasurementProfile?.signals ?? [])
    .map(signal => tagsById.get(signal.tagId.toLowerCase()))
    .filter((tag): tag is TagEngineering => Boolean(tag));
  const selectedPredicateTag = tagsById.get(contextPredicateTagId.toLowerCase());
  const availablePredicateKinds = contextPredicateKinds(selectedPredicateTag?.dataType);

  const createDashboardSourceForAssignment = (assignmentId: string): IntelligenceDashboardSource | null => {
    const assignment = assignmentById.get(assignmentId);
    if (!assignment) return null;
    const equipmentResource = resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`);
    const profile = profileById.get(assignment.profileId);
    if (!equipmentResource || !profile) return null;
    const assignmentSource: IntelligenceDashboardAssignmentSource = {
      equipmentName: equipmentResource.name,
      equipmentPath: equipmentResource.path ?? equipmentResource.scopeId,
      profile
    };
    return {
      kind: 'equipment-profile',
      stableKey: `${assignmentSource.equipmentPath}/${profile.key}`,
      displayName: `${equipmentResource.name} · ${profile.name}`,
      assignments: [assignmentSource]
    };
  };

  const createDashboardSourceForStage = (stage: AssetProcessIntelligenceProcessStageEngineering): IntelligenceDashboardSource | null => {
    const assignmentSources = [...stage.links]
      .sort((left, right) => left.order - right.order || left.assignmentId.localeCompare(right.assignmentId))
      .flatMap(link => {
        const assignment = assignmentById.get(link.assignmentId);
        const equipmentResource = assignment
          ? resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`)
          : undefined;
        const profile = assignment ? profileById.get(assignment.profileId) : undefined;
        return assignment && equipmentResource && profile
          ? [{ equipmentName: equipmentResource.name, equipmentPath: equipmentResource.path ?? equipmentResource.scopeId, profile }]
          : [];
      });
    if (assignmentSources.length === 0) return null;
    return {
      kind: 'process-stage',
      stableKey: stage.key,
      displayName: stage.name,
      assignments: assignmentSources
    };
  };

  const generateDashboard = (source: IntelligenceDashboardSource | null) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed) {
      setError(copy.dashboardLicenseRequired);
      return;
    }
    if (!canEngineer || !canManage) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    if (!source || !onCreateDashboardDraft) {
      setError(copy.dashboardSourceUnavailable);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceDashboardDraft(screens, source, tags, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

  const generatePerformanceScreen = (
    assignment: AssetProcessIntelligenceAssignmentEngineering,
    profile: AssetProcessIntelligenceProfileEngineering,
    equipmentName: string,
    selection: Readonly<{ ruleId: string; ruleVersion: string; signalIds: readonly string[] }>
  ) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    try {
      const draft = generateIntelligencePerformanceScreenDraft(screens, {
        scopeId: assignment.equipmentId,
        assignmentId: assignment.id,
        profileId: profile.id,
        ruleId: selection.ruleId,
        ruleVersion: selection.ruleVersion,
        signalIds: selection.signalIds,
        displayName: equipmentName
      } satisfies IntelligencePerformanceDashboardSelection, locale);
      onCreateDashboardDraft(draft);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

  const generateCoverageScreen = (selection: IntelligenceProfileCoverageDashboardSelection) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceProfileCoverageScreenDraft(screens, selection, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

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

  const updateSignalBinding = (signalId: string, binding: AssetProcessIntelligenceSignalSemanticBinding | null) => {
    if (!selectedProfile) return;
    if (binding) setSemanticContractVersion(current => Math.max(current, binding.catalogVersion));
    updateProfile({ signals: selectedProfile.signals.map(signal => signal.id === signalId ? { ...signal, semanticBinding: binding } : signal) });
  };

  const updateSignalQualifier = (signalId: string, field: keyof AssetProcessIntelligenceSignalSemanticBinding, value: string) => {
    const signal = selectedProfile?.signals.find(item => item.id === signalId);
    if (!signal?.semanticBinding) return;
    updateSignalBinding(signalId, { ...signal.semanticBinding, [field]: value || null });
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
    setOperatingContextProfiles(current => current.filter(item => !removedAssignments.has(item.assignmentId)));
    setSelectedProfileId('');
    setSelectedContextProfileId('');
  };

  const addAssignment = () => {
    if (!assignmentEquipmentId || !assignmentProfileId) return;
    const id = crypto.randomUUID();
    setAssignments(current => [...current, { id, equipmentId: assignmentEquipmentId, profileId: assignmentProfileId }]);
    setAssignmentEquipmentId('');
    setAssignmentProfileId('');
  };

  const createContextProfile = () => {
    const assignment = assignmentById.get(contextAssignmentId);
    if (!assignment) return;
    const nextNumber = operatingContextProfiles.length + 1;
    const profile: AssetProcessIntelligenceOperatingContextProfileEngineering = {
      id: crypto.randomUUID(), key: `context-${nextNumber}`, displayLabel: `${copy.defaultContext} ${nextNumber}`,
      assignmentId: assignment.id, version: 1, predicates: []
    };
    setOperatingContextProfiles(current => [...current, profile]);
    setSelectedContextProfileId(profile.id);
    setContextPredicateTagId('');
    setContextAssignmentId('');
  };

  const updateContextProfile = (changes: Partial<AssetProcessIntelligenceOperatingContextProfileEngineering>) => {
    if (!selectedContextProfile) return;
    setOperatingContextProfiles(current => current.map(profile => profile.id === selectedContextProfile.id
      ? { ...profile, ...changes }
      : profile));
  };

  const updateContextPredicate = (tagId: string, changes: Partial<AssetProcessIntelligenceOperatingContextPredicateEngineering>) => {
    if (!selectedContextProfile) return;
    updateContextProfile({ predicates: selectedContextProfile.predicates.map(predicate =>
      predicate.tagId === tagId ? { ...predicate, ...changes } : predicate) });
  };

  const addContextPredicate = () => {
    if (!selectedContextProfile || !contextPredicateTagId ||
        selectedContextProfile.predicates.length >= 16 ||
        selectedContextProfile.predicates.some(item => item.tagId === contextPredicateTagId)) return;
    const tag = tagsById.get(contextPredicateTagId.toLowerCase());
    if (!tag) return;
    const predicate: AssetProcessIntelligenceOperatingContextPredicateEngineering = { tagId: tag.id!, kind: contextPredicateKind };
    if (contextPredicateKind === 'NumericInterval') {
      predicate.minimum = contextPredicateMinimum.trim() === '' ? null : Number(contextPredicateMinimum);
      predicate.maximum = contextPredicateMaximum.trim() === '' ? null : Number(contextPredicateMaximum);
      predicate.engineeringUnit = tag.engineeringUnit ?? null;
      predicate.engineeringUnitConfirmed = true;
    } else if (contextPredicateKind === 'BooleanEquals') {
      predicate.booleanValue = contextPredicateBoolean === 'true';
    } else {
      predicate.exactValue = contextPredicateExact;
    }
    updateContextProfile({ predicates: [...selectedContextProfile.predicates, predicate] });
    setContextPredicateTagId('');
    setContextPredicateMinimum('');
    setContextPredicateMaximum('');
    setContextPredicateExact('');
  };

  const removeContextProfile = () => {
    if (!selectedContextProfile) return;
    setOperatingContextProfiles(current => current.filter(item => item.id !== selectedContextProfile.id));
    setSelectedContextProfileId('');
  };

  const removeContextPredicate = (tagId: string) => {
    if (!selectedContextProfile) return;
    updateContextProfile({ predicates: selectedContextProfile.predicates.filter(predicate => predicate.tagId !== tagId) });
  };

  const removeAssignment = (id: string) => {
    setAssignments(current => current.filter(item => item.id !== id));
    setOperatingContextProfiles(current => current.filter(item => item.assignmentId !== id));
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
          <button type="button" disabled={!canEngineer || !dirtyConfiguration || configSaving || !semanticAnnotationsComplete} onClick={() => void saveConfiguration()}>
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
                  const binding = signal.semanticBinding;
                  return <article className="asset-process-intelligence__row" key={signal.id}>
                    <span><strong>{signal.key} · {signal.signalRole}</strong><small>{tag ? `${tag.path} · ${tag.dataType}${tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}${tag.controlledEngineeringUnit ? ` · ${tag.controlledEngineeringUnit.code}` : ''}` : `${copy.missingTag}: ${signal.tagId}`}</small></span>
                    <span className={tag ? '' : 'asset-process-intelligence__missing'}>{signal.required ? copy.required : copy.optional}</span>
                    <div className="asset-process-intelligence__semantic-fields">
                      <label><span>{copy.semanticRole}</span><select
                        aria-label={`${copy.semanticRole}: ${signal.key}`}
                        data-testid={`intelligence-semantic-role-${signal.key}`}
                        value={binding ? semanticRoleOptionValue(binding.catalogVersion, binding.roleCode) : ''}
                        disabled={!canEngineer}
                        onChange={event => {
                          const selectedRole = semanticRoles.find(role =>
                            semanticRoleOptionValue(role.catalogVersion, role.code) === event.currentTarget.value);
                          updateSignalBinding(signal.id, selectedRole
                            ? emptySemanticBinding(selectedRole.catalogVersion, selectedRole.code)
                            : null);
                        }}
                      >
                        <option value="">{copy.noSemanticRole}</option>
                        {binding && !semanticRoles.some(role => role.code === binding.roleCode && role.catalogVersion === binding.catalogVersion) ? <option value={semanticRoleOptionValue(binding.catalogVersion, binding.roleCode)}>{copy.unsupportedSemantic}: v{binding.catalogVersion} · {binding.roleCode}</option> : null}
                        {semanticRoles.map(role => <option key={semanticRoleOptionValue(role.catalogVersion, role.code)} value={semanticRoleOptionValue(role.catalogVersion, role.code)}>{role.code} · v{role.catalogVersion}</option>)}
                      </select></label>
                      {binding ? semanticQualifierFields(binding, semanticCatalog?.semanticCatalogs ?? []).map(field => <label key={field.key}><span>{copy[field.label]}</span><select
                        aria-label={`${copy[field.label]}: ${signal.key}`}
                        data-testid={`intelligence-semantic-${field.key}-${signal.key}`}
                        value={binding[field.key] ?? ''}
                        disabled={!canEngineer}
                        onChange={event => updateSignalQualifier(signal.id, field.key, event.currentTarget.value)}
                      ><option value="">{copy.chooseQualifier}</option>{field.options.map(option => <option key={option} value={option}>{option}</option>)}</select></label>) : null}
                      {binding ? <small className={isSemanticBindingComplete(binding, semanticCatalog?.semanticCatalogs ?? []) ? '' : 'asset-process-intelligence__missing'}>{isSemanticBindingComplete(binding, semanticCatalog?.semanticCatalogs ?? []) ? copy.semanticComplete : copy.semanticIncomplete}</small> : null}
                    </div>
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
              const dashboardSource = createDashboardSourceForAssignment(assignment.id);
              const matchingAssignments = assignments.filter(item =>
                item.equipmentId.toLowerCase() === assignment.equipmentId.toLowerCase() &&
                item.profileId.toLowerCase() === assignment.profileId.toLowerCase());
              const matchingScopes = initialConfig.scopes.filter(scope =>
                String(scope.kind).toLowerCase() === 'equipment' &&
                scope.scopeId.toLowerCase() === assignment.equipmentId.toLowerCase());
              const coverageScope = matchingScopes.length === 1 ? matchingScopes[0] : null;
              const coverageBlockedReason = capabilitiesLoading ? 'capabilities-loading'
                : capabilitiesError || !capabilities ? 'authority-unavailable'
                  : !snapshot.licensed ? 'license-required'
                    : !canUseTrend ? 'trend-use-required'
                      : matchingAssignments.length !== 1 ? 'assignment-ambiguous'
                        : matchingScopes.length !== 1 ? 'scope-unavailable'
                          : !coverageScope?.active ? 'scope-inactive'
                            : !coverageScope.executionArmed ? 'permit-required'
                            : dirtyConfiguration ? 'configuration-unsaved'
                              : dirtySelection ? 'scope-selection-unsaved'
                                : snapshot.execution.workspaceDirty ? 'workspace-dirty'
                                  : !snapshot.execution.projectKey || snapshot.execution.activeRevision === null ? 'project-unavailable'
                                    : !profile?.signals.length ? 'signals-required'
                                      : new Set(profile.signals.map(signal => signal.tagId.toLowerCase())).size !== profile.signals.length
                                        ? 'duplicate-tags'
                                        : null;
              return <article className="asset-process-intelligence__row" key={assignment.id}>
                <span><strong>{equipmentResource?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</strong><small>{equipmentResource?.path ?? assignment.equipmentId} · {profile?.key ?? assignment.profileId}</small></span>
                {(!equipmentResource || !profile) ? <span className="asset-process-intelligence__missing">{copy.referenceMissing}</span> : null}
                <button type="button" className="secondary" data-testid={`intelligence-dashboard-assignment-${assignment.id}`}
                  disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft || !dashboardSource}
                  onClick={() => generateDashboard(dashboardSource)}>{copy.generateDashboard}</button>
                {equipmentResource && profile ? <AssetProcessIntelligenceCoveragePanel
                  locale={locale}
                  scopeId={assignment.equipmentId}
                  profileId={assignment.profileId}
                  projectKey={snapshot.execution.projectKey}
                  activeRevision={snapshot.execution.activeRevision}
                  canQuery={!coverageBlockedReason}
                  blockedReason={coverageBlockedReason}
                  onCreateRuntimeScreenDraft={snapshot.licensed && canEngineer && canManage && onCreateDashboardDraft
                    ? () => generateCoverageScreen({
                        scopeId: assignment.equipmentId,
                        profileId: profile.id,
                        displayName: `${equipmentResource.name} · ${profile.name}`
                      })
                    : undefined}
                /> : null}
                {equipmentResource && profile ? <AssetProcessIntelligencePerformancePanel
                  locale={locale}
                  scopeId={assignment.equipmentId}
                  assignmentId={assignment.id}
                  profile={profile}
                  tags={tags}
                  semanticCatalogs={semanticCatalog?.semanticCatalogs ?? []}
                  projectKey={snapshot.execution.projectKey}
                  activeRevision={snapshot.execution.activeRevision}
                  canQuery={!coverageBlockedReason}
                  onCreateRuntimeScreenDraft={snapshot.licensed && canEngineer && canManage && onCreateDashboardDraft
                    ? selection => generatePerformanceScreen(assignment, profile, equipmentResource.name, selection)
                    : undefined}
                /> : null}
                <button type="button" className="secondary" disabled={!canEngineer} onClick={() => removeAssignment(assignment.id)}>{copy.remove}</button>
              </article>;
            })}
          </div>}
        </section>

        <section className="asset-process-intelligence__authoring-section" aria-labelledby="intelligence-context-profiles-heading" data-testid="intelligence-context-profiles">
          <div className="asset-process-intelligence__section-heading"><div><h3 id="intelligence-context-profiles-heading">{copy.contextProfiles}</h3><p>{copy.contextProfilesHint}</p></div></div>
          {assignments.length === 0 ? <div className="eng-empty"><strong>{copy.noAssignments}</strong></div> : <div className="asset-process-intelligence__assignment-form">
            <label>{copy.selectedAssignment}<select value={contextAssignmentId} disabled={!canEngineer || !canManage} onChange={event => setContextAssignmentId(event.currentTarget.value)}>
              <option value="">{copy.chooseAssignment}</option>
              {assignments.map(assignment => {
                const equip = resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`);
                const profile = profileById.get(assignment.profileId);
                return <option key={assignment.id} value={assignment.id}>{equip?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</option>;
              })}
            </select></label>
            <button type="button" className="secondary" disabled={!canEngineer || !canManage || !contextAssignmentId} onClick={createContextProfile}>{copy.newContextProfile}</button>
          </div>}
          {!canManage ? <p role="note">{copy.contextAuthorizationHint}</p> : null}
          {operatingContextProfiles.length === 0 ? <div className="eng-empty"><strong>{copy.noContextProfiles}</strong></div> : <div className="asset-process-intelligence__split">
            <div className="asset-process-intelligence__choice-list" role="listbox" aria-label={copy.contextProfiles}>
              {operatingContextProfiles.map(profile => {
                const assignment = assignmentById.get(profile.assignmentId);
                const equip = assignment ? resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`) : undefined;
                return <button type="button" role="option" aria-selected={profile.id === selectedContextProfileId} key={profile.id} onClick={() => setSelectedContextProfileId(profile.id)}>
                  <strong>{profile.displayLabel}</strong><small>{profile.key} · v{profile.version} · {equip?.name ?? copy.missingEquipment}</small>
                </button>;
              })}
            </div>
            {selectedContextProfile ? <div className="asset-process-intelligence__editor">
              <div className="asset-process-intelligence__field-grid">
                <label>{copy.contextKey}<input value={selectedContextProfile.key} disabled={!canEngineer || !canManage} onChange={event => updateContextProfile({ key: event.currentTarget.value })}/></label>
                <label>{copy.contextLabel}<input value={selectedContextProfile.displayLabel} disabled={!canEngineer || !canManage} onChange={event => updateContextProfile({ displayLabel: event.currentTarget.value })}/></label>
              </div>
              <p className="asset-process-intelligence__version">{copy.savedProfileVersion}: v{snapshot?.configuration.operatingContextProfiles.find(item => item.id === selectedContextProfile.id)?.version ?? copy.newProfile}</p>
              <div className="asset-process-intelligence__section-heading"><div><h4>{copy.contextPredicates}</h4><p>{copy.contextPredicatesHint}</p><p>{unitCopy.policy}</p></div></div>
              {selectedContextProfile.predicates.length === 0 ? <div className="eng-empty"><strong>{copy.noContextPredicates}</strong></div> : <div className="asset-process-intelligence__list">
                {selectedContextProfile.predicates.map(predicate => {
                  const tag = tagsById.get(predicate.tagId.toLowerCase());
                  const numericUnitConfirmed = predicate.kind !== 'NumericInterval' || (!!tag &&
                    predicate.engineeringUnitConfirmed === true &&
                    (predicate.engineeringUnit ?? null) === (tag?.engineeringUnit ?? null));
                  return <article className="asset-process-intelligence__row" key={predicate.tagId}>
                    <span><strong>{tag?.path ?? copy.missingTag}</strong><small>{tag ? `${tag.dataType}${tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}` : predicate.tagId}</small></span>
                    {predicate.kind === 'NumericInterval' ? <div className="asset-process-intelligence__field-grid">
                      <label>{copy.minimumInclusive}<input type="number" value={predicate.minimum ?? ''} disabled={!canEngineer || !canManage || !numericUnitConfirmed} onChange={event => updateContextPredicate(predicate.tagId, { minimum: event.currentTarget.value === '' ? null : Number(event.currentTarget.value) })}/></label>
                      <label>{copy.maximumInclusive}<input type="number" value={predicate.maximum ?? ''} disabled={!canEngineer || !canManage || !numericUnitConfirmed} onChange={event => updateContextPredicate(predicate.tagId, { maximum: event.currentTarget.value === '' ? null : Number(event.currentTarget.value) })}/></label>
                    </div> : predicate.kind === 'BooleanEquals' ? <label>{copy.predicateValue}<select value={String(predicate.booleanValue ?? true)} disabled={!canEngineer || !canManage} onChange={event => updateContextPredicate(predicate.tagId, { booleanValue: event.currentTarget.value === 'true' })}><option value="true">{copy.matchTrue}</option><option value="false">{copy.matchFalse}</option></select></label> : <label>{copy.exactValue}<input value={predicate.exactValue ?? ''} disabled={!canEngineer || !canManage} onChange={event => updateContextPredicate(predicate.tagId, { exactValue: event.currentTarget.value })}/></label>}
                    {predicate.kind === 'NumericInterval' ? <>
                      <small role="status">{numericUnitConfirmed
                        ? `${unitCopy.confirmed}: ${predicate.engineeringUnit ?? unitCopy.unitless}`
                        : `${unitCopy.unconfirmed}: ${unitCopy.storedUnit} ${predicate.engineeringUnit ?? unitCopy.unitless}; ${unitCopy.currentUnit} ${tag?.engineeringUnit ?? unitCopy.unitless}`}</small>
                      {!numericUnitConfirmed ? <button type="button" className="secondary" disabled={!canEngineer || !canManage || !tag} onClick={() => tag && updateContextPredicate(predicate.tagId, {
                        engineeringUnit: tag.engineeringUnit ?? null,
                        engineeringUnitConfirmed: true
                      })}>{unitCopy.confirm}</button> : null}
                    </> : null}
                    <button type="button" className="secondary" disabled={!canEngineer || !canManage} onClick={() => removeContextPredicate(predicate.tagId)}>{copy.remove}</button>
                  </article>;
                })}
              </div>}
              <div className="asset-process-intelligence__assignment-form">
                <label>{copy.predicateTag}<select value={contextPredicateTagId} disabled={!canEngineer || !canManage || !selectedContextMeasurementProfile || selectedContextProfile.predicates.length >= 16} onChange={event => {
                  const nextId = event.currentTarget.value;
                  const nextTag = tagsById.get(nextId.toLowerCase());
                  const nextKinds = contextPredicateKinds(nextTag?.dataType);
                  setContextPredicateTagId(nextId);
                  if (nextKinds.length) setContextPredicateKind(nextKinds[0]);
                }}>
                  <option value="">{copy.choosePredicateTag}</option>
                  {contextSignalTags.filter(tag => !selectedContextProfile.predicates.some(predicate => predicate.tagId.toLowerCase() === tag.id?.toLowerCase()) && contextPredicateKinds(tag.dataType).length > 0).map(tag => <option key={tag.id} value={tag.id}>{tag.path} · {tag.dataType}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</option>)}
                </select></label>
                {selectedPredicateTag ? <>
                  <label>{copy.predicateKind}<select value={contextPredicateKind} disabled={!canEngineer || !canManage} onChange={event => setContextPredicateKind(event.currentTarget.value as AssetProcessIntelligenceOperatingContextPredicateKind)}>
                    {availablePredicateKinds.map(kind => <option key={kind} value={kind}>{copy[kind]}</option>)}
                  </select></label>
                  {contextPredicateKind === 'NumericInterval' ? <>
                    <label>{copy.minimumInclusive}<input type="number" value={contextPredicateMinimum} disabled={!canEngineer || !canManage} onChange={event => setContextPredicateMinimum(event.currentTarget.value)}/></label>
                    <label>{copy.maximumInclusive}<input type="number" value={contextPredicateMaximum} disabled={!canEngineer || !canManage} onChange={event => setContextPredicateMaximum(event.currentTarget.value)}/></label>
                  </> : contextPredicateKind === 'BooleanEquals' ? <label>{copy.predicateValue}<select value={contextPredicateBoolean} disabled={!canEngineer || !canManage} onChange={event => setContextPredicateBoolean(event.currentTarget.value)}><option value="true">{copy.matchTrue}</option><option value="false">{copy.matchFalse}</option></select></label> : <label>{copy.exactValue}<input value={contextPredicateExact} disabled={!canEngineer || !canManage} onChange={event => setContextPredicateExact(event.currentTarget.value)}/></label>}
                </> : null}
                <button type="button" className="secondary" disabled={!canEngineer || !canManage || !selectedPredicateTag || selectedContextProfile.predicates.length >= 16} onClick={addContextPredicate}>{copy.addPredicate}</button>
              </div>
              <button type="button" className="secondary asset-process-intelligence__danger" disabled={!canEngineer || !canManage} onClick={removeContextProfile}>{copy.deleteContextProfile}</button>
            </div> : null}
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
              <button type="button" className="secondary" data-testid={`intelligence-dashboard-stage-${selectedStage.id}`}
                disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft || !createDashboardSourceForStage(selectedStage)}
                onClick={() => generateDashboard(createDashboardSourceForStage(selectedStage))}>{copy.generateStageDashboard}</button>
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
        <section className="asset-process-intelligence__authoring-section" aria-labelledby="intelligence-dashboard-heading" data-testid="intelligence-dashboard-generator">
          <div className="asset-process-intelligence__section-heading"><div><h3 id="intelligence-dashboard-heading">{copy.dashboardTitle}</h3><p>{copy.dashboardHint}</p></div></div>
          {!snapshot.licensed ? <p role="note">{copy.dashboardLicenseRequired}</p> : !canEngineer || !canManage ? <p role="note">{copy.dashboardAuthorizationHint}</p> : null}
        </section>
        <footer className="asset-process-intelligence__authoring-footer">
          <span>{copy.staticOnly}</span>
          <button type="button" disabled={!canEngineer || !dirtyConfiguration || configSaving || !semanticAnnotationsComplete} onClick={() => void saveConfiguration()}>
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

function contextPredicateKinds(dataType: string | undefined): AssetProcessIntelligenceOperatingContextPredicateKind[] {
  switch (dataType) {
    case 'Int16': case 'Int32': case 'Int64': case 'Float': case 'Double': return ['NumericInterval'];
    case 'Boolean': return ['BooleanEquals'];
    case 'String': return ['StringEquals'];
    case 'Enum': return ['EnumEquals'];
    default: return [];
  }
}

type SemanticQualifierKey = 'currentBasis' | 'signalKind' | 'frequencyReference' | 'pressureReference' | 'flowDirection';
type SemanticQualifierLabel = 'currentBasisLabel' | 'signalKindLabel' | 'frequencyReferenceLabel' | 'pressureReferenceLabel' | 'flowDirectionLabel';
function semanticRoleOptionValue(catalogVersion: number, roleCode: string): string {
  return `${catalogVersion}:${roleCode}`;
}

function emptySemanticBinding(catalogVersion: number, roleCode: string): AssetProcessIntelligenceSignalSemanticBinding {
  return { catalogVersion, roleCode, currentBasis: null, signalKind: null, frequencyReference: null, pressureReference: null, flowDirection: null };
}

function semanticQualifierFields(
  binding: AssetProcessIntelligenceSignalSemanticBinding,
  catalogs: readonly VersionedSemanticCatalog[]
): Array<{ key: SemanticQualifierKey; label: SemanticQualifierLabel; options: readonly string[] }> {
  const role = catalogs.find(catalog => catalog.catalogVersion === binding.catalogVersion)?.roles
    .find(candidate => candidate.code === binding.roleCode);
  if (!role) return [];
  return role.qualifiers.flatMap(qualifier => {
    const key = qualifier.name as SemanticQualifierKey;
    const label = qualifierLabel(key);
    return label ? [{ key, label, options: qualifier.allowedValues }] : [];
  });
}

function isSemanticBindingComplete(
  binding: AssetProcessIntelligenceSignalSemanticBinding | null | undefined,
  catalogs: readonly VersionedSemanticCatalog[]
): boolean {
  if (!binding) return false;
  const fields = semanticQualifierFields(binding, catalogs);
  const role = catalogs.find(catalog => catalog.catalogVersion === binding.catalogVersion)?.roles
    .find(candidate => candidate.code === binding.roleCode);
  if (!role) return false;
  const applicable = new Set(fields.map(field => field.key));
  const allKeys: SemanticQualifierKey[] = ['currentBasis', 'signalKind', 'frequencyReference', 'pressureReference', 'flowDirection'];
  return fields.every(field => field.options.includes(binding[field.key] ?? '')) &&
    allKeys.every(key => applicable.has(key) || !binding[key]);
}

function qualifierLabel(key: SemanticQualifierKey): SemanticQualifierLabel | null {
  switch (key) {
    case 'currentBasis': return 'currentBasisLabel';
    case 'signalKind': return 'signalKindLabel';
    case 'frequencyReference': return 'frequencyReferenceLabel';
    case 'pressureReference': return 'pressureReferenceLabel';
    case 'flowDirection': return 'flowDirectionLabel';
    default: return null;
  }
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
    profiles: 'Reusable profiles', profilesHint: 'A profile can be reused by multiple Equipment assignments.', newProfile: 'New profile', noProfiles: 'No profiles yet. Create one to define static signal references.', defaultProfile: 'Profile', defaultSignal: 'Signal', profileKey: 'Stable key', profileName: 'Display name', signals: 'Signal references', signalsHint: 'References are by TAG ID; legacy role and unit text stay unchanged. Controlled semantics are opt-in.', noSignals: 'No signals in this profile.', signalKey: 'Signal key', signalKeyPlaceholder: 'e.g. temperature', signalRole: 'Legacy domain role text', semanticRole: 'Controlled semantic role', noSemanticRole: 'No controlled role', chooseQualifier: 'Choose a qualifier', currentBasisLabel: 'Current basis', signalKindLabel: 'Signal kind', frequencyReferenceLabel: 'Frequency reference', pressureReferenceLabel: 'Pressure reference', flowDirectionLabel: 'Flow direction', semanticComplete: 'Controlled annotation complete', semanticIncomplete: 'Incomplete: select every required qualifier before saving', unsupportedSemantic: 'Unsupported stored role', tagReference: 'TAG reference', chooseTag: 'Choose a TAG', noTags: 'No stable TAGs available', addSignal: 'Add signal', required: 'Required', optional: 'Optional', missingTag: 'Missing TAG reference', remove: 'Remove', deleteProfile: 'Delete profile and its assignments',
    assignments: 'Equipment assignments', assignmentsHint: 'Assign a reusable profile to each stable Equipment identity. One Equipment may have multiple profile assignments.', equipment: 'Equipment', profile: 'Profile', chooseEquipment: 'Choose Equipment', chooseProfile: 'Choose a profile', assignProfile: 'Assign profile', noEquipment: 'No stable Equipment is available.', noAssignments: 'No assignments yet.', missingEquipment: 'Missing Equipment', missingProfile: 'Missing profile', referenceMissing: 'Reference is missing',
    processStages: 'Ordered process stages', processStagesHint: 'Arrange assignment links into stages; order is explicit and stable.', newStage: 'New stage', noStages: 'No process stages yet.', defaultStage: 'Stage', stageKey: 'Stable key', stageName: 'Display name', noStageAssignments: 'No assignments in this stage.', stageAssignment: 'Assignment', chooseAssignment: 'Choose an assignment', addToStage: 'Add to stage', moveUp: 'Move up', moveDown: 'Move down', staticOnly: 'Configuration-only: no TAG reads, samples, Historian queries or inferred values are stored.', saveConfiguration: 'Save process configuration', configurationSaved: 'Process configuration saved to Working.',
    dashboardTitle: 'Guided dashboard draft', dashboardHint: 'Generate an ordinary editable screen from a profile or process stage. Generation uses TAG metadata only; it does not read live values or Historian and does not invent KPIs. The draft opens in the canonical screen editor and saves only through its existing Preview/Apply flow.', generateDashboard: 'Create screen draft', generateStageDashboard: 'Create stage screen', dashboardLicenseRequired: 'The Asset & Process Intelligence license is required to generate this screen.', dashboardAuthorizationHint: 'EngineeringModify and AssetProcessIntelligenceManage are required to generate a screen from these profiles.', dashboardSourceUnavailable: 'Choose a complete profile or process stage with at least one resolvable numeric TAG.',
    contextProfiles: 'Operating-context profiles', contextProfilesHint: 'Bind every context to one Equipment assignment. Eligibility queries are separate, read-only and use the exact saved profile version.', newContextProfile: 'New context', noContextProfiles: 'No operating contexts yet.', defaultContext: 'Context', contextKey: 'Stable key', contextLabel: 'Operator label', selectedAssignment: 'Equipment assignment', contextAuthorizationHint: 'EngineeringModify and a resource-scoped AssetProcessIntelligenceManage grant are required to edit contexts.', contextPredicates: 'Required predicates', contextPredicatesHint: 'All predicates must match at the same exact synchronized UTC source timestamp. Numeric bounds use the current Engineering unit.', noContextPredicates: 'Add at least one required predicate before saving.', predicateTag: 'TAG from assigned measurement profile', choosePredicateTag: 'Choose a profile TAG', predicateKind: 'Predicate type', NumericInterval: 'Inclusive numeric interval', BooleanEquals: 'Boolean equals', StringEquals: 'Exact String', EnumEquals: 'Exact Enum', minimumInclusive: 'Minimum (inclusive)', maximumInclusive: 'Maximum (inclusive)', predicateValue: 'Required value', exactValue: 'Exact ordinal value', matchTrue: 'True', matchFalse: 'False', addPredicate: 'Add predicate', deleteContextProfile: 'Delete context profile', savedProfileVersion: 'Server profile version', contextVersionConflict: 'This context profile changed elsewhere. Refresh and review the latest version before saving.'
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
    profiles: 'Perfiles reutilizables', profilesHint: 'Un perfil puede reutilizarse en varias asignaciones de equipos.', newProfile: 'Nuevo perfil', noProfiles: 'Aún no hay perfiles. Cree uno para definir referencias estáticas.', defaultProfile: 'Perfil', defaultSignal: 'Señal', profileKey: 'Clave estable', profileName: 'Nombre', signals: 'Referencias de señales', signalsHint: 'Las referencias usan el ID TAG; el texto de rol y unidad se conserva. La semántica controlada es opcional.', noSignals: 'Este perfil no tiene señales.', signalKey: 'Clave de señal', signalKeyPlaceholder: 'p. ej. temperatura', signalRole: 'Texto de rol heredado', semanticRole: 'Rol semántico controlado', noSemanticRole: 'Sin rol controlado', chooseQualifier: 'Elija un calificador', currentBasisLabel: 'Base de corriente', signalKindLabel: 'Tipo de señal', frequencyReferenceLabel: 'Referencia de frecuencia', pressureReferenceLabel: 'Referencia de presión', flowDirectionLabel: 'Dirección del flujo', semanticComplete: 'Anotación controlada completa', semanticIncomplete: 'Incompleta: seleccione todos los calificadores requeridos antes de guardar', unsupportedSemantic: 'Rol almacenado no compatible', tagReference: 'Referencia TAG', chooseTag: 'Elija un TAG', noTags: 'No hay TAGs estables', addSignal: 'Añadir señal', required: 'Obligatoria', optional: 'Opcional', missingTag: 'Falta la referencia TAG', remove: 'Eliminar', deleteProfile: 'Eliminar perfil y sus asignaciones',
    assignments: 'Asignaciones de equipo', assignmentsHint: 'Asigne un perfil reutilizable a cada identidad estable de equipo. Un equipo puede tener varios perfiles.', equipment: 'Equipo', profile: 'Perfil', chooseEquipment: 'Elija un equipo', chooseProfile: 'Elija un perfil', assignProfile: 'Asignar perfil', noEquipment: 'No hay equipos estables.', noAssignments: 'Aún no hay asignaciones.', missingEquipment: 'Equipo inexistente', missingProfile: 'Perfil inexistente', referenceMissing: 'Falta la referencia',
    processStages: 'Etapas ordenadas', processStagesHint: 'Organice los enlaces de asignación; el orden es explícito y estable.', newStage: 'Nueva etapa', noStages: 'Aún no hay etapas.', defaultStage: 'Etapa', stageKey: 'Clave estable', stageName: 'Nombre', noStageAssignments: 'No hay asignaciones en esta etapa.', stageAssignment: 'Asignación', chooseAssignment: 'Elija una asignación', addToStage: 'Añadir a etapa', moveUp: 'Mover arriba', moveDown: 'Mover abajo', staticOnly: 'Solo configuración: no se leen TAGs, valores, Historian ni se guardan inferencias.', saveConfiguration: 'Guardar configuración', configurationSaved: 'Configuración guardada en Working.',
    dashboardTitle: 'Borrador de panel guiado', dashboardHint: 'Genere una pantalla editable normal desde un perfil o una etapa del proceso. La generación usa solo metadatos TAG; no lee valores en vivo ni Historian y no inventa KPI. El borrador abre en el editor canónico y solo se guarda mediante su flujo Preview/Apply.', generateDashboard: 'Crear borrador de pantalla', generateStageDashboard: 'Crear pantalla de etapa', dashboardLicenseRequired: 'Se requiere la licencia de Inteligencia de Activos y Procesos para generar esta pantalla.', dashboardAuthorizationHint: 'Se requieren EngineeringModify y AssetProcessIntelligenceManage para generar una pantalla desde estos perfiles.', dashboardSourceUnavailable: 'Elija un perfil o etapa completa con al menos un TAG numérico resoluble.',
    contextProfiles: 'Perfiles de contexto operativo', contextProfilesHint: 'Vincule cada contexto a una asignación de equipo. Las consultas de elegibilidad son independientes, de solo lectura y usan la versión exacta del perfil.', newContextProfile: 'Nuevo contexto', noContextProfiles: 'Aún no hay contextos operativos.', defaultContext: 'Contexto', contextKey: 'Clave estable', contextLabel: 'Etiqueta del operador', selectedAssignment: 'Asignación de equipo', contextAuthorizationHint: 'Se requieren EngineeringModify y AssetProcessIntelligenceManage con alcance de recurso para editar contextos.', contextPredicates: 'Predicados obligatorios', contextPredicatesHint: 'Todos los predicados deben coincidir en la misma marca UTC de origen, sincronizada y exacta. Los límites numéricos usan la unidad actual de Engineering.', noContextPredicates: 'Añada al menos un predicado obligatorio antes de guardar.', predicateTag: 'TAG del perfil de medición asignado', choosePredicateTag: 'Elija un TAG del perfil', predicateKind: 'Tipo de predicado', NumericInterval: 'Intervalo numérico inclusivo', BooleanEquals: 'Igualdad booleana', StringEquals: 'String exacto', EnumEquals: 'Enum exacto', minimumInclusive: 'Mínimo (inclusivo)', maximumInclusive: 'Máximo (inclusivo)', predicateValue: 'Valor obligatorio', exactValue: 'Valor ordinal exacto', matchTrue: 'Verdadero', matchFalse: 'Falso', addPredicate: 'Añadir predicado', deleteContextProfile: 'Eliminar perfil de contexto', savedProfileVersion: 'Versión guardada por el servidor', contextVersionConflict: 'Este perfil cambió en otro lugar. Actualice y revise la versión más reciente antes de guardar.'
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
    profiles: 'Perfis reutilizáveis', profilesHint: 'Um perfil pode ser reutilizado em várias associações de equipamentos.', newProfile: 'Novo perfil', noProfiles: 'Nenhum perfil ainda. Crie um para definir referências estáticas de sinais.', defaultProfile: 'Perfil', defaultSignal: 'Sinal', profileKey: 'Chave estável', profileName: 'Nome de exibição', signals: 'Referências de sinais', signalsHint: 'As referências usam o ID do TAG; textos livres de papel e unidade são preservados. Semântica controlada é opcional.', noSignals: 'Este perfil não possui sinais.', signalKey: 'Chave do sinal', signalKeyPlaceholder: 'ex.: temperatura', signalRole: 'Texto de papel legado', semanticRole: 'Papel semântico controlado', noSemanticRole: 'Sem papel controlado', chooseQualifier: 'Escolha um qualificador', currentBasisLabel: 'Base de corrente', signalKindLabel: 'Tipo do sinal', frequencyReferenceLabel: 'Referência de frequência', pressureReferenceLabel: 'Referência de pressão', flowDirectionLabel: 'Direção do fluxo', semanticComplete: 'Anotação controlada completa', semanticIncomplete: 'Incompleta: selecione todos os qualificadores obrigatórios antes de salvar', unsupportedSemantic: 'Papel armazenado não compatível', tagReference: 'Referência do TAG', chooseTag: 'Escolha um TAG', noTags: 'Nenhum TAG estável disponível', addSignal: 'Adicionar sinal', required: 'Obrigatório', optional: 'Opcional', missingTag: 'Referência de TAG ausente', remove: 'Remover', deleteProfile: 'Excluir perfil e associações',
    assignments: 'Associações de equipamentos', assignmentsHint: 'Associe um perfil reutilizável a cada identidade estável de equipamento. Um equipamento pode usar vários perfis.', equipment: 'Equipamento', profile: 'Perfil', chooseEquipment: 'Escolha um equipamento', chooseProfile: 'Escolha um perfil', assignProfile: 'Associar perfil', noEquipment: 'Nenhum equipamento estável disponível.', noAssignments: 'Nenhuma associação ainda.', missingEquipment: 'Equipamento ausente', missingProfile: 'Perfil ausente', referenceMissing: 'Referência ausente',
    processStages: 'Etapas ordenadas do processo', processStagesHint: 'Organize associações nas etapas; a ordem é explícita e estável.', newStage: 'Nova etapa', noStages: 'Nenhuma etapa de processo ainda.', defaultStage: 'Etapa', stageKey: 'Chave estável', stageName: 'Nome de exibição', noStageAssignments: 'Nenhuma associação nesta etapa.', stageAssignment: 'Associação', chooseAssignment: 'Escolha uma associação', addToStage: 'Adicionar à etapa', moveUp: 'Mover para cima', moveDown: 'Mover para baixo', staticOnly: 'Somente configuração: sem leitura de TAG, valores, consultas ao Historian ou inferências.', saveConfiguration: 'Salvar configuração de processo', configurationSaved: 'Configuração de processo salva no Working.',
    dashboardTitle: 'Rascunho guiado de tela', dashboardHint: 'Gere uma tela normal e editável a partir de um perfil ou etapa do processo. A geração usa somente metadados dos TAGs; não lê valores ao vivo nem Historian e não inventa KPIs. O rascunho abre no editor canônico e só é salvo pelo fluxo existente de Preview/Apply.', generateDashboard: 'Criar rascunho de tela', generateStageDashboard: 'Criar tela da etapa', dashboardLicenseRequired: 'A licença de Inteligência de Ativos e Processos é necessária para gerar esta tela.', dashboardAuthorizationHint: 'EngineeringModify e AssetProcessIntelligenceManage são necessários para gerar telas a partir destes perfis.', dashboardSourceUnavailable: 'Escolha um perfil ou etapa completa com pelo menos um TAG numérico resolvível.',
    contextProfiles: 'Perfis de contexto operacional', contextProfilesHint: 'Vincule cada contexto a uma associação de equipamento. Consultas de elegibilidade são separadas, somente leitura e usam a versão exata do perfil.', newContextProfile: 'Novo contexto', noContextProfiles: 'Nenhum contexto operacional ainda.', defaultContext: 'Contexto', contextKey: 'Chave estável', contextLabel: 'Rótulo do operador', selectedAssignment: 'Associação de equipamento', contextAuthorizationHint: 'São necessários EngineeringModify e AssetProcessIntelligenceManage com escopo para o recurso para editar contextos.', contextPredicates: 'Predicados obrigatórios', contextPredicatesHint: 'Todos os predicados devem corresponder no mesmo timestamp UTC de origem, sincronizado e exato. Os limites numéricos usam a unidade de Engineering atual.', noContextPredicates: 'Adicione pelo menos um predicado obrigatório antes de salvar.', predicateTag: 'TAG do perfil de medição associado', choosePredicateTag: 'Escolha um TAG do perfil', predicateKind: 'Tipo de predicado', NumericInterval: 'Intervalo numérico inclusivo', BooleanEquals: 'Igualdade booleana', StringEquals: 'String exata', EnumEquals: 'Enum exato', minimumInclusive: 'Mínimo (inclusivo)', maximumInclusive: 'Máximo (inclusivo)', predicateValue: 'Valor obrigatório', exactValue: 'Valor ordinal exato', matchTrue: 'Verdadeiro', matchFalse: 'Falso', addPredicate: 'Adicionar predicado', deleteContextProfile: 'Excluir perfil de contexto', savedProfileVersion: 'Versão salva pelo servidor', contextVersionConflict: 'Este perfil de contexto foi alterado em outra edição. Atualize e revise a versão mais recente antes de salvar.'
  } as const;
}

function contextUnitText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    policy: 'Numeric bounds are saved with the current TAG unit. A unit change never converts bounds; review and explicitly reconfirm them. Queries fail closed until confirmed.',
    confirmed: 'Bounds confirmed in',
    unconfirmed: 'Numeric bounds need unit confirmation.',
    storedUnit: 'Saved unit:',
    currentUnit: 'Current unit:',
    unitless: 'unitless',
    confirm: 'Confirm bounds in current unit'
  } as const;
  if (locale === 'es') return {
    policy: 'Los límites numéricos se guardan con la unidad actual del TAG. Un cambio de unidad nunca convierte los límites; revíselos y confírmelos explícitamente. Las consultas se bloquean hasta confirmarlos.',
    confirmed: 'Límites confirmados en',
    unconfirmed: 'Los límites numéricos requieren confirmar la unidad.',
    storedUnit: 'Unidad guardada:',
    currentUnit: 'Unidad actual:',
    unitless: 'sin unidad',
    confirm: 'Confirmar límites en la unidad actual'
  } as const;
  return {
    policy: 'Os limites numéricos são salvos com a unidade atual do TAG. Uma mudança de unidade nunca converte os limites; revise-os e confirme explicitamente. As consultas ficam bloqueadas até a confirmação.',
    confirmed: 'Limites confirmados em',
    unconfirmed: 'Os limites numéricos precisam de confirmação da unidade.',
    storedUnit: 'Unidade salva:',
    currentUnit: 'Unidade atual:',
    unitless: 'sem unidade',
    confirm: 'Confirmar limites na unidade atual'
  } as const;
}
