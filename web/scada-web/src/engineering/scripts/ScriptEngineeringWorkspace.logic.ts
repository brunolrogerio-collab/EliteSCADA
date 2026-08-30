import type {
  CanonicalScriptPackage,
  ScriptEngineeringDefinition,
  ScriptEngineeringDependency,
  ScriptEngineeringDependencyKind,
  ScriptEngineeringEntryPoint,
  ScriptEngineeringEventKind,
  ScriptEngineeringScope,
  ScriptImportMode,
  ScriptMutationPreviewToken,
  ScriptVisualEventReference
} from './scriptEngineeringTypes';

export const SCRIPT_SCOPES: ScriptEngineeringScope[] = ['clientVisual', 'server'];
export const SCRIPT_EVENT_KINDS: ScriptEngineeringEventKind[] = [
  'initialize',
  'dispose',
  'objectInteraction',
  'tagChanged',
  'clientMemoryChanged',
  'timer',
  'propertyChanged',
  'frameTick',
  'serverRuntimeEvent'
];
export const SCRIPT_DEPENDENCY_KINDS: ScriptEngineeringDependencyKind[] = [
  'script',
  'visualDefinition',
  'visualObject',
  'tag',
  'clientMemoryTag',
  'serverMemoryTag',
  'resource'
];

export const MINIMUM_SCRIPT_TIMER_INTERVAL_MS = 50;

const numericScopes: ScriptEngineeringScope[] = ['clientVisual', 'server'];
const numericEvents: ScriptEngineeringEventKind[] = SCRIPT_EVENT_KINDS;
const numericDependencies: ScriptEngineeringDependencyKind[] = SCRIPT_DEPENDENCY_KINDS;

export function normalizeScope(value: unknown): ScriptEngineeringScope {
  if (value === 'ClientVisual' || value === 'clientVisual' || value === 0) return 'clientVisual';
  if (value === 'Server' || value === 'server' || value === 1) return 'server';
  if (typeof value === 'number' && numericScopes[value]) return numericScopes[value];
  return 'clientVisual';
}

export function normalizeEventKind(value: unknown): ScriptEngineeringEventKind {
  if (typeof value === 'number' && numericEvents[value]) return numericEvents[value];
  if (typeof value === 'string') {
    const normalized = value.charAt(0).toLowerCase() + value.slice(1);
    if (SCRIPT_EVENT_KINDS.includes(normalized as ScriptEngineeringEventKind))
      return normalized as ScriptEngineeringEventKind;
  }
  return 'initialize';
}

export function normalizeDependencyKind(value: unknown): ScriptEngineeringDependencyKind {
  if (typeof value === 'number' && numericDependencies[value]) return numericDependencies[value];
  if (typeof value === 'string') {
    const normalized = value.charAt(0).toLowerCase() + value.slice(1);
    if (SCRIPT_DEPENDENCY_KINDS.includes(normalized as ScriptEngineeringDependencyKind))
      return normalized as ScriptEngineeringDependencyKind;
  }
  return 'script';
}

export function normalizeScriptDefinition(raw: Record<string, unknown>): ScriptEngineeringDefinition {
  return {
    id: String(raw.id ?? ''),
    path: String(raw.path ?? ''),
    name: String(raw.name ?? ''),
    scope: normalizeScope(raw.scope),
    source: String(raw.source ?? ''),
    enabled: raw.enabled !== false,
    language: String(raw.language ?? 'python'),
    languageVersion: String(raw.languageVersion ?? '3'),
    entryPoints: Array.isArray(raw.entryPoints)
      ? raw.entryPoints.map(item => normalizeEntryPoint(item as Record<string, unknown>))
      : [],
    dependencies: Array.isArray(raw.dependencies)
      ? raw.dependencies.map(item => normalizeDependency(item as Record<string, unknown>))
      : [],
    description: typeof raw.description === 'string' ? raw.description : null,
    metadata: normalizeMetadata(raw.metadata)
  };
}

export function normalizeVisualEventReference(raw: Record<string, unknown>): ScriptVisualEventReference {
  return {
    visualDefinitionId: String(raw.visualDefinitionId ?? ''),
    visualObjectId: raw.visualObjectId ? String(raw.visualObjectId) : null,
    eventKind: normalizeEventKind(raw.eventKind),
    scriptId: String(raw.scriptId ?? ''),
    entryPoint: String(raw.entryPoint ?? ''),
    targetReference: typeof raw.targetReference === 'string' ? raw.targetReference : null,
    tagReference: normalizeTagReference(raw.tagReference),
    timerIntervalMs: normalizeTimerInterval(raw.timerIntervalMs)
  };
}

