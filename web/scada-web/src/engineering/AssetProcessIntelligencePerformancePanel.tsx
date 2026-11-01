import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import type {
  AssetProcessIntelligenceProfileEngineering,
  AssetProcessIntelligenceSignalEngineering,
  AssetProcessIntelligenceSignalSemanticBinding,
  TagEngineering
} from './types';
import type { EngineeringLocale } from './i18n';
import type { VersionedSemanticCatalog } from './semanticCatalogApi';
import { isNumericTagDataType, type IntelligencePerformanceDashboardSelection } from './intelligenceDashboardGenerator';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const DEFAULT_MAX_WINDOW_DAYS = 31;

type PerformanceInputUnit = {
  role: string;
  unitCatalogVersions: number[];
  acceptedUnitCodes: string[];
};
export type PerformanceRule = {
  id: string;
  version: string;
  kind: string;
  formula: string;
  inputRoles: string[];
  inputUnits: PerformanceInputUnit[];
  outputUnit: string;
  outputUnitVariants: Array<{ productionRole: string; canonicalInputUnit: string; outputUnit: string }>;
  temporalRequirement: string;
  minimumQualifiedTuples: number;
  bounds: string;
  unavailableStates: string[];
};
export type PerformanceCatalog = {
  contractVersion: number;
  catalogVersion: number;
  ruleVersion: string;
  dataset: string;
  datasetContractVersion: number;
  maximumWindowDays: number;
  maximumRawRowsAcrossSelectedTags: number;
  rules: PerformanceRule[];
};
export type WindowValue = { fromUtc: string; toUtc: string };
export type PerformanceResponse = {
  contractVersion: number;
  catalogVersion: number;
  ruleId: string;
  ruleVersion: string;
  projectKey: string;
  activeRevision: number;
  scopeKind: string;
  scopeId: string;
  assignmentId: string;
  equipmentId: string;
  profileId: string;
  window: WindowValue;
  signals: Array<{ signalId: string; signalKey: string; roleCode: string; canonicalUnit: string }>;
  inputs: Array<{
    signalId: string;
    rawObservationCount: number;
    eligibleSynchronizedCount: number;
    pairedCount: number;
    qualifiedCount: number;
    qualityCounts: Record<string, number>;
    rejectionCounts: Record<string, number>;
  }>;
  totalRawObservationCount: number;
  summary: {
    status: string;
    unit: string;
    reasonCodes: string[];
    qualifiedTupleCount: number;
    minimum: number | null;
    maximum: number | null;
    arithmeticMean: number | null;
    meanSemantics: string;
  };
  provenance: {
    dataset: string;
    datasetContractVersion: number;
    timestampPairing: string;
    acquisitionAlignment: string;
    operatingRegimeEquivalence: string;
    expectedCadence: string;
    completeness: string;
    measurementInterpretation: string;
  };
};
type RuleSelection = { rule: PerformanceRule; signalIds: string[]; reason: string | null };

