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
  generateIntelligenceContextualDeviationScreenDraft,
  generateIntelligenceProcessStageCoverageScreenDraft,
  generateIntelligenceProcessStageGuidedDashboardDrafts,
  generateIntelligenceProcessStageInsightsDashboardDraft,
  generateIntelligenceGuidedDashboardDrafts,
  isNumericTagDataType,
  type IntelligenceDashboardAssignmentSource,
  type IntelligenceDashboardSource,
  type IntelligenceContextualDeviationDashboardSelection,
  type IntelligencePerformanceDashboardSelection,
  type IntelligenceGuidedDashboardSelections,
  type IntelligenceProfileCoverageDashboardSelection,
  type IntelligenceProcessStageCoverageDashboardSelection,
  type IntelligenceProcessStageInsightsDashboardSelection
} from './intelligenceDashboardGenerator';
import { AssetProcessIntelligenceCoveragePanel } from './AssetProcessIntelligenceCoveragePanel';
import { IntelligenceReferenceEvidencePanel } from './IntelligenceReferenceEvidencePanel';
import { IntelligenceContextEligibilityPanel } from './IntelligenceContextEligibilityPanel';
import { IntelligenceExperimentalJointPanel } from './IntelligenceExperimentalJointPanel';
import { AssetProcessIntelligencePerformancePanel } from './AssetProcessIntelligencePerformancePanel';
import { AssetProcessIntelligenceContextualDeviationPanel } from './AssetProcessIntelligenceContextualDeviationPanel';
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