export function createNewScriptDefinition(): ScriptEngineeringDefinition {
  return {
    id: createStableId(),
    path: 'scripts/new-script.py',
    name: 'New Script',
    scope: 'clientVisual',
    source: 'pass\n',
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [],
    dependencies: [],
    description: '',
    metadata: {}
  };
}

export function cloneScriptDefinition(script: ScriptEngineeringDefinition): ScriptEngineeringDefinition {
  return {
    ...script,
    entryPoints: script.entryPoints.map(entry => ({
      ...entry,
      tagReference: entry.tagReference
        ? { ...entry.tagReference, selector: entry.tagReference.selector ? { ...entry.tagReference.selector } : null }
        : null
    })),
    dependencies: script.dependencies.map(dependency => ({ ...dependency })),
    metadata: { ...script.metadata }
  };
}

export function scriptMutationMode(
  script: ScriptEngineeringDefinition,
  existingScripts: readonly ScriptEngineeringDefinition[]
): ScriptImportMode {
  return existingScripts.some(existing => existing.id === script.id) ? 'UpdateExisting' : 'CreateOnly';
}

export function buildCanonicalScriptPackage(
  script: ScriptEngineeringDefinition,
  allVisualReferences: readonly ScriptVisualEventReference[],
  exportedAt = new Date().toISOString()
): CanonicalScriptPackage {
  const ownedReferences = allVisualReferences
    .filter(reference => reference.scriptId === script.id)
    .map(reference => ({
      ...reference,
      tagReference: reference.tagReference
        ? { ...reference.tagReference, selector: reference.tagReference.selector ? { ...reference.tagReference.selector } : null }
        : null
    }));

  return {
    schema: 'scada.engineering',
    schemaVersion: 10,
    exportedAt,
    tags: [],
    alarms: [],
    scripts: [{
      id: script.id,
      path: script.path,
      name: script.name,
      scope: script.scope,
      source: script.source,
      enabled: script.enabled,
      language: script.language || 'python',
      languageVersion: script.languageVersion || '3',
      entryPoints: script.entryPoints.map(entry => ({
        ...entry,
        tagReference: entry.tagReference
          ? { ...entry.tagReference, selector: entry.tagReference.selector ? { ...entry.tagReference.selector } : null }
          : null
      })),
      dependencies: script.dependencies.map(dependency => ({ ...dependency })),
      description: script.description?.trim() ? script.description : null,
      metadata: { ...script.metadata }
    }],
    scriptVisualEventReferences: ownedReferences
  };
}

export function canonicalScriptPackageFingerprint(packageData: CanonicalScriptPackage): string {
  return JSON.stringify({
    ...packageData,
    exportedAt: '<preview-time>'
  });
}

export function previewTokenMatches(
  token: ScriptMutationPreviewToken | null,
  packageData: CanonicalScriptPackage,
  mode: ScriptImportMode
): boolean {
  if (!token || token.mode !== mode) return false;
  return token.packageFingerprint === canonicalScriptPackageFingerprint(packageData);
}

export function validateScriptDraft(script: ScriptEngineeringDefinition): string[] {
  const issues: string[] = [];
  if (!script.id.trim()) issues.push('id');
  if (!script.name.trim()) issues.push('name');
  if (!script.path.trim() || script.path !== script.path.trim() || script.path.includes('\\')) issues.push('path');
  if (!script.source.trim()) issues.push('source');
  if (!script.languageVersion.trim()) issues.push('languageVersion');

  const entryKeys = new Set<string>();
  for (const entry of script.entryPoints) {
    if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(entry.handlerName.trim())) issues.push('entryPoint');
    issues.push(...validateEventTarget(entry.eventKind, entry.targetReference, entry.tagReference, entry.timerIntervalMs));
    const key = eventAssociationIdentity(entry.eventKind, entry.handlerName.trim(), entry.targetReference, entry.tagReference, entry.timerIntervalMs);
    if (entryKeys.has(key)) issues.push('entryPointDuplicate');
    entryKeys.add(key);
  }

  const dependencyKeys = new Set<string>();
  for (const dependency of script.dependencies) {
    if (!dependency.stableReference.trim()) issues.push('dependency');
    const key = `${dependency.kind}|${dependency.stableReference.trim()}`;
    if (dependencyKeys.has(key)) issues.push('dependencyDuplicate');
    dependencyKeys.add(key);
  }
  return Array.from(new Set(issues));
}