export function AssetProcessIntelligencePerformancePanel({
  locale,
  scopeId,
  assignmentId,
  profile,
  tags,
  semanticCatalogs,
  projectKey,
  activeRevision,
  canQuery,
  onCreateRuntimeScreenDraft,
  onGuidedSelectionChange
}: {
  locale: EngineeringLocale;
  scopeId: string;
  assignmentId: string;
  profile: AssetProcessIntelligenceProfileEngineering;
  tags: readonly TagEngineering[];
  semanticCatalogs: readonly VersionedSemanticCatalog[];
  projectKey: string | null;
  activeRevision: number | null;
  canQuery: boolean;
  onCreateRuntimeScreenDraft?: (selection: Readonly<{ ruleId: string; ruleVersion: string; signalIds: readonly string[] }>) => void;
  onGuidedSelectionChange?: (assignmentId: string, selection: IntelligencePerformanceDashboardSelection | null) => void;
}) {
  const copy = useMemo(() => performanceCopy(locale), [locale]);
  const [expanded, setExpanded] = useState(false);
  const [catalog, setCatalog] = useState<PerformanceCatalog | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [ruleId, setRuleId] = useState('');
  const [fromLocal, setFromLocal] = useState(() => localDateTimeInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [toLocal, setToLocal] = useState(() => localDateTimeInput(new Date()));
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<PerformanceResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const catalogController = useRef<AbortController | null>(null);
  const queryController = useRef<AbortController | null>(null);
  const requestVersion = useRef(0);

  useEffect(() => {
    requestVersion.current += 1;
    queryController.current?.abort();
    queryController.current = null;
    setLoading(false);
    setResult(null);
    setError(null);
    return () => queryController.current?.abort();
  }, [scopeId, assignmentId, profile.id, profile.signals, tags, semanticCatalogs, projectKey, activeRevision, canQuery]);

  useEffect(() => () => {
    catalogController.current?.abort();
    queryController.current?.abort();
  }, []);

  const selections = useMemo(() => (catalog?.rules ?? []).map(rule => ({
    rule,
    ...resolveRuleSelection(rule, profile.signals, tags, semanticCatalogs, copy)
  })), [catalog, profile.signals, tags, semanticCatalogs, copy]);
  const selected = selections.find(item => item.rule.id === ruleId) ?? null;
  const windowResult = useMemo(() => parseWindow(fromLocal, toLocal, catalog?.maximumWindowDays ?? DEFAULT_MAX_WINDOW_DAYS),
    [fromLocal, toLocal, catalog?.maximumWindowDays]);

  useEffect(() => {
    if (!selected || selected.reason || !scopeId || !assignmentId || !profile.id) {
      onGuidedSelectionChange?.(assignmentId, null);
      return;
    }
    onGuidedSelectionChange?.(assignmentId, {
      scopeId,
      assignmentId,
      profileId: profile.id,
      ruleId: selected.rule.id,
      ruleVersion: selected.rule.version,
      signalIds: selected.signalIds,
      displayName: profile.name
    });
  }, [assignmentId, onGuidedSelectionChange, profile.id, profile.name, scopeId, selected]);

  const invalidateResult = () => {
    requestVersion.current += 1;
    queryController.current?.abort();
    queryController.current = null;
    setLoading(false);
    setResult(null);
    setError(null);
  };

  const openPanel = () => {
    setExpanded(true);
    if (catalog || catalogLoading) return;
    const controller = new AbortController();
    catalogController.current?.abort();
    catalogController.current = controller;
    setCatalogLoading(true);
    setCatalogError(null);
    void fetch(`${API}/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog`, {
      credentials: 'include', headers: { accept: 'application/json' }, signal: controller.signal
    }).then(async response => {
      if (!response.ok) throw new Error(`HTTP_${response.status}`);
      const payload = await response.json() as PerformanceCatalog;
      if (!isValidCatalog(payload)) throw new Error('INVALID_CATALOG');
      setCatalog(payload);
    }).catch(reason => {
      if (!controller.signal.aborted) setCatalogError(reason instanceof Error ? reason.message : copy.catalogUnavailable);
    }).finally(() => {
      if (!controller.signal.aborted) setCatalogLoading(false);
    });
  };

  const closePanel = () => {
    invalidateResult();
    catalogController.current?.abort();
    catalogController.current = null;
    setCatalogLoading(false);
    setExpanded(false);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canQuery || !projectKey || activeRevision === null || !selected || selected.reason || !windowResult.window) return;
    invalidateResult();
    const controller = new AbortController();
    queryController.current = controller;
    const version = ++requestVersion.current;
    setLoading(true);
    try {
      const response = await fetch(`${API}/api/engineering/asset-process-intelligence/analytics/performance/v1/query`, {
        method: 'POST', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({
          scopeKind: 0,
          scopeId,
          assignmentId,
          profileId: profile.id,
          ruleId: selected.rule.id,
          signalIds: selected.signalIds,
          window: windowResult.window
        }),
        signal: controller.signal
      });
      const payload = await response.json().catch(() => null) as PerformanceResponse | { code?: unknown } | null;
      if (!response.ok) {
        const code = payload && 'code' in payload && typeof payload.code === 'string' ? payload.code : `HTTP_${response.status}`;
        throw new PerformanceQueryError(code);
      }
      if (!matchesResponse(payload, scopeId, assignmentId, profile.id, projectKey, activeRevision,
          selected.rule, catalog?.catalogVersion ?? 0, selected.signalIds, windowResult.window)) {
        throw new PerformanceQueryError('INVALID_RESPONSE');
      }
      if (requestVersion.current === version) setResult(payload);
    } catch (reason) {
      if (!controller.signal.aborted && requestVersion.current === version) {
        setError(reason instanceof PerformanceQueryError ? errorText(reason.code, copy) : copy.networkError);
      }
    } finally {
      if (requestVersion.current === version) setLoading(false);
    }
  };

  const selectRule = (value: string) => { invalidateResult(); setRuleId(value); };
  const updateWindow = (setter: (value: string) => void, value: string) => { invalidateResult(); setter(value); };

  return <section className="asset-process-intelligence__performance" data-testid={`intelligence-performance-${profile.id}`}>
    {!expanded ? <button type="button" className="secondary" aria-expanded="false" onClick={openPanel}>{copy.open}</button> : <>
      <button type="button" className="secondary" aria-expanded="true" onClick={closePanel}>{copy.hide}</button>
      <div className="asset-process-intelligence__performance-body">
        <p>{copy.description}</p>
        {!canQuery ? <p className="asset-process-intelligence__coverage-note" role="note">{copy.blocked}</p> : null}
        {catalogLoading ? <p role="status">{copy.loadingCatalog}</p> : null}
        {catalogError ? <p className="asset-process-intelligence__coverage-error" role="alert">{copy.catalogUnavailable}: {catalogError}</p> : null}
        {catalog ? <form className="asset-process-intelligence__performance-form" onSubmit={event => void submit(event)}>
          <label>{copy.rule}
            <select aria-label={copy.rule} value={ruleId} onChange={event => selectRule(event.currentTarget.value)} disabled={!canQuery || loading}>
              <option value="">{copy.chooseRule}</option>
              {selections.map(item => <option key={item.rule.id} value={item.rule.id} disabled={Boolean(item.reason)}>
                {ruleTitle(item.rule.id, copy)}{item.reason ? ` — ${item.reason}` : ''}
              </option>)}
            </select>
          </label>
          {selected ? <div className="asset-process-intelligence__performance-rule">
            <p><strong>{copy.formula}:</strong> {selected.rule.formula}</p>
            <p><strong>{copy.inputSelection}:</strong> {selected.reason ? selected.reason : selected.signalIds.map(id => {
              const signal = profile.signals.find(item => item.id.toLowerCase() === id.toLowerCase());
              const tag = signal ? tags.find(item => item.id?.toLowerCase() === signal.tagId.toLowerCase()) : undefined;
              return signal?.signalRole?.trim() || tag?.path || copy.unknown;
            }).join(' · ')}</p>
            <p><strong>{copy.bounds}:</strong> {selected.rule.bounds}</p>
            <p><strong>{copy.timeRequirement}:</strong> {selected.rule.temporalRequirement}</p>
            {onCreateRuntimeScreenDraft && !selected.reason ? <button type="button" className="secondary"
              data-testid={`intelligence-performance-create-screen-${profile.id}`}
              onClick={() => onCreateRuntimeScreenDraft({
                ruleId: selected.rule.id,
                ruleVersion: selected.rule.version,
                signalIds: selected.signalIds
              })}>{copy.createRuntimeScreen}</button> : null}
          </div> : null}
          <label>{copy.from}<input type="datetime-local" value={fromLocal} disabled={!canQuery || loading} onChange={event => updateWindow(setFromLocal, event.currentTarget.value)}/></label>
          <label>{copy.to}<input type="datetime-local" value={toLocal} disabled={!canQuery || loading} onChange={event => updateWindow(setToLocal, event.currentTarget.value)}/></label>
          {!windowResult.window ? <p className="asset-process-intelligence__coverage-error" role="alert">{windowResult.error === 'missing' ? copy.windowRequired : copy.windowInvalid}</p> : null}
          <p className="asset-process-intelligence__coverage-note">{copy.utcHint}</p>
          <button type="submit" disabled={!canQuery || !selected || Boolean(selected.reason) || !windowResult.window || loading}>
            {loading ? copy.loading : copy.analyze}
          </button>
        </form> : null}
        {error ? <p className="asset-process-intelligence__coverage-error" role="alert">{error}</p> : null}
        {result && selected ? <PerformanceResult result={result} rule={selected.rule} locale={locale} copy={copy} profile={profile}/> : null}
      </div>
    </>}
  </section>;
}

function resolveRuleSelection(
  rule: PerformanceRule,
  signals: readonly AssetProcessIntelligenceSignalEngineering[],
  tags: readonly TagEngineering[],
  semanticCatalogs: readonly VersionedSemanticCatalog[],
  copy: PerformanceCopy
): Omit<RuleSelection, 'rule'> {
  const selected: AssetProcessIntelligenceSignalEngineering[] = [];
  for (const roleGroup of rule.inputRoles) {
    const alternatives = roleGroup.split('|').map(role => role.trim()).filter(Boolean);
    const matches = signals.filter(signal => signal.semanticBinding && alternatives.includes(signal.semanticBinding.roleCode));
    if (matches.length === 0) return { signalIds: [], reason: copy.inputMissing };
    if (matches.length !== 1) return { signalIds: [], reason: copy.inputAmbiguous };
    const signal = matches[0];
    const binding = signal.semanticBinding!;
    const tagMatches = tags.filter(tag => tag.id?.toLowerCase() === signal.tagId.toLowerCase());
    if (tagMatches.length !== 1 || !tagMatches[0].id) return { signalIds: [], reason: copy.tagUnavailable };
    if (!isNumericTagDataType(tagMatches[0].dataType)) return { signalIds: [], reason: copy.numericTagRequired };
    const inputUnit = rule.inputUnits.find(unit => unit.role === binding.roleCode);
    const controlledUnit = tagMatches[0].controlledEngineeringUnit;
    if (!inputUnit || !controlledUnit || !inputUnit.unitCatalogVersions.includes(controlledUnit.catalogVersion) ||
        !inputUnit.acceptedUnitCodes.includes(controlledUnit.code) ||
        !supportsPerformanceUnit(binding.roleCode, binding, controlledUnit.catalogVersion, controlledUnit.code)) {
      return { signalIds: [], reason: copy.unitUnsupported };
    }
    if (!inputUnit.unitCatalogVersions.includes(binding.catalogVersion) ||
        !isSemanticBindingComplete(binding, semanticCatalogs) || !supportsPerformanceQualifier(rule.id, binding)) {
      return { signalIds: [], reason: copy.semanticUnsupported };
    }
    selected.push(signal);
  }
  if (selected.some(signal => !signal.tagId) || new Set(selected.map(signal => signal.tagId.toLowerCase())).size !== selected.length ||
      new Set(selected.map(signal => signal.id.toLowerCase())).size !== selected.length) {
    return { signalIds: [], reason: copy.duplicateTag };
  }
  return { signalIds: selected.map(signal => signal.id), reason: null };
}