type DashboardSourceOption = {
  key: string;
  label: string;
  source: IntelligenceDashboardSource;
  hasNumericSignal: boolean;
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
  onCreateDashboardDraft?: (draft: ScreenEngineering | readonly ScreenEngineering[]) => void;
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
  const [dashboardSourceKey, setDashboardSourceKey] = useState('');
  const [newSignalRole, setNewSignalRole] = useState('');
  const [newSignalTagId, setNewSignalTagId] = useState('');
  const [newSignalRequired, setNewSignalRequired] = useState(true);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [configSaving, setConfigSaving] = useState(false);
  const [transitioning, setTransitioning] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [guidedPerformanceSelections, setGuidedPerformanceSelections] = useState<Record<string, IntelligencePerformanceDashboardSelection | null>>({});
  const [guidedDeviationSelections, setGuidedDeviationSelections] = useState<Record<string, IntelligenceContextualDeviationDashboardSelection | null>>({});

  const updateGuidedPerformanceSelection = useCallback((assignmentId: string, selection: IntelligencePerformanceDashboardSelection | null) => {
    setGuidedPerformanceSelections(current => ({ ...current, [assignmentId]: selection }));
  }, []);
  const updateGuidedDeviationSelection = useCallback((assignmentId: string, selection: IntelligenceContextualDeviationDashboardSelection | null) => {
    setGuidedDeviationSelections(current => ({ ...current, [assignmentId]: selection }));
  }, []);

  const copy = useMemo(() => text(locale), [locale]);
  const stageCoverageCopy = useMemo(() => processStageCoverageText(locale), [locale]);
  const guidedStageDashboardCopy = useMemo(() => guidedProcessStageDashboardText(locale), [locale]);
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
    const matchingAssignments = assignments.filter(item => item.id.toLowerCase() === assignmentId.toLowerCase());
    const assignment = matchingAssignments.length === 1 ? matchingAssignments[0] : null;
    if (!assignment) return null;
    const matchingEquipment = equipment.filter(item => item.scopeId.toLowerCase() === assignment.equipmentId.toLowerCase());
    const matchingProfiles = profiles.filter(item => item.id.toLowerCase() === assignment.profileId.toLowerCase());
    const equipmentResource = matchingEquipment.length === 1 ? matchingEquipment[0] : null;
    const profile = matchingProfiles.length === 1 ? matchingProfiles[0] : null;
    if (!equipmentResource || !profile) return null;
    const assignmentSource: IntelligenceDashboardAssignmentSource = {
      equipmentName: equipmentResource.name,
      equipmentPath: equipmentResource.path ?? equipmentResource.scopeId,
      scopeId: assignment.equipmentId,
      assignmentId: assignment.id,
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
          ? [{
              equipmentName: equipmentResource.name,
              equipmentPath: equipmentResource.path ?? equipmentResource.scopeId,
              scopeId: assignment.equipmentId,
              assignmentId: assignment.id,
              profile
            }]
          : [];
      });
    if (assignmentSources.length === 0 || assignmentSources.length !== stage.links.length) return null;
    return {
      kind: 'process-stage',
      stableKey: stage.key,
      displayName: stage.name,
      assignments: assignmentSources
    };
  };

  const dashboardSourceOptions: DashboardSourceOption[] = [
    ...assignments.flatMap(assignment => {
      const source = createDashboardSourceForAssignment(assignment.id);
      if (!source) return [];
      return [{
        key: `equipment-profile:${assignment.id.toLowerCase()}`,
        label: `${copy.dashboardEquipmentSource} · ${source.displayName}`,
        source,
        hasNumericSignal: source.assignments.some(item => item.profile.signals.some(signal => {
          const tag = tagsById.get(signal.tagId.toLowerCase());
          return Boolean(tag && isNumericTagDataType(tag.dataType));
        }))
      }];
    }),
    ...processStages.flatMap(stage => {
      const source = createDashboardSourceForStage(stage);
      if (!source) return [];
      return [{
        key: `process-stage:${stage.id.toLowerCase()}`,
        label: `${copy.dashboardStageSource} · ${source.displayName}`,
        source,
        hasNumericSignal: source.assignments.some(item => item.profile.signals.some(signal => {
          const tag = tagsById.get(signal.tagId.toLowerCase());
          return Boolean(tag && isNumericTagDataType(tag.dataType));
        }))
      }];
    })
  ];
  const effectiveDashboardSourceKey = dashboardSourceKey || (dashboardSourceOptions.length === 1 ? dashboardSourceOptions[0].key : '');
  const selectedDashboardSource = dashboardSourceOptions.find(option => option.key === effectiveDashboardSourceKey) ?? null;

  const createProcessStageCoverageSelection = (
    stage: AssetProcessIntelligenceProcessStageEngineering
  ): IntelligenceProcessStageCoverageDashboardSelection | null => {
    if (stage.links.length < 1 || stage.links.length > 32) return null;
    const members = [...stage.links]
      .sort((left, right) => left.order - right.order || left.assignmentId.localeCompare(right.assignmentId))
      .map(link => {
        const assignment = assignmentById.get(link.assignmentId);
        const equipmentResource = assignment
          ? resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`)
          : undefined;
        const profile = assignment ? profileById.get(assignment.profileId) : undefined;
        if (!assignment || !equipmentResource || !profile) return null;
        return {
          assignmentId: assignment.id,
          scopeId: assignment.equipmentId,
          profileId: profile.id,
          displayLabel: `${equipmentResource.name} · ${profile.name}`,
          order: link.order
        };
      });
    if (members.some(member => member === null)) return null;
    return {
      stageId: stage.id,
      stageKey: stage.key,
      stageName: stage.name,
      members: members as NonNullable<typeof members[number]>[]
    };
  };

  const createProcessStageInsightsSelection = (
    stage: AssetProcessIntelligenceProcessStageEngineering
  ): IntelligenceProcessStageInsightsDashboardSelection | null => {
    const coverage = createProcessStageCoverageSelection(stage);
    if (!coverage) return null;
    const members = coverage.members.map(member => {
      const performance = guidedPerformanceSelections[member.assignmentId];
      const contextualDeviation = guidedDeviationSelections[member.assignmentId];
      if (!performance || !contextualDeviation) return null;
      return {
        ...member,
        performance: {
          ruleId: performance.ruleId,
          ruleVersion: performance.ruleVersion,
          signalIds: performance.signalIds
        },
        contextualDeviation: {
          targetSignalIds: contextualDeviation.targetSignalIds,
          firstContextSignalId: contextualDeviation.firstContextSignalId,
          firstContextLowerInclusiveCanonical: contextualDeviation.firstContextLowerInclusiveCanonical,
          firstContextUpperExclusiveCanonical: contextualDeviation.firstContextUpperExclusiveCanonical,
          secondContextSignalId: contextualDeviation.secondContextSignalId,
          secondContextLowerInclusiveCanonical: contextualDeviation.secondContextLowerInclusiveCanonical,
          secondContextUpperExclusiveCanonical: contextualDeviation.secondContextUpperExclusiveCanonical
        }
      };
    });
    if (members.some(member => member === null)) return null;
    return { ...coverage, members: members as NonNullable<typeof members[number]>[] };
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
    if (dirtyConfiguration) {
      setError(copy.dashboardConfigurationNeedsSaving);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceDashboardDraft(screens, source, tags, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

  const generateGuidedDashboard = (assignmentId: string) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    const source = createDashboardSourceForAssignment(assignmentId);
    const performance = guidedPerformanceSelections[assignmentId];
    const contextualDeviation = guidedDeviationSelections[assignmentId];
    if (!source || !performance || !contextualDeviation) {
      setError(copy.guidedDashboardSelectionsRequired);
      return;
    }
    try {
      const drafts = generateIntelligenceGuidedDashboardDrafts(screens, source, tags, {
        performance,
        contextualDeviation
      } satisfies IntelligenceGuidedDashboardSelections, locale);
      onCreateDashboardDraft(drafts);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

  const generateProcessStageCoverageScreen = (stage: AssetProcessIntelligenceProcessStageEngineering) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    const selection = createProcessStageCoverageSelection(stage);
    if (!selection) {
      setError(stageCoverageCopy.unavailable);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceProcessStageCoverageScreenDraft(screens, selection, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : stageCoverageCopy.unavailable);
    }
  };

  const generateGuidedProcessStageDashboard = (stage: AssetProcessIntelligenceProcessStageEngineering) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    const source = createDashboardSourceForStage(stage);
    const selection = createProcessStageCoverageSelection(stage);
    if (!source || !selection) {
      setError(stageCoverageCopy.unavailable);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceProcessStageGuidedDashboardDrafts(
        screens, source, selection, tags, locale
      ));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : stageCoverageCopy.unavailable);
    }
  };

  const generateProcessStageInsightsScreen = (stage: AssetProcessIntelligenceProcessStageEngineering) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    const source = createDashboardSourceForStage(stage);
    const selection = createProcessStageInsightsSelection(stage);
    if (!source || !selection) {
      setError(stageCoverageCopy.unavailable);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceProcessStageInsightsDashboardDraft(screens, source, selection, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : stageCoverageCopy.unavailable);
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

  const generateContextualDeviationScreen = (
    assignment: AssetProcessIntelligenceAssignmentEngineering,
    profile: AssetProcessIntelligenceProfileEngineering,
    equipmentName: string,
    selection: IntelligenceContextualDeviationDashboardSelection
  ) => {
    setError(null);
    setMessage(null);
    if (!snapshot?.licensed || !canEngineer || !canManage || !onCreateDashboardDraft) {
      setError(copy.dashboardAuthorizationHint);
      return;
    }
    try {
      onCreateDashboardDraft(generateIntelligenceContextualDeviationScreenDraft(screens, {
        ...selection,
        scopeId: assignment.equipmentId,
        assignmentId: assignment.id,
        profileId: profile.id,
        displayName: equipmentName
      }, locale));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : copy.dashboardSourceUnavailable);
    }
  };

  const createProfile = () => {
    const nextNumber = profiles.length + 1;
    const profile: AssetProcessIntelligenceProfileEngineering = {
      id: crypto.randomUUID(), key: generatedStableKey('profile', profiles.map(item => item.key)),
      name: `${copy.defaultProfile} ${nextNumber}`, signals: []
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
    if (!selectedProfile || !newSignalTagId || !newSignalRole.trim()) return;
    const tag = tagsById.get(newSignalTagId.toLowerCase());
    const signal: AssetProcessIntelligenceSignalEngineering = {
      id: crypto.randomUUID(), key: uniqueStableKey(slug(newSignalRole.trim() || tag?.name || copy.defaultSignal), selectedProfile.signals.map(item => item.key)),
      signalRole: newSignalRole.trim(), tagId: newSignalTagId, required: newSignalRequired
    };
    updateProfile({ signals: [...selectedProfile.signals, signal] });
    setNewSignalRole('');
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
      id: crypto.randomUUID(), key: generatedStableKey('context', operatingContextProfiles.map(item => item.key)),
      displayLabel: `${copy.defaultContext} ${nextNumber}`,
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
      id: crypto.randomUUID(), key: generatedStableKey('stage', processStages.map(item => item.key)),
      name: `${copy.defaultStage} ${nextNumber}`,
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
      {snapshot ? <div className="eng-section-meta"><strong>{copy.queryMode}</strong><span>{snapshot.execution.activePermitCount} {copy.permits}</span></div> : null}
    </header>

    <section className="eng-panel asset-process-intelligence__stage" aria-live="polite">
      <strong>{snapshot?.execution.workerRegistered ? copy.workerAvailable : copy.noWorker}</strong>
      <p>{copy.stageHint}</p>
      {snapshot?.execution.workspaceDirty ? <p role="status">{copy.workingChanges} <a href="/engineering">{copy.openLifecycle}</a></p> : null}
      <div className="asset-process-intelligence__status">
        <span>{copy.entitlement}: <strong>{snapshot?.licensed ? copy.entitled : copy.notEntitled}</strong></span>
        <span>{copy.project}: <strong>{snapshot?.execution.projectKey ?? copy.noProject}</strong></span>
        <span>{copy.revision}: <strong>{snapshot?.execution.activeRevision ?? '—'}</strong></span>
        {snapshot?.execution.workerRegistered && <span>{copy.queue}: <strong>{snapshot.execution.queueDepth}</strong></span>}
      </div>
    </section>

    {error ? <div className="eng-state-card error" role="alert">{error}</div> : null}
    {message ? <div className="eng-state-card" role="status">{message}</div> : null}
    {loading ? <div className="eng-state-card"><strong>{copy.loading}</strong></div> : null}
    {!loading && snapshot && <>
      <section className="eng-panel asset-process-intelligence__authoring-section" aria-labelledby="intelligence-dashboard-heading" data-testid="intelligence-dashboard-generator">
        <div className="asset-process-intelligence__section-heading"><div><h2 id="intelligence-dashboard-heading">{copy.dashboardTitle}</h2><p>{copy.dashboardHint}</p></div></div>
        {!snapshot.licensed ? <p role="note">{copy.dashboardLicenseRequired}</p> : !canEngineer || !canManage ? <p role="note">{copy.dashboardAuthorizationHint}</p> : null}
        {dashboardSourceOptions.length === 0 ? <div className="eng-empty"><strong>{copy.dashboardNoSources}</strong></div> : <div className="asset-process-intelligence__dashboard-form">
          <label>{copy.dashboardSourceLabel}<select value={effectiveDashboardSourceKey} disabled={!canEngineer || !canManage}
            data-testid="intelligence-dashboard-source" onChange={event => setDashboardSourceKey(event.currentTarget.value)}>
            <option value="">{copy.chooseDashboardSource}</option>
            {dashboardSourceOptions.map(option => <option key={option.key} value={option.key} disabled={!option.hasNumericSignal}>{option.label}</option>)}
          </select></label>
          <button type="button" data-testid="intelligence-dashboard-generate"
            disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft || !selectedDashboardSource?.hasNumericSignal || dirtyConfiguration}
            onClick={() => generateDashboard(selectedDashboardSource?.source ?? null)}>{copy.generateDashboard}</button>
        </div>}
        {selectedDashboardSource && !selectedDashboardSource.hasNumericSignal ? <p role="status">{copy.dashboardSourceNeedsNumeric}</p> : null}
        {dirtyConfiguration ? <p role="status">{copy.dashboardConfigurationNeedsSaving}</p> : null}
        <p className="asset-process-intelligence__dashboard-next-steps">{copy.dashboardNextSteps}</p>
      </section>
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
                    onChange={event => {
                      const checked = event.currentTarget.checked;
                      setSelected(current => {
                        const next = new Set(current);
                        if (checked) next.add(key); else next.delete(key);
                        return next;
                      });
                    }}/>
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
                <strong>{profile.name}</strong><small>{profile.signals.length} {copy.signals.toLowerCase()}</small>
              </button>)}
            </div>
            {selectedProfile ? <div className="asset-process-intelligence__editor">
              <label>{copy.profileName}<input value={selectedProfile.name} disabled={!canEngineer} onChange={event => updateProfile({ name: event.currentTarget.value })}/></label>
              <div className="asset-process-intelligence__section-heading"><div><h4>{copy.signals}</h4><p>{copy.signalsHint}</p></div></div>
              {selectedProfile.signals.length === 0 ? <div className="eng-empty"><strong>{copy.noSignals}</strong></div> : <div className="asset-process-intelligence__list">
                {selectedProfile.signals.map(signal => {
                  const tag = tagsById.get(signal.tagId.toLowerCase());
                  const binding = signal.semanticBinding;
                  return <article className="asset-process-intelligence__row" key={signal.id}>
                    <span><strong>{signal.signalRole || tag?.name || copy.defaultSignal}</strong><small>{tag ? `${tag.path} · ${tag.dataType}${tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}${tag.controlledEngineeringUnit ? ` · ${tag.controlledEngineeringUnit.code}` : ''}` : copy.missingTag}</small></span>
                    <span className={tag ? '' : 'asset-process-intelligence__missing'}>{signal.required ? copy.required : copy.optional}</span>
                    <div className="asset-process-intelligence__semantic-fields">
                      <label><span>{copy.semanticRole}</span><select
                        aria-label={`${copy.semanticRole}: ${signal.signalRole || tag?.name || copy.defaultSignal}`}
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
                        {binding && !semanticRoles.some(role => role.code === binding.roleCode && role.catalogVersion === binding.catalogVersion) ? <option value={semanticRoleOptionValue(binding.catalogVersion, binding.roleCode)}>{copy.unsupportedSemantic}</option> : null}
                        {semanticRoles.map(role => <option key={semanticRoleOptionValue(role.catalogVersion, role.code)} value={semanticRoleOptionValue(role.catalogVersion, role.code)}>{semanticRoleLabel(role.code, locale)}</option>)}
                      </select></label>
                      {binding ? semanticQualifierFields(binding, semanticCatalog?.semanticCatalogs ?? []).map(field => <label key={field.key}><span>{copy[field.label]}</span><select
                        aria-label={`${copy[field.label]}: ${signal.signalRole || tag?.name || copy.defaultSignal}`}
                        data-testid={`intelligence-semantic-${field.key}-${signal.key}`}
                        value={binding[field.key] ?? ''}
                        disabled={!canEngineer}
                        onChange={event => updateSignalQualifier(signal.id, field.key, event.currentTarget.value)}
                      ><option value="">{copy.chooseQualifier}</option>{field.options.map(option => <option key={option} value={option}>{semanticQualifierLabel(option, locale)}</option>)}</select></label>) : null}
                      {binding ? <small className={isSemanticBindingComplete(binding, semanticCatalog?.semanticCatalogs ?? []) ? '' : 'asset-process-intelligence__missing'}>{isSemanticBindingComplete(binding, semanticCatalog?.semanticCatalogs ?? []) ? copy.semanticComplete : copy.semanticIncomplete}</small> : null}
                    </div>
                    <button type="button" className="secondary" disabled={!canEngineer} onClick={() => updateProfile({ signals: selectedProfile.signals.filter(item => item.id !== signal.id) })}>{copy.remove}</button>
                  </article>;
                })}
              </div>}
              <div className="asset-process-intelligence__signal-form">
                <label>{copy.signalRole}<input value={newSignalRole} required disabled={!canEngineer} onChange={event => setNewSignalRole(event.currentTarget.value)} placeholder={copy.signalRolePlaceholder}/></label>
                <label>{copy.tagReference}<select value={newSignalTagId} disabled={!canEngineer || tags.length === 0} onChange={event => setNewSignalTagId(event.currentTarget.value)}>
                  <option value="">{tags.length ? copy.chooseTag : copy.noTags}</option>
                  {tags.filter(tag => tag.id).map(tag => <option key={tag.id} value={tag.id}>{tag.path} · {tag.dataType}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</option>)}
                </select></label>
                <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={newSignalRequired} disabled={!canEngineer} onChange={event => setNewSignalRequired(event.currentTarget.checked)}/>{copy.required}</label>
                <button type="button" className="secondary" disabled={!canEngineer || !newSignalRole.trim() || !newSignalTagId} onClick={addSignal}>{copy.addSignal}</button>
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
                <span><strong>{equipmentResource?.name ?? copy.missingEquipment} · {profile?.name ?? copy.missingProfile}</strong>{equipmentResource ? <small>{equipmentResource.path ?? equipmentResource.name}</small> : null}</span>
                {(!equipmentResource || !profile) ? <span className="asset-process-intelligence__missing">{copy.referenceMissing}</span> : null}
                <button type="button" className="secondary" data-testid={`intelligence-guided-dashboard-assignment-${assignment.id}`}
                  disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft || !createDashboardSourceForAssignment(assignment.id) ||
                    !guidedPerformanceSelections[assignment.id] || !guidedDeviationSelections[assignment.id]}
                  onClick={() => generateGuidedDashboard(assignment.id)}>{copy.generateGuidedDashboard}</button>
                <small className="asset-process-intelligence__guided-dashboard-hint">{copy.guidedDashboardHint}</small>
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
                {equipmentResource && profile ? <IntelligenceReferenceEvidencePanel
                  locale={locale} scopeId={assignment.equipmentId} profileId={assignment.profileId}
                  projectKey={snapshot.execution.projectKey} activeRevision={snapshot.execution.activeRevision}
                  canQuery={!coverageBlockedReason}
                /> : null}
                {equipmentResource && profile ? <IntelligenceContextEligibilityPanel
                  locale={locale} scopeId={assignment.equipmentId} assignmentId={assignment.id} profileId={assignment.profileId}
                  contexts={initialConfig.operatingContextProfiles}
                  projectKey={snapshot.execution.projectKey} activeRevision={snapshot.execution.activeRevision}
                  canQuery={!coverageBlockedReason}
                /> : null}
                {equipmentResource && profile ? <IntelligenceExperimentalJointPanel
                  locale={locale} scopeId={assignment.equipmentId} assignmentId={assignment.id} profile={profile} tags={tags}
                  contexts={initialConfig.operatingContextProfiles}
                  projectKey={snapshot.execution.projectKey} activeRevision={snapshot.execution.activeRevision}
                  canQuery={!coverageBlockedReason}
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
                  onGuidedSelectionChange={updateGuidedPerformanceSelection}
                  onCreateRuntimeScreenDraft={snapshot.licensed && canEngineer && canManage && onCreateDashboardDraft
                    ? selection => generatePerformanceScreen(assignment, profile, equipmentResource.name, selection)
                    : undefined}
                /> : null}
                {equipmentResource && profile ? <AssetProcessIntelligenceContextualDeviationPanel
                  locale={locale}
                  scopeId={assignment.equipmentId}
                  assignmentId={assignment.id}
                  profile={profile}
                  tags={tags}
                  controlledUnits={semanticCatalog?.controlledEngineeringUnitCatalogs ?? []}
                  displayName={`${equipmentResource.name} · ${profile.name}`}
                  canCreate={Boolean(snapshot.licensed && canEngineer && canManage && onCreateDashboardDraft)}
                  onGuidedSelectionChange={updateGuidedDeviationSelection}
                  onCreateRuntimeScreenDraft={snapshot.licensed && canEngineer && canManage && onCreateDashboardDraft
                    ? selection => generateContextualDeviationScreen(assignment, profile, equipmentResource.name, selection)
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
                  <strong>{profile.displayLabel}</strong><small>{equip?.name ?? copy.missingEquipment}</small>
                </button>;
              })}
            </div>
            {selectedContextProfile ? <div className="asset-process-intelligence__editor">
              <label>{copy.contextLabel}<input value={selectedContextProfile.displayLabel} disabled={!canEngineer || !canManage} onChange={event => updateContextProfile({ displayLabel: event.currentTarget.value })}/></label>
              <div className="asset-process-intelligence__section-heading"><div><h4>{copy.contextPredicates}</h4><p>{copy.contextPredicatesHint}</p><p>{unitCopy.policy}</p></div></div>
              {selectedContextProfile.predicates.length === 0 ? <div className="eng-empty"><strong>{copy.noContextPredicates}</strong></div> : <div className="asset-process-intelligence__list">
                {selectedContextProfile.predicates.map(predicate => {
                  const tag = tagsById.get(predicate.tagId.toLowerCase());
                  const numericUnitConfirmed = predicate.kind !== 'NumericInterval' || (!!tag &&
                    predicate.engineeringUnitConfirmed === true &&
                    (predicate.engineeringUnit ?? null) === (tag?.engineeringUnit ?? null));
                  return <article className="asset-process-intelligence__row" key={predicate.tagId}>
                    <span><strong>{tag?.path ?? copy.missingTag}</strong>{tag ? <small>{tag.dataType}{tag.engineeringUnit ? ` · ${tag.engineeringUnit}` : ''}</small> : null}</span>
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
                <button type="button" role="option" aria-selected={stage.id === selectedStageId} onClick={() => setSelectedStageId(stage.id)}><strong>{stage.order + 1}. {stage.name}</strong></button>
                <span><button type="button" className="secondary" aria-label={copy.moveUp} disabled={!canEngineer || index === 0} onClick={() => reorderStage(stage.id, -1)}>↑</button><button type="button" className="secondary" aria-label={copy.moveDown} disabled={!canEngineer || index === processStages.length - 1} onClick={() => reorderStage(stage.id, 1)}>↓</button></span>
              </div>)}
            </div>
            {selectedStage ? <div className="asset-process-intelligence__editor">
              <label>{copy.stageName}<input value={selectedStage.name} disabled={!canEngineer} onChange={event => updateStage({ name: event.currentTarget.value })}/></label>
              <button type="button" className="secondary" data-testid={`intelligence-guided-stage-dashboard-${selectedStage.id}`}
                disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft ||
                  !createDashboardSourceForStage(selectedStage) || !createProcessStageCoverageSelection(selectedStage)}
                onClick={() => generateGuidedProcessStageDashboard(selectedStage)}>{guidedStageDashboardCopy.generate}</button>
              <button type="button" className="secondary" data-testid={`intelligence-process-stage-insights-${selectedStage.id}`}
                disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft ||
                  !createDashboardSourceForStage(selectedStage) || !createProcessStageInsightsSelection(selectedStage)}
                onClick={() => generateProcessStageInsightsScreen(selectedStage)}>{guidedStageDashboardCopy.generateInsights}</button>
              <small>{guidedStageDashboardCopy.hint}</small>
              <button type="button" className="secondary" data-testid={`intelligence-stage-coverage-${selectedStage.id}`}
                disabled={!snapshot.licensed || !canEngineer || !canManage || !onCreateDashboardDraft || !createProcessStageCoverageSelection(selectedStage)}
                onClick={() => generateProcessStageCoverageScreen(selectedStage)}>{stageCoverageCopy.generate}</button>
              {selectedStage.links.length === 0 ? <div className="eng-empty"><strong>{copy.noStageAssignments}</strong></div> : <div className="asset-process-intelligence__list">
                {[...selectedStage.links].sort((a, b) => a.order - b.order).map((link, index) => {
                  const assignment = assignmentById.get(link.assignmentId);
                  const equipmentResource = assignment ? resourcesByKey.get(`Equipment:${assignment.equipmentId.toLowerCase()}`) : undefined;
                  const profile = assignment ? profileById.get(assignment.profileId) : undefined;
                  return <article className="asset-process-intelligence__row" key={`${selectedStage.id}:${link.assignmentId}`}>
                    <span><strong>{link.order + 1}. {assignment && equipmentResource && profile ? `${equipmentResource.name} · ${profile.name}` : copy.referenceMissing}</strong>{assignment && equipmentResource ? <small>{equipmentResource.path ?? equipmentResource.name}</small> : null}</span>
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
function generatedStableKey(prefix: string, existingKeys: readonly string[]): string {
  const existing = new Set(existingKeys.map(key => key.toLowerCase()));
  let candidate: string;
  do {
    candidate = `${prefix}-${crypto.randomUUID().replaceAll('-', '')}`;
  } while (existing.has(candidate.toLowerCase()));
  return candidate;
}
function uniqueStableKey(base: string, existingKeys: readonly string[]): string {
  const normalized = slug(base);
  const existing = new Set(existingKeys.map(key => key.toLowerCase()));
  if (!existing.has(normalized.toLowerCase())) return normalized;
  let number = 2;
  let candidate = `${normalized.slice(0, 61)}-${number}`;
  while (existing.has(candidate.toLowerCase())) {
    number += 1;
    const suffix = `-${number}`;
    candidate = `${normalized.slice(0, 64 - suffix.length)}${suffix}`;
  }
  return candidate;
}

const intelligenceSemanticLabels: Record<string, Partial<Record<EngineeringLocale, string>>> = {
  'pump.electrical_current': { 'pt-BR': 'Corrente elétrica', en: 'Electrical current', es: 'Corriente eléctrica' },
  'pump.drive_frequency': { 'pt-BR': 'Frequência de acionamento', en: 'Drive frequency', es: 'Frecuencia de accionamiento' },
  'pump.discharge_pressure': { 'pt-BR': 'Pressão de descarga', en: 'Discharge pressure', es: 'Presión de descarga' },
  'pump.discharge_volumetric_flow': { 'pt-BR': 'Vazão volumétrica', en: 'Volumetric flow', es: 'Caudal volumétrico' },
  'equipment.active_input_power': { 'pt-BR': 'Potência ativa de entrada', en: 'Active input power', es: 'Potencia activa de entrada' },
  'process.net_good_production_volume_rate': { 'pt-BR': 'Volume de produção aprovada por tempo', en: 'Good production volume rate', es: 'Volumen de producción buena por tiempo' },
  'process.net_good_production_mass_rate': { 'pt-BR': 'Massa de produção aprovada por tempo', en: 'Good production mass rate', es: 'Masa de producción buena por tiempo' },
  'process.net_good_production_item_rate': { 'pt-BR': 'Unidades aprovadas por tempo', en: 'Good items per time', es: 'Unidades buenas por tiempo' },
  'pump.direct_differential_pressure': { 'pt-BR': 'Pressão diferencial da bomba', en: 'Pump differential pressure', es: 'Presión diferencial de la bomba' },
  line_rms: { 'pt-BR': 'Corrente eficaz de linha (RMS)', en: 'Line RMS current', es: 'Corriente eficaz de línea (RMS)' },
  phase_rms: { 'pt-BR': 'Corrente eficaz de fase (RMS)', en: 'Phase RMS current', es: 'Corriente eficaz de fase (RMS)' },
  dc_bus: { 'pt-BR': 'Corrente do barramento CC', en: 'DC bus current', es: 'Corriente del bus de CC' },
  measured_output: { 'pt-BR': 'Valor medido na saída', en: 'Measured output', es: 'Valor medido en la salida' },
  command_setpoint: { 'pt-BR': 'Referência de comando', en: 'Command setpoint', es: 'Referencia de comando' },
  drive_electrical: { 'pt-BR': 'Frequência elétrica do inversor', en: 'Drive electrical frequency', es: 'Frecuencia eléctrica del variador' },
  shaft_rotational: { 'pt-BR': 'Rotação do eixo', en: 'Shaft rotational speed', es: 'Velocidad de giro del eje' },
  gauge: { 'pt-BR': 'Pressão manométrica', en: 'Gauge pressure', es: 'Presión manométrica' },
  absolute: { 'pt-BR': 'Pressão absoluta', en: 'Absolute pressure', es: 'Presión absoluta' },
  pump_to_process: { 'pt-BR': 'Da bomba para o processo', en: 'Pump to process', es: 'De la bomba al proceso' },
  process_to_pump: { 'pt-BR': 'Do processo para a bomba', en: 'Process to pump', es: 'Del proceso a la bomba' },
  bidirectional_signed: { 'pt-BR': 'Bidirecional com direção e sinal', en: 'Bidirectional with direction and sign', es: 'Bidireccional con dirección y signo' },
  bidirectional_magnitude: { 'pt-BR': 'Bidirecional por magnitude', en: 'Bidirectional magnitude', es: 'Magnitud bidireccional' },
  measured_total_input: { 'pt-BR': 'Total medido na entrada', en: 'Measured total input', es: 'Total medido de entrada' },
  differential_outlet_minus_inlet: { 'pt-BR': 'Saída menos entrada', en: 'Outlet minus inlet', es: 'Salida menos entrada' }
};

function semanticRoleLabel(code: string, locale: EngineeringLocale): string {
  return intelligenceSemanticLabels[code]?.[locale] ?? humanizeSemanticCode(code);
}
function semanticQualifierLabel(code: string, locale: EngineeringLocale): string {
  return intelligenceSemanticLabels[code]?.[locale] ?? humanizeSemanticCode(code);
}
function humanizeSemanticCode(value: string): string {
  return value.replace(/[._-]+/g, ' ').replace(/\b\w/g, letter => letter.toUpperCase());
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
    eyebrow: 'Engineering · Analysis setup', title: 'Asset & Process Intelligence', queryMode: 'On-demand queries',
    description: 'Associate measurements with equipment, select what to monitor and query history to compare periods and operating conditions.',
    noWorker: 'Analyses run when you request a query.', workerAvailable: 'Analytical processing available',
    stageHint: 'Activate the equipment or area, then open an analysis under its profile assignment. Select the periods and query. Editing profiles or activating a scope does not start continuous analysis.', workingChanges: 'Working changes must be reviewed and published before activation.', openLifecycle: 'Open project lifecycle',
    entitlement: 'Signed entitlement', entitled: 'Available', notEntitled: 'Required to activate', project: 'Project', revision: 'Active revision', queue: 'Queue', permits: 'execution permits',
    noProject: 'No active project', eligibleScopes: 'Eligible scopes', scopeHint: 'Select the Equipment and Locations you want to analyze. Saving changes Working only; publish it before activation.',
    noResources: 'No Equipment or Locations are available to select.', refresh: 'Refresh', notConfigured: 'Not selected', configuredActive: 'Configured active', statusPaused: 'Paused', permitArmed: 'Process permit armed', permitClosed: 'Configured active · process permit closed; explicitly activate after authorization is restored (or after restart, if applicable)', noPermit: 'No process permit', activate: 'Activate', pause: 'Pause', working: 'Applying…', saveScopes: 'Save eligible scopes', saving: 'Saving…', saved: 'Eligible scopes saved.', activated: 'Scope activated for the current revision.', paused: 'Scope paused; pending work was cancelled.', authorizationHint: 'EngineeringModify and an explicit AssetProcessIntelligenceManage grant, global or scoped to this resource, are required.', loading: 'Loading scopes…',
    stage2Title: 'Process configuration', stage2Hint: 'Create reusable measurement profiles from existing TAGs, assign them to Equipment and arrange the process stages. These edits are saved to Working.',
    profiles: 'Reusable profiles', profilesHint: 'A profile can be reused by multiple Equipment assignments.', newProfile: 'New profile', noProfiles: 'No profiles yet. Create one to define static signal references.', defaultProfile: 'Profile', defaultSignal: 'Signal', profileName: 'Display name', signals: 'Signal references', signalsHint: 'Choose existing TAGs and describe what each measurement represents. The optional meaning selection enables analyses that support it.', noSignals: 'No signals in this profile.', signalRole: 'What does this measurement represent?', signalRolePlaceholder: 'e.g. motor current', semanticRole: 'Measurement meaning', noSemanticRole: 'Not specified', chooseQualifier: 'Choose a qualifier', currentBasisLabel: 'Current basis', signalKindLabel: 'Signal kind', frequencyReferenceLabel: 'Frequency reference', pressureReferenceLabel: 'Pressure reference', flowDirectionLabel: 'Flow direction', semanticComplete: 'Controlled annotation complete', semanticIncomplete: 'Incomplete: select every required qualifier before saving', unsupportedSemantic: 'This saved meaning is unavailable', tagReference: 'TAG reference', chooseTag: 'Choose a TAG', noTags: 'No TAGs are available', addSignal: 'Add signal', required: 'Required', optional: 'Optional', missingTag: 'Missing TAG reference', remove: 'Remove', deleteProfile: 'Delete profile and its assignments',
    assignments: 'Equipment assignments', assignmentsHint: 'Choose the Equipment and profile to link. One Equipment may use more than one profile.', equipment: 'Equipment', profile: 'Profile', chooseEquipment: 'Choose Equipment', chooseProfile: 'Choose a profile', assignProfile: 'Assign profile', noEquipment: 'No Equipment is available.', noAssignments: 'No assignments yet.', missingEquipment: 'Missing Equipment', missingProfile: 'Missing profile', referenceMissing: 'Reference is missing',
    processStages: 'Ordered process stages', processStagesHint: 'Group Equipment assignments into process stages and set the order you want to monitor.', newStage: 'New stage', noStages: 'No process stages yet.', defaultStage: 'Stage', stageName: 'Display name', noStageAssignments: 'No assignments in this stage.', stageAssignment: 'Assignment', chooseAssignment: 'Choose an assignment', addToStage: 'Add to stage', moveUp: 'Move up', moveDown: 'Move down', staticOnly: 'Editing this configuration does not start queries. Use the analysis panels in each assignment to read history when needed.', saveConfiguration: 'Save process configuration', configurationSaved: 'Process configuration saved to Working.',
    dashboardTitle: 'Create a Runtime dashboard', dashboardHint: 'Create a normal editable Screen from existing TAGs. The basic draft adds value cards, a trend and coverage; it does not read live or historical data or invent KPIs.', dashboardSourceLabel: 'Dashboard content', chooseDashboardSource: 'Select an equipment profile or process stage', dashboardEquipmentSource: 'Equipment profile', dashboardStageSource: 'Process stage', dashboardNoSources: 'Create a profile with at least one numeric TAG, assign it to equipment, or add that assignment to a process stage.', dashboardSourceNeedsNumeric: 'This source has no numeric TAG yet. Add a numeric measurement to its profile.', dashboardConfigurationNeedsSaving: 'Save the current Intelligence configuration before generating the dashboard.', dashboardNextSteps: 'After generation: review the Screen, use Preview and Apply, then publish and activate the project revision for Runtime.', generateDashboard: 'Generate dashboard draft', generateGuidedDashboard: 'Create guided dashboard views', guidedDashboardHint: 'Choose a valid Performance rule and complete the two contextual signal ranges first. Each view remains a separate editable Screen.', guidedDashboardSelectionsRequired: 'Choose one valid Performance rule and complete the contextual-deviation signal/range selection before generating the four-screen set.', dashboardLicenseRequired: 'The Asset & Process Intelligence license is required to generate this screen.', dashboardAuthorizationHint: 'EngineeringModify and an explicit AssetProcessIntelligenceManage grant, global or scoped to this resource, are required to generate a screen from these profiles.', dashboardSourceUnavailable: 'Choose a complete profile or process stage with at least one resolvable numeric TAG.',
    contextProfiles: 'Operating-context profiles', contextProfilesHint: 'Link each context to an Equipment assignment. Matching checks are separate, read-only and use the saved measurement profile.', newContextProfile: 'New context', noContextProfiles: 'No operating contexts yet.', defaultContext: 'Context', contextLabel: 'Context name', selectedAssignment: 'Equipment assignment', contextAuthorizationHint: 'EngineeringModify and an explicit AssetProcessIntelligenceManage grant, global or scoped to this resource, are required to edit contexts.', contextPredicates: 'Conditions to check', contextPredicatesHint: 'All conditions must match readings from the same source time. Number ranges use the TAG’s current Engineering unit.', noContextPredicates: 'Add at least one condition to describe this operating context.', predicateTag: 'Measurement from the assigned profile', choosePredicateTag: 'Choose a measurement', predicateKind: 'Condition type', NumericInterval: 'Number range, including both limits', BooleanEquals: 'True or false', StringEquals: 'Exact text', EnumEquals: 'Exact choice', minimumInclusive: 'From (inclusive)', maximumInclusive: 'To (inclusive)', predicateValue: 'Value to match', exactValue: 'Exact value', matchTrue: 'True', matchFalse: 'False', addPredicate: 'Add condition', deleteContextProfile: 'Delete context profile', contextVersionConflict: 'This context changed elsewhere. Refresh and review its conditions before saving.'
  } as const;
  if (locale === 'es') return {
    eyebrow: 'Ingeniería · Configuración de análisis', title: 'Inteligencia de Activos y Procesos', queryMode: 'Consultas bajo demanda',
    description: 'Asocie mediciones a los equipos, seleccione qué seguir y consulte el histórico para comparar períodos y condiciones operativas.',
    noWorker: 'Los análisis se ejecutan cuando solicita una consulta.', workerAvailable: 'Procesamiento analítico disponible',
    stageHint: 'Active el equipo o área y abra un análisis en su asignación de perfil. Seleccione los períodos y consulte. Editar perfiles o activar un alcance no inicia análisis continuo.', workingChanges: 'Revise y publique los cambios de trabajo antes de activar.', openLifecycle: 'Abrir ciclo del proyecto',
    entitlement: 'Licencia firmada', entitled: 'Disponible', notEntitled: 'Necesaria para activar', project: 'Proyecto', revision: 'Revisión activa', queue: 'Cola', permits: 'permisos de ejecución',
    noProject: 'Sin proyecto activo', eligibleScopes: 'Alcances elegibles', scopeHint: 'Seleccione los equipos y ubicaciones que desea analizar. Guardar modifica solo Working; publique antes de activar.',
    noResources: 'No hay equipos o ubicaciones para seleccionar.', refresh: 'Actualizar', notConfigured: 'No seleccionado', configuredActive: 'Activo configurado', statusPaused: 'Pausado', permitArmed: 'Permiso de proceso habilitado', permitClosed: 'Activo configurado · permiso de proceso cerrado; actívelo explícitamente tras restaurar la autorización (o tras reiniciar, si corresponde)', noPermit: 'Sin permiso de proceso', activate: 'Activar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Guardar alcances', saving: 'Guardando…', saved: 'Alcances guardados.', activated: 'Alcance activado para la revisión actual.', paused: 'Alcance pausado; se canceló el trabajo pendiente.', authorizationHint: 'Se requiere EngineeringModify y un permiso explícito AssetProcessIntelligenceManage, global o para ese recurso.', loading: 'Cargando alcances…',
    stage2Title: 'Configuración de procesos', stage2Hint: 'Cree perfiles reutilizables con TAGs existentes, asígnelos a equipos y ordene las etapas del proceso. Los cambios se guardan en Working.',
    profiles: 'Perfiles reutilizables', profilesHint: 'Un perfil puede reutilizarse en varias asignaciones de equipos.', newProfile: 'Nuevo perfil', noProfiles: 'Aún no hay perfiles. Cree uno para definir referencias estáticas.', defaultProfile: 'Perfil', defaultSignal: 'Señal', profileName: 'Nombre', signals: 'Referencias de señales', signalsHint: 'Elija TAGs existentes y describa qué representa cada medición. El significado opcional habilita los análisis compatibles.', noSignals: 'Este perfil no tiene señales.', signalRole: '¿Qué representa esta medición?', signalRolePlaceholder: 'p. ej. corriente del motor', semanticRole: 'Significado de la medición', noSemanticRole: 'Sin especificar', chooseQualifier: 'Elija un calificador', currentBasisLabel: 'Base de corriente', signalKindLabel: 'Tipo de señal', frequencyReferenceLabel: 'Referencia de frecuencia', pressureReferenceLabel: 'Referencia de presión', flowDirectionLabel: 'Dirección del flujo', semanticComplete: 'Anotación controlada completa', semanticIncomplete: 'Incompleta: seleccione todos los calificadores requeridos antes de guardar', unsupportedSemantic: 'Este significado guardado no está disponible', tagReference: 'Referencia TAG', chooseTag: 'Elija un TAG', noTags: 'No hay TAGs disponibles', addSignal: 'Añadir señal', required: 'Obligatoria', optional: 'Opcional', missingTag: 'Falta la referencia TAG', remove: 'Eliminar', deleteProfile: 'Eliminar perfil y sus asignaciones',
    assignments: 'Asignaciones de equipo', assignmentsHint: 'Elija el equipo y el perfil que desea asociar. Un equipo puede usar más de un perfil.', equipment: 'Equipo', profile: 'Perfil', chooseEquipment: 'Elija un equipo', chooseProfile: 'Elija un perfil', assignProfile: 'Asignar perfil', noEquipment: 'No hay equipos disponibles.', noAssignments: 'Aún no hay asignaciones.', missingEquipment: 'Equipo inexistente', missingProfile: 'Perfil inexistente', referenceMissing: 'Falta la referencia',
    processStages: 'Etapas ordenadas', processStagesHint: 'Agrupe las asignaciones de equipos en etapas del proceso y ajuste el orden que desea supervisar.', newStage: 'Nueva etapa', noStages: 'Aún no hay etapas.', defaultStage: 'Etapa', stageName: 'Nombre', noStageAssignments: 'No hay asignaciones en esta etapa.', stageAssignment: 'Asignación', chooseAssignment: 'Elija una asignación', addToStage: 'Añadir a etapa', moveUp: 'Mover arriba', moveDown: 'Mover abajo', staticOnly: 'Editar esta configuración no inicia consultas. Use los paneles de cada asignación para consultar el histórico cuando lo necesite.', saveConfiguration: 'Guardar configuración', configurationSaved: 'Configuración guardada en Working.',
    dashboardTitle: 'Crear un panel para Runtime', dashboardHint: 'Cree una Pantalla editable normal con TAGs existentes. El borrador básico incluye tarjetas de valores, una tendencia y cobertura; no consulta valores actuales ni Historian ni inventa KPI.', dashboardSourceLabel: 'Contenido del panel', chooseDashboardSource: 'Elija un perfil de equipo o una etapa del proceso', dashboardEquipmentSource: 'Perfil de equipo', dashboardStageSource: 'Etapa del proceso', dashboardNoSources: 'Cree un perfil con al menos un TAG numérico, asígnelo a un equipo o añada esa asociación a una etapa del proceso.', dashboardSourceNeedsNumeric: 'Esta fuente aún no tiene un TAG numérico. Añada una medición numérica al perfil.', dashboardConfigurationNeedsSaving: 'Guarde la configuración actual de Intelligence antes de generar el panel.', dashboardNextSteps: 'Después de generar: revise la Pantalla, use Preview y Aplicar, y luego publique y active la revisión del proyecto para Runtime.', generateDashboard: 'Generar borrador del panel', generateGuidedDashboard: 'Crear vistas guiadas del panel', guidedDashboardHint: 'Primero elija una regla de rendimiento válida y complete los dos rangos de señales contextuales. Cada vista sigue siendo una Pantalla editable independiente.', guidedDashboardSelectionsRequired: 'Elija una regla de rendimiento válida y complete la selección de señales y rangos contextuales antes de generar las cuatro pantallas.', dashboardLicenseRequired: 'Se requiere la licencia de Inteligencia de Activos y Procesos para generar esta pantalla.', dashboardAuthorizationHint: 'Se requieren EngineeringModify y un permiso explícito AssetProcessIntelligenceManage, global o para ese recurso, para generar pantallas desde estos perfiles.', dashboardSourceUnavailable: 'Elija un perfil o etapa completa con al menos un TAG numérico resoluble.',
    contextProfiles: 'Perfiles de contexto operativo', contextProfilesHint: 'Vincule cada contexto a una asignación de equipo. Las comprobaciones son independientes, de solo lectura y usan el perfil de medición guardado.', newContextProfile: 'Nuevo contexto', noContextProfiles: 'Aún no hay contextos operativos.', defaultContext: 'Contexto', contextLabel: 'Nombre del contexto', selectedAssignment: 'Asignación de equipo', contextAuthorizationHint: 'Se requieren EngineeringModify y un permiso explícito AssetProcessIntelligenceManage, global o para ese recurso, para editar contextos.', contextPredicates: 'Condiciones a verificar', contextPredicatesHint: 'Todas las condiciones deben coincidir con lecturas del mismo instante de origen. Los intervalos numéricos usan la unidad actual del TAG.', noContextPredicates: 'Añada al menos una condición para describir este contexto operacional.', predicateTag: 'Medición del perfil asignado', choosePredicateTag: 'Elija una medición', predicateKind: 'Tipo de condición', NumericInterval: 'Intervalo numérico, incluidos ambos límites', BooleanEquals: 'Verdadero o falso', StringEquals: 'Texto exacto', EnumEquals: 'Opción exacta', minimumInclusive: 'Desde (incluido)', maximumInclusive: 'Hasta (incluido)', predicateValue: 'Valor que debe coincidir', exactValue: 'Valor exacto', matchTrue: 'Verdadero', matchFalse: 'Falso', addPredicate: 'Añadir condición', deleteContextProfile: 'Eliminar perfil de contexto', contextVersionConflict: 'Este contexto cambió en otra edición. Actualice la pantalla y revise sus condiciones antes de guardar.'
  } as const;
  return {
    eyebrow: 'Engenharia · Configuração das análises', title: 'Inteligência de Ativos e Processos', queryMode: 'Consultas sob demanda',
    description: 'Associe medições aos equipamentos, escolha o que acompanhar e consulte o histórico para comparar períodos e condições de operação.',
    noWorker: 'As análises são executadas quando você solicita uma consulta.', workerAvailable: 'Processamento analítico disponível',
    stageHint: 'Ative o equipamento ou área e abra uma análise na associação do perfil. Escolha os períodos e consulte. Editar perfis ou ativar um escopo não inicia análise contínua.', workingChanges: 'Revise e publique as alterações de Working antes de ativar.', openLifecycle: 'Abrir ciclo do projeto',
    entitlement: 'Licença assinada', entitled: 'Disponível', notEntitled: 'Necessária para ativar', project: 'Projeto', revision: 'Revisão ativa', queue: 'Fila', permits: 'permissões de execução',
    noProject: 'Nenhum projeto ativo', eligibleScopes: 'Escopos elegíveis', scopeHint: 'Selecione os equipamentos e locais que deseja analisar. Salvar altera apenas o Working; publique antes de ativar.',
    noResources: 'Não há equipamentos ou locais disponíveis para selecionar.', refresh: 'Atualizar', notConfigured: 'Não selecionado', configuredActive: 'Ativo na configuração', statusPaused: 'Pausado', permitArmed: 'Permissão de processo armada', permitClosed: 'Ativo configurado · permissão de processo fechada; ative explicitamente após restaurar a autorização (ou após reiniciar, se aplicável)', noPermit: 'Sem permissão de processo', activate: 'Ativar', pause: 'Pausar', working: 'Aplicando…', saveScopes: 'Salvar escopos elegíveis', saving: 'Salvando…', saved: 'Escopos elegíveis salvos.', activated: 'Escopo ativado para a revisão atual.', paused: 'Escopo pausado; o trabalho pendente foi cancelado.', authorizationHint: 'São necessários EngineeringModify e uma permissão explícita AssetProcessIntelligenceManage, global ou para este recurso.', loading: 'Carregando escopos…',
    stage2Title: 'Configuração de processos', stage2Hint: 'Crie perfis reutilizáveis com TAGs já cadastrados, associe-os aos equipamentos e organize as etapas do processo. As edições são salvas no Working.',
    profiles: 'Perfis reutilizáveis', profilesHint: 'Um perfil pode ser reutilizado em várias associações de equipamentos.', newProfile: 'Novo perfil', noProfiles: 'Nenhum perfil ainda. Crie um para definir referências estáticas de sinais.', defaultProfile: 'Perfil', defaultSignal: 'Sinal', profileName: 'Nome de exibição', signals: 'Referências de sinais', signalsHint: 'Escolha TAGs existentes e descreva o que cada medição representa. O significado opcional habilita as análises compatíveis.', noSignals: 'Este perfil não possui sinais.', signalRole: 'O que esta medição representa?', signalRolePlaceholder: 'ex.: corrente elétrica do motor', semanticRole: 'Significado da medição', noSemanticRole: 'Não informado', chooseQualifier: 'Escolha um qualificador', currentBasisLabel: 'Base de corrente', signalKindLabel: 'Tipo do sinal', frequencyReferenceLabel: 'Referência de frequência', pressureReferenceLabel: 'Referência de pressão', flowDirectionLabel: 'Direção do fluxo', semanticComplete: 'Anotação controlada completa', semanticIncomplete: 'Incompleta: selecione todos os qualificadores obrigatórios antes de salvar', unsupportedSemantic: 'Este significado salvo não está disponível', tagReference: 'Referência do TAG', chooseTag: 'Escolha um TAG', noTags: 'Nenhum TAG disponível', addSignal: 'Adicionar sinal', required: 'Obrigatório', optional: 'Opcional', missingTag: 'Referência de TAG ausente', remove: 'Remover', deleteProfile: 'Excluir perfil e associações',
    assignments: 'Associações de equipamentos', assignmentsHint: 'Escolha o equipamento e o perfil que deseja associar. Um equipamento pode usar mais de um perfil.', equipment: 'Equipamento', profile: 'Perfil', chooseEquipment: 'Escolha um equipamento', chooseProfile: 'Escolha um perfil', assignProfile: 'Associar perfil', noEquipment: 'Nenhum equipamento disponível.', noAssignments: 'Nenhuma associação ainda.', missingEquipment: 'Equipamento ausente', missingProfile: 'Perfil ausente', referenceMissing: 'Referência ausente',
    processStages: 'Etapas ordenadas do processo', processStagesHint: 'Agrupe as associações de equipamentos em etapas do processo e ajuste a ordem que deseja acompanhar.', newStage: 'Nova etapa', noStages: 'Nenhuma etapa de processo ainda.', defaultStage: 'Etapa', stageName: 'Nome de exibição', noStageAssignments: 'Nenhuma associação nesta etapa.', stageAssignment: 'Associação', chooseAssignment: 'Escolha uma associação', addToStage: 'Adicionar à etapa', moveUp: 'Mover para cima', moveDown: 'Mover para baixo', staticOnly: 'Editar esta configuração não inicia consultas. Use os painéis de análise de cada associação quando quiser consultar o histórico.', saveConfiguration: 'Salvar configuração de processo', configurationSaved: 'Configuração de processo salva no Working.',
    dashboardTitle: 'Gerar painel para o Runtime', dashboardHint: 'Gere uma Tela normal e editável a partir de TAGs existentes. O rascunho básico inclui cartões de valores, tendência e cobertura; não consulta valores atuais nem Historian e não inventa KPIs.', dashboardSourceLabel: 'Conteúdo do painel', chooseDashboardSource: 'Selecione um perfil de equipamento ou uma etapa do processo', dashboardEquipmentSource: 'Perfil de equipamento', dashboardStageSource: 'Etapa do processo', dashboardNoSources: 'Crie um perfil com ao menos um TAG numérico, associe-o a um equipamento ou inclua essa associação numa etapa do processo.', dashboardSourceNeedsNumeric: 'Esta origem ainda não tem TAG numérico. Adicione uma medição numérica ao perfil.', dashboardConfigurationNeedsSaving: 'Salve a configuração atual da Intelligence antes de gerar o painel.', dashboardNextSteps: 'Depois de gerar: revise a Tela, use Pré-visualizar e Aplicar e, em seguida, publique e ative a revisão do projeto para vê-la no Runtime.', generateDashboard: 'Gerar rascunho do painel', generateGuidedDashboard: 'Criar telas do painel guiado', guidedDashboardHint: 'Escolha uma regra de desempenho válida e complete os dois intervalos dos sinais de contexto. Cada visualização continua sendo uma Tela separada e editável.', guidedDashboardSelectionsRequired: 'Escolha uma regra de desempenho válida e complete a seleção dos sinais e intervalos contextuais antes de gerar o conjunto de quatro telas.', dashboardLicenseRequired: 'A licença de Inteligência de Ativos e Processos é necessária para gerar esta tela.', dashboardAuthorizationHint: 'EngineeringModify e uma permissão explícita AssetProcessIntelligenceManage, global ou para este recurso, são necessários para gerar telas a partir destes perfis.', dashboardSourceUnavailable: 'Escolha um perfil ou etapa completa com pelo menos um TAG numérico resolvível.',
    contextProfiles: 'Perfis de contexto operacional', contextProfilesHint: 'Vincule cada contexto a uma associação de equipamento. As verificações são separadas, somente leitura e usam o perfil de medição salvo.', newContextProfile: 'Novo contexto', noContextProfiles: 'Nenhum contexto operacional ainda.', defaultContext: 'Contexto', contextLabel: 'Nome do contexto', selectedAssignment: 'Associação de equipamento', contextAuthorizationHint: 'São necessários EngineeringModify e uma permissão explícita AssetProcessIntelligenceManage, global ou para este recurso, para editar contextos.', contextPredicates: 'Condições a conferir', contextPredicatesHint: 'Todas as condições precisam corresponder a leituras do mesmo instante de origem. Os intervalos numéricos usam a unidade atual do TAG.', noContextPredicates: 'Adicione ao menos uma condição para descrever este contexto operacional.', predicateTag: 'Medição do perfil associado', choosePredicateTag: 'Escolha uma medição', predicateKind: 'Tipo de condição', NumericInterval: 'Intervalo numérico, incluindo os dois limites', BooleanEquals: 'Verdadeiro ou falso', StringEquals: 'Texto exato', EnumEquals: 'Opção exata', minimumInclusive: 'De (incluindo o limite)', maximumInclusive: 'Até (incluindo o limite)', predicateValue: 'Valor que deve corresponder', exactValue: 'Valor exato', matchTrue: 'Verdadeiro', matchFalse: 'Falso', addPredicate: 'Adicionar condição', deleteContextProfile: 'Excluir perfil de contexto', contextVersionConflict: 'Este contexto foi alterado em outra edição. Atualize a tela e confira as condições antes de salvar.'
  } as const;
}

function processStageCoverageText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    generate: 'Create stage coverage screen',
    unavailable: 'Choose a process stage with one to 32 complete, unique Equipment assignments and profiles.'
  };
  if (locale === 'es') return {
    generate: 'Crear pantalla de cobertura de etapa',
    unavailable: 'Elija una etapa con una a 32 asignaciones de equipo y perfiles completos y únicos.'
  };
  return {
    generate: 'Criar tela de cobertura da etapa',
    unavailable: 'Escolha uma etapa com uma a 32 associações de Equipamento e perfis completos e únicos.'
  };
}

function guidedProcessStageDashboardText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    generate: 'Create guided stage dashboard',
    generateInsights: 'Create stage member insights',
    hint: 'Configure a Performance rule and contextual-deviation ranges for every member before generating member insights.'
  };
  if (locale === 'es') return {
    generate: 'Crear panel guiado de etapa',
    generateInsights: 'Crear análisis de miembros de etapa',
    hint: 'Configure una regla de rendimiento y rangos de desviación contextual para cada miembro antes de generar los análisis.'
  };
  return {
    generate: 'Criar painel guiado da etapa',
    generateInsights: 'Criar análises dos membros da etapa',
    hint: 'Configure uma regra de desempenho e os intervalos de desvio contextual de cada membro antes de gerar as análises.'
  };
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