export function validateVisualEventReference(reference: ScriptVisualEventReference): string[] {
  const issues: string[] = [];
  if (!reference.visualDefinitionId.trim()) issues.push('visualDefinitionId');
  if (!reference.scriptId.trim()) issues.push('scriptId');
  if (!reference.entryPoint.trim()) issues.push('entryPoint');
  issues.push(...validateEventTarget(
    reference.eventKind,
    reference.targetReference,
    reference.tagReference,
    reference.timerIntervalMs
  ));
  return Array.from(new Set(issues));
}

export function scriptSearchText(script: ScriptEngineeringDefinition): string {
  return [script.name, script.path, script.scope, script.description ?? ''].join(' ').toLowerCase();
}

function normalizeEntryPoint(raw: Record<string, unknown>): ScriptEngineeringEntryPoint {
  return {
    eventKind: normalizeEventKind(raw.eventKind),
    handlerName: String(raw.handlerName ?? ''),
    targetReference: typeof raw.targetReference === 'string' ? raw.targetReference : null,
    tagReference: normalizeTagReference(raw.tagReference),
    timerIntervalMs: normalizeTimerInterval(raw.timerIntervalMs)
  };
}

function normalizeDependency(raw: Record<string, unknown>): ScriptEngineeringDependency {
  return {
    kind: normalizeDependencyKind(raw.kind),
    stableReference: String(raw.stableReference ?? '')
  };
}

function normalizeTagReference(value: unknown): ScriptEngineeringEntryPoint['tagReference'] {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
  const raw = value as Record<string, unknown>;
  const tagId = typeof raw.tagId === 'string' ? raw.tagId : '';
  if (!tagId) return null;

  let selector: NonNullable<ScriptEngineeringEntryPoint['tagReference']>['selector'] = null;
  if (raw.selector && typeof raw.selector === 'object' && !Array.isArray(raw.selector)) {
    const rawSelector = raw.selector as Record<string, unknown>;
    if (typeof rawSelector.kind === 'string' && typeof rawSelector.index === 'number') {
      selector = { kind: rawSelector.kind, index: rawSelector.index };
    }
  }

  return { tagId, selector };
}

function normalizeTimerInterval(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function validateEventTarget(
  eventKind: ScriptEngineeringEventKind,
  targetReference: string | null | undefined,
  tagReference: ScriptEngineeringEntryPoint['tagReference'],
  timerIntervalMs: number | null | undefined
): string[] {
  const issues: string[] = [];

  if (eventKind === 'timer') {
    if (!Number.isInteger(timerIntervalMs) || (timerIntervalMs ?? 0) < MINIMUM_SCRIPT_TIMER_INTERVAL_MS)
      issues.push('timerIntervalMs');
    if (tagReference) issues.push('tagReferenceUnexpected');
    if (targetReference?.trim()) issues.push('targetReferenceUnexpected');
    return issues;
  }

  if (timerIntervalMs != null) issues.push('timerIntervalUnexpected');

  if (eventKind === 'tagChanged') {
    if (!tagReference?.tagId?.trim()) issues.push('tagReference');
    if (targetReference?.trim()) issues.push('targetReferenceUnexpected');
    const selector = tagReference?.selector;
    if (selector && (selector.kind !== 'bit' || !Number.isInteger(selector.index) || selector.index < 0))
      issues.push('tagSelector');
    return issues;
  }

  if (tagReference) issues.push('tagReferenceUnexpected');

  if (eventKind === 'clientMemoryChanged') {
    if (!targetReference?.trim()) issues.push('targetReference');
  }

  return issues;
}

function eventAssociationIdentity(
  eventKind: ScriptEngineeringEventKind,
  handlerName: string,
  targetReference: string | null | undefined,
  tagReference: ScriptEngineeringEntryPoint['tagReference'],
  timerIntervalMs: number | null | undefined
): string {
  const selector = tagReference?.selector ? `${tagReference.selector.kind}:${tagReference.selector.index}` : '';
  return [
    eventKind,
    handlerName,
    targetReference ?? '',
    tagReference?.tagId ?? '',
    selector,
    timerIntervalMs ?? ''
  ].join('|');
}

function normalizeMetadata(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>)
      .filter(([, item]) => typeof item === 'string')
      .map(([key, item]) => [key, item as string])
  );
}

function createStableId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, char => {
    const random = Math.floor(Math.random() * 16);
    const value = char === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}