function supportsPerformanceQualifier(ruleId: string, binding: AssetProcessIntelligenceSignalSemanticBinding) {
  switch (binding.roleCode) {
    case 'equipment.active_input_power':
      return binding.catalogVersion === 2 && binding.signalKind === 'measured_total_input';
    case 'process.net_good_production_volume_rate':
    case 'process.net_good_production_mass_rate':
    case 'process.net_good_production_item_rate':
      return binding.catalogVersion === 2 && binding.signalKind === 'measured_output';
    case 'pump.discharge_volumetric_flow':
      return ruleId !== 'specific-active-energy-at-recorded-production-rate.v1' &&
        binding.catalogVersion === 1 && binding.flowDirection === 'pump_to_process';
    case 'pump.direct_differential_pressure':
      return ruleId !== 'specific-active-energy-at-recorded-production-rate.v1' &&
        binding.catalogVersion === 2 && binding.signalKind === 'measured_output' &&
        binding.pressureReference === 'differential_outlet_minus_inlet';
    default: return false;
  }
}

function supportsPerformanceUnit(
  roleCode: string,
  binding: AssetProcessIntelligenceSignalSemanticBinding,
  unitCatalogVersion: number,
  unitCode: string
) {
  switch (roleCode) {
    case 'equipment.active_input_power':
      return binding.catalogVersion === 2 && unitCatalogVersion === 2 &&
        (unitCode === 'unit.power.watt' || unitCode === 'unit.power.kilowatt');
    case 'process.net_good_production_volume_rate':
      return binding.catalogVersion === 2 && unitCatalogVersion === 2 &&
        unitCode === 'unit.volumetric_flow.cubic_metre_per_second';
    case 'process.net_good_production_mass_rate':
      return binding.catalogVersion === 2 && unitCatalogVersion === 2 &&
        (unitCode === 'unit.mass_flow.kilogram_per_second' || unitCode === 'unit.mass_flow.kilogram_per_hour');
    case 'process.net_good_production_item_rate':
      return binding.catalogVersion === 2 && unitCatalogVersion === 2 &&
        (unitCode === 'unit.item_rate.item_per_second' || unitCode === 'unit.item_rate.item_per_hour');
    case 'pump.discharge_volumetric_flow':
      return binding.catalogVersion === 1 && binding.flowDirection === 'pump_to_process' &&
        ((unitCatalogVersion === 1 && unitCode === 'unit.volumetric_flow.cubic_metre_per_hour') ||
          (unitCatalogVersion === 2 && unitCode === 'unit.volumetric_flow.cubic_metre_per_second'));
    case 'pump.direct_differential_pressure':
      return binding.catalogVersion === 2 && binding.signalKind === 'measured_output' &&
        binding.pressureReference === 'differential_outlet_minus_inlet' &&
        ((unitCatalogVersion === 1 && unitCode === 'unit.pressure.bar') ||
          (unitCatalogVersion === 2 && (unitCode === 'unit.pressure.pascal' || unitCode === 'unit.pressure.kilopascal')));
    default:
      return false;
  }
}

function isSemanticBindingComplete(
  binding: AssetProcessIntelligenceSignalSemanticBinding,
  catalogs: readonly VersionedSemanticCatalog[]
) {
  const role = catalogs.find(item => item.catalogVersion === binding.catalogVersion)?.roles
    .find(item => item.code === binding.roleCode);
  if (!role) return false;
  const supportedKeys = new Set(['currentBasis', 'signalKind', 'frequencyReference', 'pressureReference', 'flowDirection']);
  const qualifiers = role.qualifiers;
  for (const qualifier of qualifiers) {
    if (!supportedKeys.has(qualifier.name)) return false;
    const value = binding[qualifier.name as keyof AssetProcessIntelligenceSignalSemanticBinding];
    if (typeof value !== 'string' || !qualifier.allowedValues.includes(value)) return false;
  }
  return true;
}

export function isValidCatalog(value: PerformanceCatalog | null): value is PerformanceCatalog {
  return Boolean(value && value.contractVersion === 1 && value.catalogVersion === 1 &&
    Array.isArray(value.rules) && Number.isInteger(value.maximumWindowDays) && value.maximumWindowDays > 0 &&
    value.rules.every(rule => typeof rule.id === 'string' && typeof rule.formula === 'string' &&
      Array.isArray(rule.inputRoles) && Array.isArray(rule.inputUnits) && rule.inputRoles.length > 0));
}

export function matchesResponse(
  value: unknown,
  scopeId: string,
  assignmentId: string,
  profileId: string,
  projectKey: string,
  activeRevision: number,
  rule: PerformanceRule,
  catalogVersion: number,
  signalIds: readonly string[],
  window: WindowValue
): value is PerformanceResponse {
  if (!value || typeof value !== 'object') return false;
  const response = value as Partial<PerformanceResponse>;
  const expectedSignals = signalIds.map(id => id.toLowerCase()).sort();
  const actualSignals = Array.isArray(response.signals) ? response.signals.map(signal => signal.signalId?.toLowerCase()).sort() : [];
  const actualInputs = Array.isArray(response.inputs) ? response.inputs.map(input => input.signalId?.toLowerCase()).sort() : [];
  return response.contractVersion === 1 && response.catalogVersion === catalogVersion &&
    response.ruleId === rule.id && response.ruleVersion === rule.version &&
    response.projectKey === projectKey && response.activeRevision === activeRevision &&
    response.scopeKind === 'Equipment' && sameId(response.scopeId, scopeId) &&
    sameId(response.assignmentId, assignmentId) && sameId(response.profileId, profileId) &&
    sameId(response.equipmentId, scopeId) &&
    expectedSignals.length > 0 && expectedSignals.length === actualSignals.length &&
    expectedSignals.every((id, index) => id === actualSignals[index]) &&
    expectedSignals.length === actualInputs.length && expectedSignals.every((id, index) => id === actualInputs[index]) &&
    Date.parse(response.window?.fromUtc ?? '') === Date.parse(window.fromUtc) &&
    Date.parse(response.window?.toUtc ?? '') === Date.parse(window.toUtc) &&
    Array.isArray(response.signals) && response.signals.every(signal => typeof signal.signalKey === 'string' &&
      typeof signal.roleCode === 'string' && typeof signal.canonicalUnit === 'string') &&
    Array.isArray(response.inputs) && response.inputs.every(input =>
      [input.rawObservationCount, input.eligibleSynchronizedCount, input.pairedCount, input.qualifiedCount].every(isNonNegativeInteger) &&
      isCountMap(input.qualityCounts) && isCountMap(input.rejectionCounts)) &&
    Number.isInteger(response.totalRawObservationCount) && (response.totalRawObservationCount ?? -1) >= 0 &&
    Boolean(response.summary && typeof response.summary.status === 'string' && typeof response.summary.unit === 'string' &&
      Array.isArray(response.summary.reasonCodes) && response.summary.reasonCodes.every(code => typeof code === 'string') &&
      isNonNegativeInteger(response.summary.qualifiedTupleCount) &&
      [response.summary.minimum, response.summary.maximum, response.summary.arithmeticMean].every(value =>
        value === null || (typeof value === 'number' && Number.isFinite(value))) && typeof response.summary.meanSemantics === 'string') &&
    Boolean(response.provenance && typeof response.provenance.dataset === 'string' &&
      Number.isInteger(response.provenance.datasetContractVersion) && typeof response.provenance.timestampPairing === 'string' &&
      typeof response.provenance.acquisitionAlignment === 'string' && typeof response.provenance.operatingRegimeEquivalence === 'string' &&
      typeof response.provenance.expectedCadence === 'string' && typeof response.provenance.completeness === 'string' &&
      typeof response.provenance.measurementInterpretation === 'string');
}

function isNonNegativeInteger(value: unknown): value is number {
  return Number.isInteger(value) && (value as number) >= 0;
}

function isCountMap(value: unknown): value is Record<string, number> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value) &&
    Object.values(value as Record<string, unknown>).every(isNonNegativeInteger));
}

function sameId(left: string | null | undefined, right: string) {
  return typeof left === 'string' && left.toLowerCase() === right.toLowerCase();
}

export function PerformanceResult({ result, rule, locale, copy, profile }: {
  result: PerformanceResponse;
  rule: PerformanceRule;
  locale: EngineeringLocale;
  copy: PerformanceCopy;
  profile?: AssetProcessIntelligenceProfileEngineering;
}) {
  const localeCode = locale === 'en' ? 'en-US' : locale === 'es' ? 'es-ES' : 'pt-BR';
  const number = (value: number | null | undefined) => value === null || value === undefined || !Number.isFinite(value)
    ? copy.unknown : new Intl.NumberFormat(localeCode, { maximumFractionDigits: 8 }).format(value);
  const signalById = new Map((profile?.signals ?? []).map(signal => [signal.id.toLowerCase(), signal]));
  const signalDescription = new Map(result.signals.map(signal => [signal.signalId.toLowerCase(), signal]));
  return <div className="asset-process-intelligence__performance-result" data-testid="intelligence-performance-result">
    <p role="status"><strong>{copy.queryWindow}</strong> {formatUtc(result.window.fromUtc, localeCode)} – {formatUtc(result.window.toUtc, localeCode)}</p>
    <section>
      <h4>{ruleTitle(result.ruleId, copy)} · {result.summary.unit || copy.unknown}</h4>
      <p><strong>{copy.status}:</strong> {describePerformanceStatus(result.summary.status, locale)}</p>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.qualifiedTuples} value={number(result.summary.qualifiedTupleCount)}/>
        <Fact label={copy.minimum} value={number(result.summary.minimum)}/>
        <Fact label={copy.maximum} value={number(result.summary.maximum)}/>
        <Fact label={copy.arithmeticMean} value={number(result.summary.arithmeticMean)}/>
        <Fact label={copy.meanSemantics} value={describeMeanSemantics(locale)}/>
        <Fact label={copy.totalRawObservations} value={number(result.totalRawObservationCount)}/>
      </dl>
      <p><strong>{copy.formula}:</strong> {describePerformanceFormula(rule.id, locale)}</p>
      <p><strong>{copy.reasonCodes}:</strong> {result.summary.reasonCodes.length
        ? result.summary.reasonCodes.map(code => describePerformanceCode(code, locale)).join(' · ')
        : copy.none}</p>
    </section>
    <section>
      <h4>{copy.inputs}</h4>
      <div className="asset-process-intelligence__performance-inputs">
        {result.inputs.map(input => {
          const signal = signalById.get(input.signalId.toLowerCase());
          const description = signalDescription.get(input.signalId.toLowerCase());
          const label = describeMeasurementRole(description?.roleCode, locale) || signal?.signalRole?.trim() || copy.unknown;
          return <article key={input.signalId}>
            <h5>{label}</h5>
            <p>{description?.canonicalUnit ?? copy.unknown}</p>
            <dl className="asset-process-intelligence__coverage-facts">
              <Fact label={copy.rawObservations} value={number(input.rawObservationCount)}/>
              <Fact label={copy.eligibleSynchronized} value={number(input.eligibleSynchronizedCount)}/>
              <Fact label={copy.paired} value={number(input.pairedCount)}/>
              <Fact label={copy.qualified} value={number(input.qualifiedCount)}/>
            </dl>
            <p><strong>{copy.qualityCounts}:</strong> {formatCounts(input.qualityCounts, copy, locale)}</p>
            <p><strong>{copy.rejectionCounts}:</strong> {formatCounts(input.rejectionCounts, copy, locale)}</p>
          </article>;
        })}
      </div>
    </section>
    <section>
      <h4>{copy.provenance}</h4>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.dataset} value={describePerformanceDataset(result.provenance.dataset, locale)}/>
        <Fact label={copy.timestampPairing} value={describePerformanceEvidence(result.provenance.timestampPairing, locale)}/>
        <Fact label={copy.acquisitionAlignment} value={describePerformanceEvidence(result.provenance.acquisitionAlignment, locale)}/>
        <Fact label={copy.regimeEquivalence} value={describePerformanceEvidence(result.provenance.operatingRegimeEquivalence, locale)}/>
        <Fact label={copy.expectedCadence} value={describePerformanceEvidence(result.provenance.expectedCadence, locale)}/>
        <Fact label={copy.completeness} value={describePerformanceEvidence(result.provenance.completeness, locale)}/>
        <Fact label={copy.measurementInterpretation} value={describeMeasurementInterpretation(result.ruleId, locale)}/>
      </dl>
      <p className="asset-process-intelligence__coverage-note">{copy.nonClaim}</p>
    </section>
  </div>;
}

function describeMeasurementRole(roleCode: string | undefined, locale: EngineeringLocale): string | null {
  const labels: Record<string, Partial<Record<EngineeringLocale, string>>> = {
    'equipment.active_input_power': { 'pt-BR': 'Potência ativa de entrada', en: 'Active input power', es: 'Potencia activa de entrada' },
    'process.net_good_production_volume_rate': { 'pt-BR': 'Volume de produção aprovada por tempo', en: 'Good production volume rate', es: 'Volumen de producción buena por tiempo' },
    'process.net_good_production_mass_rate': { 'pt-BR': 'Massa de produção aprovada por tempo', en: 'Good production mass rate', es: 'Masa de producción buena por tiempo' },
    'process.net_good_production_item_rate': { 'pt-BR': 'Unidades aprovadas por tempo', en: 'Good units per time', es: 'Unidades buenas por tiempo' },
    'pump.electrical_current': { 'pt-BR': 'Corrente elétrica', en: 'Electrical current', es: 'Corriente eléctrica' },
    'pump.drive_frequency': { 'pt-BR': 'Frequência de acionamento', en: 'Drive frequency', es: 'Frecuencia de accionamiento' },
    'pump.discharge_pressure': { 'pt-BR': 'Pressão de descarga', en: 'Discharge pressure', es: 'Presión de descarga' },
    'pump.discharge_volumetric_flow': { 'pt-BR': 'Vazão volumétrica', en: 'Volumetric flow', es: 'Caudal volumétrico' },
    'pump.direct_differential_pressure': { 'pt-BR': 'Pressão diferencial da bomba', en: 'Pump differential pressure', es: 'Presión diferencial de la bomba' }
  };
  return roleCode ? labels[roleCode]?.[locale] ?? null : null;
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>;
}

function formatCounts(values: Record<string, number>, copy: PerformanceCopy, locale: EngineeringLocale) {
  return Object.entries(values ?? {}).sort(([a], [b]) => a.localeCompare(b))
    .map(([key, value]) => `${describePerformanceCode(key, locale)}: ${new Intl.NumberFormat(locale === 'en' ? 'en-US' : locale === 'es' ? 'es-ES' : 'pt-BR').format(value)}`).join(' · ') || copy.none;
}

function describePerformanceStatus(status: string, locale: EngineeringLocale): string {
  if (status === 'available') return locale === 'pt-BR' ? 'Dados suficientes' : locale === 'es' ? 'Datos suficientes' : 'Sufficient data';
  if (status === 'insufficient_data') return locale === 'pt-BR' ? 'Dados insuficientes' : locale === 'es' ? 'Datos insuficientes' : 'Insufficient data';
  return humanizePerformanceEvidence(status, locale);
}

function describePerformanceDataset(dataset: string, locale: EngineeringLocale): string {
  if (dataset === 'historian.samples' || dataset === 'historian.samples.v2') {
    return locale === 'pt-BR' ? 'Amostras do Historian' : locale === 'es' ? 'Muestras del Historian' : 'Historian samples';
  }
  return humanizePerformanceEvidence(dataset, locale);
}

function describePerformanceEvidence(value: string, locale: EngineeringLocale): string {
  const normalized = value.trim().toLowerCase();
  if (normalized.startsWith('exact equality of each selected tag')) {
    return locale === 'pt-BR'
      ? 'Só são combinadas leituras com o mesmo horário de origem, marcado como sincronizado. Não há interpolação nem preenchimento de valores.'
      : locale === 'es'
        ? 'Solo se combinan lecturas con la misma hora de origen marcada como sincronizada. No se interpolan ni completan valores.'
        : 'Only readings with the same source time marked as synchronized are combined. Values are not interpolated or carried forward.';
  }
  if (normalized === 'exact_synchronized_source_timestamp') {
    return locale === 'pt-BR'
      ? 'Combina leituras com o mesmo horário de origem marcado como sincronizado.'
      : locale === 'es'
        ? 'Combina lecturas con la misma hora de origen marcada como sincronizada.'
        : 'Combines readings with the same source time marked as synchronized.';
  }
  if (normalized.startsWith('exact equality of distinct synchronized source timestamps')) {
    return locale === 'pt-BR' ? 'Combina leituras que têm o mesmo horário de origem sincronizado.'
      : locale === 'es' ? 'Combina lecturas que tienen la misma hora de origen sincronizada.'
        : 'Combines readings that have the same synchronized source time.';
  }
  if (normalized.startsWith('unverified')) {
    return locale === 'pt-BR' ? 'Não verificado: horários iguais não comprovam aquisição simultânea nem relógios sincronizados.'
      : locale === 'es' ? 'No verificado: las horas coincidentes no prueban adquisición simultánea ni relojes sincronizados.'
        : 'Not verified: matching timestamps do not prove simultaneous acquisition or synchronized clocks.';
  }
  if (normalized === 'unknown') return locale === 'pt-BR' ? 'Não informado' : locale === 'es' ? 'No informado' : 'Not provided';
  if (normalized === 'unknown_without_expected_cadence') {
    return locale === 'pt-BR' ? 'Indeterminada: não há cadência esperada configurada para comparação.'
      : locale === 'es' ? 'Indeterminada: no hay una frecuencia esperada configurada para comparar.'
        : 'Unknown: no expected sampling interval is configured for comparison.';
  }
  return humanizePerformanceEvidence(value, locale);
}

function describeMeasurementInterpretation(ruleId: string, locale: EngineeringLocale): string {
  const text: Record<string, Record<EngineeringLocale, string>> = {
    'specific-active-energy-at-recorded-production-rate.v1': {
      'pt-BR': 'Potência elétrica ativa registrada dividida pela taxa registrada de produção aprovada. Não representa a energia total consumida no período.',
      en: 'Recorded active electrical power divided by the recorded rate of accepted production. It is not total energy consumed over the period.',
      es: 'Potencia eléctrica activa registrada dividida por la tasa registrada de producción aceptada. No representa la energía total consumida en el período.'
    },
    'recorded-differential-pressure-power.v1': {
      'pt-BR': 'Vazão multiplicada pela pressão diferencial medida diretamente. É potência associada à pressão, não potência total nem eficiência da bomba.',
      en: 'Flow multiplied by directly measured differential pressure. This is pressure-related power, not total pump power or efficiency.',
      es: 'Caudal multiplicado por la presión diferencial medida directamente. Es potencia asociada a la presión, no potencia total ni eficiencia de la bomba.'
    },
    'recorded-pressure-power-input-ratio.v1': {
      'pt-BR': 'Compara a potência associada à pressão com a potência elétrica ativa medida. Não representa a eficiência total da bomba ou do sistema.',
      en: 'Compares pressure-related power with measured active electrical power. It is not the total efficiency of the pump or system.',
      es: 'Compara la potencia asociada a la presión con la potencia eléctrica activa medida. No representa la eficiencia total de la bomba o del sistema.'
    }
  };
  return text[ruleId]?.[locale] ?? humanizePerformanceEvidence(ruleId, locale);
}

function describePerformanceFormula(ruleId: string, locale: EngineeringLocale): string {
  const text: Record<string, Record<EngineeringLocale, string>> = {
    'specific-active-energy-at-recorded-production-rate.v1': {
      'pt-BR': 'Potência elétrica ativa ÷ produção aprovada por segundo. O resultado é kWh por unidade produzida.',
      en: 'Active electrical power ÷ accepted production per second. The result is kWh per produced unit.',
      es: 'Potencia eléctrica activa ÷ producción aceptada por segundo. El resultado es kWh por unidad producida.'
    },
    'recorded-differential-pressure-power.v1': {
      'pt-BR': 'Vazão × diferença entre a pressão de saída e a pressão de entrada. O resultado é potência associada à pressão, em watts.',
      en: 'Flow × the difference between outlet and inlet pressure. The result is pressure-related power in watts.',
      es: 'Caudal × diferencia entre la presión de salida y la de entrada. El resultado es potencia asociada a la presión, en vatios.'
    },
    'recorded-pressure-power-input-ratio.v1': {
      'pt-BR': '100 × vazão × pressão diferencial ÷ potência elétrica ativa medida. É uma comparação, não a eficiência total do sistema.',
      en: '100 × flow × differential pressure ÷ measured active electrical power. This is a comparison, not total system efficiency.',
      es: '100 × caudal × presión diferencial ÷ potencia eléctrica activa medida. Es una comparación, no la eficiencia total del sistema.'
    }
  };
  return text[ruleId]?.[locale] ?? humanizePerformanceEvidence(ruleId, locale);
}

function describeMeanSemantics(locale: EngineeringLocale): string {
  return locale === 'pt-BR'
    ? 'Média aritmética dos resultados válidos; cada horário tem o mesmo peso.'
    : locale === 'es'
      ? 'Media aritmética de los resultados válidos; cada hora tiene el mismo peso.'
      : 'Arithmetic mean of valid results; each timestamp has equal weight.';
}

function describePerformanceCode(code: string, locale: EngineeringLocale): string {
  const labels: Record<string, Record<EngineeringLocale, string>> = {
    fewer_than_two_qualified_tuples: { 'pt-BR': 'Menos de duas leituras válidas coincidentes', en: 'Fewer than two valid matching readings', es: 'Menos de dos lecturas válidas coincidentes' },
    source_timestamp_missing: { 'pt-BR': 'Leitura sem horário de origem', en: 'Reading has no source time', es: 'Lectura sin hora de origen' },
    source_timestamp_evidence_missing: { 'pt-BR': 'Sem confirmação de sincronização do horário', en: 'No confirmation that source time is synchronized', es: 'Sin confirmación de sincronización de la hora' },
    source_timestamp_not_synchronized: { 'pt-BR': 'Horário de origem não sincronizado', en: 'Source time is not synchronized', es: 'Hora de origen no sincronizada' },
    source_timestamp_outside_requested_window: { 'pt-BR': 'Leitura fora do período selecionado', en: 'Reading is outside the selected time range', es: 'Lectura fuera del período seleccionado' },
    ambiguous_source_timestamp: { 'pt-BR': 'Mais de uma leitura no mesmo horário de origem', en: 'Multiple readings have the same source time', es: 'Varias lecturas tienen la misma hora de origen' },
    no_exact_source_timestamp_pair: { 'pt-BR': 'Sem leitura correspondente no mesmo horário', en: 'No matching reading at the same source time', es: 'Sin lectura correspondiente a la misma hora' },
    value_not_finite_numeric: { 'pt-BR': 'Valor ausente ou não numérico', en: 'Value is missing or not numeric', es: 'Valor ausente o no numérico' },
    unit_conversion_non_finite: { 'pt-BR': 'Não foi possível converter a unidade', en: 'The unit could not be converted', es: 'No se pudo convertir la unidad' },
    nonpositive_production_rate: { 'pt-BR': 'Taxa de produção precisa ser maior que zero', en: 'Production rate must be greater than zero', es: 'La tasa de producción debe ser mayor que cero' },
    negative_power: { 'pt-BR': 'Potência registrada abaixo de zero', en: 'Recorded power is below zero', es: 'Potencia registrada por debajo de cero' },
    flow_not_forward_positive: { 'pt-BR': 'Vazão precisa ser positiva no sentido de saída', en: 'Flow must be positive in the discharge direction', es: 'El caudal debe ser positivo en la dirección de salida' },
    negative_differential_pressure: { 'pt-BR': 'Pressão diferencial abaixo de zero', en: 'Differential pressure is below zero', es: 'Presión diferencial por debajo de cero' },
    nonpositive_input_power: { 'pt-BR': 'Potência elétrica precisa ser maior que zero', en: 'Electrical input power must be greater than zero', es: 'La potencia eléctrica debe ser mayor que cero' },
    non_finite_arithmetic: { 'pt-BR': 'O cálculo não produziu um número válido', en: 'The calculation did not produce a valid number', es: 'El cálculo no produjo un número válido' },
    non_finite_arithmetic_mean: { 'pt-BR': 'Não foi possível obter uma média válida', en: 'A valid mean could not be calculated', es: 'No se pudo calcular una media válida' },
    result_above_100_percent_observed_not_clipped: { 'pt-BR': 'Resultado acima de 100%; mantido sem ajuste e requer interpretação', en: 'Result above 100%; retained as measured and needs interpretation', es: 'Resultado superior al 100%; se conserva y requiere interpretación' },
    quality_missing: { 'pt-BR': 'Qualidade da leitura não informada', en: 'Reading quality was not provided', es: 'No se informó la calidad de la lectura' },
    quality_not_good: { 'pt-BR': 'Leitura com qualidade inadequada para o cálculo', en: 'Reading quality is not suitable for the calculation', es: 'La calidad de la lectura no es adecuada para el cálculo' }
  };
  const qualityLabels: Record<string, Record<EngineeringLocale, string>> = {
    Good: { 'pt-BR': 'Boa', en: 'Good', es: 'Buena' },
    Uncertain: { 'pt-BR': 'Incerta', en: 'Uncertain', es: 'Incierta' },
    Bad: { 'pt-BR': 'Ruim', en: 'Bad', es: 'Mala' },
    BadCommunication: { 'pt-BR': 'Falha de comunicação', en: 'Communication failure', es: 'Fallo de comunicación' },
    BadConfiguration: { 'pt-BR': 'Configuração inválida', en: 'Invalid configuration', es: 'Configuración no válida' },
    BadDevice: { 'pt-BR': 'Falha do equipamento', en: 'Device failure', es: 'Fallo del dispositivo' },
    Stale: { 'pt-BR': 'Desatualizada', en: 'Stale', es: 'Desactualizada' },
    Disabled: { 'pt-BR': 'Desabilitada', en: 'Disabled', es: 'Deshabilitada' },
    Unavailable: { 'pt-BR': 'Indisponível', en: 'Unavailable', es: 'No disponible' },
    Missing: { 'pt-BR': 'Ausente', en: 'Missing', es: 'Ausente' }
  };
  return labels[code]?.[locale] ?? qualityLabels[code]?.[locale] ?? humanizePerformanceEvidence(code, locale);
}

function humanizePerformanceEvidence(value: string, locale: EngineeringLocale): string {
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._-]+/g, ' ').replace(/\s+/g, ' ').trim();
  return locale === 'pt-BR'
    ? spaced.charAt(0).toLocaleUpperCase('pt-BR') + spaced.slice(1)
    : spaced.charAt(0).toLocaleUpperCase(locale === 'es' ? 'es-ES' : 'en-US') + spaced.slice(1);
}

export function parseWindow(fromLocal: string, toLocal: string, maximumDays: number): { window: WindowValue | null; error: 'missing' | 'invalid' | null } {
  if (!fromLocal || !toLocal) return { window: null, error: 'missing' };
  const from = new Date(fromLocal).getTime();
  const to = new Date(toLocal).getTime();
  if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from || to - from > maximumDays * 24 * 60 * 60 * 1000) return { window: null, error: 'invalid' };
  return { window: { fromUtc: new Date(from).toISOString(), toUtc: new Date(to).toISOString() }, error: null };
}

export function localDateTimeInput(value: Date) {
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

export function formatUtc(value: string, locale: string) {
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(date) : value;
}

export function ruleTitle(id: string, copy: PerformanceCopy) {
  if (id === 'specific-active-energy-at-recorded-production-rate.v1') return copy.specificEnergy;
  if (id === 'recorded-differential-pressure-power.v1') return copy.pressurePower;
  if (id === 'recorded-pressure-power-input-ratio.v1') return copy.pressurePowerRatio;
  return id;
}

export function errorText(code: string, copy: PerformanceCopy) {
  switch (code) {
    case 'INTELLIGENCE_PERFORMANCE_SIGNAL_SELECTION_INVALID':
    case 'INTELLIGENCE_PERFORMANCE_SIGNAL_IDENTITY_INVALID': return `${copy.queryFailed}: ${copy.inputMissing}`;
    case 'INTELLIGENCE_PERFORMANCE_SEMANTICS_UNSUPPORTED': return `${copy.queryFailed}: ${copy.semanticUnsupported}`;
    case 'INTELLIGENCE_PERFORMANCE_UNITS_UNSUPPORTED': return `${copy.queryFailed}: ${copy.unitUnsupported}`;
    case 'INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE': return `${copy.queryFailed}: ${copy.windowTooDense}`;
    case 'INVALID_RESPONSE': return copy.invalidResponse;
    default: return copy.networkError;
  }
}

class PerformanceQueryError extends Error {
  constructor(readonly code: string) { super(code); }
}

export type PerformanceCopy = ReturnType<typeof performanceCopy>;
export function performanceCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    open: 'Review recorded performance', hide: 'Hide performance', createRuntimeScreen: 'Create a Runtime analysis screen', blocked: 'Analysis requires the current entitlement, TrendUse, one active Equipment assignment and execution permit, and a clean Engineering revision.', description: 'Choose one existing catalog rule. The calculation remains server-owned, read-only and explicitly requested; the report is transient. It does not establish field efficiency or physical simultaneity.',
    loadingCatalog: 'Loading the versioned performance catalog…', catalogUnavailable: 'The performance catalog is unavailable.',
    rule: 'Performance rule', chooseRule: 'Choose an eligible rule', formula: 'Catalog formula', inputSelection: 'Canonical input signals',
    bounds: 'Rule bounds', timeRequirement: 'Timestamp requirement', from: 'From (local time)', to: 'To (local time)',
    utcHint: 'Selected local times are sent as UTC. Maximum window: 31 days. No Historian query runs until Analyze is selected.',
    windowRequired: 'Choose both ends of the time window.', windowInvalid: 'The end must be after the start and the window cannot exceed 31 days.',
    analyze: 'Analyze performance', loading: 'Analyzing…', queryFailed: 'Performance query failed', networkError: 'The performance report could not be retrieved.', windowTooDense: 'This time range has too many records for one analysis. Choose a shorter range.',
    invalidResponse: 'The response does not match the selected rule, assignment, profile, project revision or window.',
    inputMissing: 'Missing a unique signal for one or more catalog roles.', inputAmbiguous: 'Multiple profile signals match a required role; resolve the assignment first.',
    tagUnavailable: 'A canonical TAG reference is unavailable.', numericTagRequired: 'A selected TAG is not numeric.',
    unitUnsupported: 'The TAG unit is missing or not accepted by the catalog rule.', semanticUnsupported: 'The canonical typed semantics or qualifiers do not match the rule.',
    duplicateTag: 'The selection contains duplicate signal or TAG identities.', unknown: 'Not provided', none: 'none reported', queryWindow: 'Requested window:',
    status: 'Result', qualifiedTuples: 'Valid matching readings', minimum: 'Minimum', maximum: 'Maximum', arithmeticMean: 'Arithmetic mean',
    meanSemantics: 'How the mean is calculated', totalRawObservations: 'Readings checked', reasonCodes: 'What affected the result', inputs: 'Input signals',
    rawObservations: 'Raw observations', eligibleSynchronized: 'Eligible synchronized observations', paired: 'Paired tuples', qualified: 'Qualified tuples',
    qualityCounts: 'Reading quality', rejectionCounts: 'Readings not used', provenance: 'Data source and limits', dataset: 'Data source',
    timestampPairing: 'How readings are matched', acquisitionAlignment: 'Sensor timing', regimeEquivalence: 'Operating conditions',
    expectedCadence: 'Expected sampling interval', completeness: 'Data completeness', measurementInterpretation: 'What this result means',
    nonClaim: 'The records do not confirm that sensors measured at exactly the same moment or under equivalent operating conditions. Without an expected sampling interval, missing data cannot be assessed. This is an analysis of recorded values, not a diagnosis or field-validated efficiency claim.',
    specificEnergy: 'Specific active energy per recorded production', pressurePower: 'Recorded differential-pressure power', pressurePowerRatio: 'Recorded pressure-power / input ratio'
  };
  if (locale === 'es') return {
    open: 'Revisar rendimiento registrado', hide: 'Ocultar rendimiento', createRuntimeScreen: 'Crear pantalla de análisis Runtime', blocked: 'El análisis requiere licencia actual, TrendUse, una asignación Equipment activa, permiso de ejecución y una revisión Engineering sin cambios pendientes.', description: 'Elija una regla existente del catálogo. El cálculo sigue siendo del servidor, read-only y explícito; el informe es transitorio. No demuestra eficiencia de campo ni simultaneidad física.',
    loadingCatalog: 'Cargando el catálogo de rendimiento versionado…', catalogUnavailable: 'El catálogo de rendimiento no está disponible.',
    rule: 'Regla de rendimiento', chooseRule: 'Elija una regla elegible', formula: 'Fórmula del catálogo', inputSelection: 'Señales canónicas de entrada',
    bounds: 'Límites de la regla', timeRequirement: 'Requisito temporal', from: 'Desde (hora local)', to: 'Hasta (hora local)',
    utcHint: 'Las horas locales se envían como UTC. Intervalo máximo: 31 días. Historian no se consulta hasta pulsar Analizar.',
    windowRequired: 'Seleccione ambos extremos del intervalo.', windowInvalid: 'El final debe ser posterior al inicio y el intervalo no puede superar 31 días.',
    analyze: 'Analizar rendimiento', loading: 'Analizando…', queryFailed: 'Falló la consulta de rendimiento', networkError: 'No se pudo obtener el informe de rendimiento.', windowTooDense: 'El intervalo contiene demasiados registros para un análisis. Seleccione uno más corto.',
    invalidResponse: 'La respuesta no coincide con la regla, asignación, perfil, revisión o intervalo seleccionado.',
    inputMissing: 'Falta una señal única para uno o más roles del catálogo.', inputAmbiguous: 'Varias señales del perfil coinciden con un rol requerido; resuelva la asignación primero.',
    tagUnavailable: 'No está disponible una referencia TAG canónica.', numericTagRequired: 'Una TAG seleccionada no es numérica.',
    unitUnsupported: 'La unidad TAG falta o no es aceptada por la regla del catálogo.', semanticUnsupported: 'La semántica tipada o los calificadores canónicos no coinciden con la regla.',
    duplicateTag: 'La selección contiene identidades de señales o TAG duplicadas.', unknown: 'No informado', none: 'sin datos', queryWindow: 'Intervalo consultado:',
    status: 'Resultado', qualifiedTuples: 'Lecturas coincidentes válidas', minimum: 'Mínimo', maximum: 'Máximo', arithmeticMean: 'Media aritmética',
    meanSemantics: 'Cómo se calcula la media', totalRawObservations: 'Lecturas revisadas', reasonCodes: 'Qué influyó en el resultado', inputs: 'Señales de entrada',
    rawObservations: 'Observaciones brutas', eligibleSynchronized: 'Observaciones sincronizadas elegibles', paired: 'Tuplas emparejadas', qualified: 'Tuplas calificadas',
    qualityCounts: 'Calidad de las lecturas', rejectionCounts: 'Lecturas no utilizadas', provenance: 'Origen y límites de los datos', dataset: 'Origen de los datos',
    timestampPairing: 'Cómo se emparejan las lecturas', acquisitionAlignment: 'Horario de los sensores', regimeEquivalence: 'Condiciones de operación',
    expectedCadence: 'Frecuencia de muestreo esperada', completeness: 'Completitud de los datos', measurementInterpretation: 'Qué significa el resultado',
    nonClaim: 'Los registros no confirman que los sensores midieran exactamente al mismo tiempo ni bajo condiciones de operación equivalentes. Sin una frecuencia de muestreo esperada, no es posible evaluar si faltan datos. Es un análisis de valores registrados, no un diagnóstico ni una eficiencia validada en campo.',
    specificEnergy: 'Energía activa específica por producción registrada', pressurePower: 'Potencia por presión diferencial registrada', pressurePowerRatio: 'Relación potencia de presión / entrada registrada'
  };
  return {
    open: 'Analisar desempenho registrado', hide: 'Ocultar desempenho', createRuntimeScreen: 'Criar tela de análise no Runtime', blocked: 'A análise exige licença válida, TrendUse, uma associação Equipment ativa, permit de execução e revisão Engineering sem alterações pendentes.', description: 'Escolha uma regra existente do catálogo. O cálculo permanece no servidor, read-only e sob solicitação explícita; o relatório é temporário. Ele não comprova eficiência em campo nem simultaneidade física.',
    loadingCatalog: 'Carregando catálogo versionado de desempenho…', catalogUnavailable: 'Catálogo de desempenho indisponível.',
    rule: 'Regra de desempenho', chooseRule: 'Escolha uma regra elegível', formula: 'Fórmula do catálogo', inputSelection: 'Sinais canônicos de entrada',
    bounds: 'Limites da regra', timeRequirement: 'Requisito temporal', from: 'De (horário local)', to: 'Até (horário local)',
    utcHint: 'Os horários locais são enviados em UTC. Janela máxima: 31 dias. O Historian só é consultado após clicar em Analisar.',
    windowRequired: 'Informe os dois extremos da janela.', windowInvalid: 'O fim deve ser posterior ao início e a janela não pode exceder 31 dias.',
    analyze: 'Analisar desempenho', loading: 'Analisando…', queryFailed: 'Falha na consulta de desempenho', networkError: 'Não foi possível obter o relatório de desempenho.', windowTooDense: 'Há registros demais nesse período para uma análise. Escolha um intervalo menor.',
    invalidResponse: 'A resposta não corresponde à regra, associação, perfil, revisão do projeto ou janela selecionada.',
    inputMissing: 'Falta um sinal único para um ou mais papéis do catálogo.', inputAmbiguous: 'Vários sinais do perfil correspondem a um papel obrigatório; resolva a associação primeiro.',
    tagUnavailable: 'Referência canônica de TAG indisponível.', numericTagRequired: 'Uma TAG selecionada não é numérica.',
    unitUnsupported: 'A unidade da TAG está ausente ou não é aceita pela regra do catálogo.', semanticUnsupported: 'A semântica tipada ou os qualificadores canônicos não correspondem à regra.',
    duplicateTag: 'A seleção contém identidade de sinal ou TAG duplicada.', unknown: 'Não informado', none: 'nenhum informado', queryWindow: 'Janela consultada:',
    status: 'Resultado', qualifiedTuples: 'Leituras coincidentes válidas', minimum: 'Mínimo', maximum: 'Máximo', arithmeticMean: 'Média aritmética',
    meanSemantics: 'Como a média é calculada', totalRawObservations: 'Leituras verificadas', reasonCodes: 'O que influenciou o resultado', inputs: 'Sinais de entrada',
    rawObservations: 'Observações brutas', eligibleSynchronized: 'Observações sincronizadas elegíveis', paired: 'Tuplas pareadas', qualified: 'Tuplas qualificadas',
    qualityCounts: 'Qualidade das leituras', rejectionCounts: 'Leituras não utilizadas', provenance: 'Origem e limites dos dados', dataset: 'Origem dos dados',
    timestampPairing: 'Como as leituras são pareadas', acquisitionAlignment: 'Horário dos sensores', regimeEquivalence: 'Condições de operação',
    expectedCadence: 'Intervalo esperado entre leituras', completeness: 'Completude dos dados', measurementInterpretation: 'O que este resultado significa',
    nonClaim: 'Os registros não confirmam que os sensores mediram exatamente ao mesmo tempo nem em condições de operação equivalentes. Sem um intervalo esperado entre leituras, não é possível avaliar se faltam dados. Esta é uma análise dos valores registrados, não um diagnóstico nem uma eficiência validada em campo.',
    specificEnergy: 'Energia ativa específica por produção registrada', pressurePower: 'Potência da pressão diferencial registrada', pressurePowerRatio: 'Relação potência de pressão / entrada registrada'
  };
}
