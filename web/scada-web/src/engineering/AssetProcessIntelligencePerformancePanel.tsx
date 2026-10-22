import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import type {
  AssetProcessIntelligenceProfileEngineering,
  AssetProcessIntelligenceSignalEngineering,
  AssetProcessIntelligenceSignalSemanticBinding,
  TagEngineering
} from './types';
import type { EngineeringLocale } from './i18n';
import type { VersionedSemanticCatalog } from './semanticCatalogApi';
import { isNumericTagDataType } from './intelligenceDashboardGenerator';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const DEFAULT_MAX_WINDOW_DAYS = 31;

type PerformanceInputUnit = {
  role: string;
  unitCatalogVersions: number[];
  acceptedUnitCodes: string[];
};
type PerformanceRule = {
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
type PerformanceCatalog = {
  contractVersion: number;
  catalogVersion: number;
  ruleVersion: string;
  dataset: string;
  datasetContractVersion: number;
  maximumWindowDays: number;
  maximumRawRowsAcrossSelectedTags: number;
  rules: PerformanceRule[];
};
type WindowValue = { fromUtc: string; toUtc: string };
type PerformanceResponse = {
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
  canQuery
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
              return signal?.key ?? id;
            }).join(' · ')}</p>
            <p><strong>{copy.bounds}:</strong> {selected.rule.bounds}</p>
            <p><strong>{copy.timeRequirement}:</strong> {selected.rule.temporalRequirement}</p>
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

function isValidCatalog(value: PerformanceCatalog | null): value is PerformanceCatalog {
  return Boolean(value && value.contractVersion === 1 && value.catalogVersion === 1 &&
    Array.isArray(value.rules) && Number.isInteger(value.maximumWindowDays) && value.maximumWindowDays > 0 &&
    value.rules.every(rule => typeof rule.id === 'string' && typeof rule.formula === 'string' &&
      Array.isArray(rule.inputRoles) && Array.isArray(rule.inputUnits) && rule.inputRoles.length > 0));
}

function matchesResponse(
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

function PerformanceResult({ result, rule, locale, copy, profile }: {
  result: PerformanceResponse;
  rule: PerformanceRule;
  locale: EngineeringLocale;
  copy: PerformanceCopy;
  profile: AssetProcessIntelligenceProfileEngineering;
}) {
  const localeCode = locale === 'en' ? 'en-US' : locale === 'es' ? 'es-ES' : 'pt-BR';
  const number = (value: number | null | undefined) => value === null || value === undefined || !Number.isFinite(value)
    ? copy.unknown : new Intl.NumberFormat(localeCode, { maximumFractionDigits: 8 }).format(value);
  const signalById = new Map(profile.signals.map(signal => [signal.id.toLowerCase(), signal]));
  const signalDescription = new Map(result.signals.map(signal => [signal.signalId.toLowerCase(), signal]));
  return <div className="asset-process-intelligence__performance-result" data-testid="intelligence-performance-result">
    <p role="status"><strong>{copy.queryWindow}</strong> {formatUtc(result.window.fromUtc, localeCode)} – {formatUtc(result.window.toUtc, localeCode)}</p>
    <section>
      <h4>{ruleTitle(result.ruleId, copy)} · {result.summary.unit || copy.unknown}</h4>
      <p><strong>{copy.status}:</strong> {result.summary.status}</p>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.qualifiedTuples} value={number(result.summary.qualifiedTupleCount)}/>
        <Fact label={copy.minimum} value={number(result.summary.minimum)}/>
        <Fact label={copy.maximum} value={number(result.summary.maximum)}/>
        <Fact label={copy.arithmeticMean} value={number(result.summary.arithmeticMean)}/>
        <Fact label={copy.meanSemantics} value={result.summary.meanSemantics}/>
        <Fact label={copy.totalRawObservations} value={number(result.totalRawObservationCount)}/>
      </dl>
      <p><strong>{copy.formula}:</strong> {rule.formula}</p>
      <p><strong>{copy.reasonCodes}:</strong> {result.summary.reasonCodes.length ? result.summary.reasonCodes.join(' · ') : copy.none}</p>
    </section>
    <section>
      <h4>{copy.inputs}</h4>
      <div className="asset-process-intelligence__performance-inputs">
        {result.inputs.map(input => {
          const signal = signalById.get(input.signalId.toLowerCase());
          const description = signalDescription.get(input.signalId.toLowerCase());
          return <article key={input.signalId}>
            <h5>{signal?.key ?? description?.signalKey ?? input.signalId}</h5>
            <p>{description?.roleCode ?? copy.unknown} · {description?.canonicalUnit ?? copy.unknown}</p>
            <dl className="asset-process-intelligence__coverage-facts">
              <Fact label={copy.rawObservations} value={number(input.rawObservationCount)}/>
              <Fact label={copy.eligibleSynchronized} value={number(input.eligibleSynchronizedCount)}/>
              <Fact label={copy.paired} value={number(input.pairedCount)}/>
              <Fact label={copy.qualified} value={number(input.qualifiedCount)}/>
            </dl>
            <p><strong>{copy.qualityCounts}:</strong> {formatCounts(input.qualityCounts, copy)}</p>
            <p><strong>{copy.rejectionCounts}:</strong> {formatCounts(input.rejectionCounts, copy)}</p>
          </article>;
        })}
      </div>
    </section>
    <section>
      <h4>{copy.provenance}</h4>
      <dl className="asset-process-intelligence__coverage-facts">
        <Fact label={copy.dataset} value={`${result.provenance.dataset} · v${number(result.provenance.datasetContractVersion)}`}/>
        <Fact label={copy.timestampPairing} value={result.provenance.timestampPairing}/>
        <Fact label={copy.acquisitionAlignment} value={result.provenance.acquisitionAlignment}/>
        <Fact label={copy.regimeEquivalence} value={result.provenance.operatingRegimeEquivalence}/>
        <Fact label={copy.expectedCadence} value={result.provenance.expectedCadence}/>
        <Fact label={copy.completeness} value={result.provenance.completeness}/>
        <Fact label={copy.measurementInterpretation} value={result.provenance.measurementInterpretation}/>
      </dl>
      <p className="asset-process-intelligence__coverage-note">{copy.nonClaim}</p>
    </section>
  </div>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>;
}

function formatCounts(values: Record<string, number>, copy: PerformanceCopy) {
  return Object.entries(values ?? {}).sort(([a], [b]) => a.localeCompare(b))
    .map(([key, value]) => `${key}: ${new Intl.NumberFormat().format(value)}`).join(' · ') || copy.none;
}

function parseWindow(fromLocal: string, toLocal: string, maximumDays: number): { window: WindowValue | null; error: 'missing' | 'invalid' | null } {
  if (!fromLocal || !toLocal) return { window: null, error: 'missing' };
  const from = new Date(fromLocal).getTime();
  const to = new Date(toLocal).getTime();
  if (!Number.isFinite(from) || !Number.isFinite(to) || to <= from || to - from > maximumDays * 24 * 60 * 60 * 1000) return { window: null, error: 'invalid' };
  return { window: { fromUtc: new Date(from).toISOString(), toUtc: new Date(to).toISOString() }, error: null };
}

function localDateTimeInput(value: Date) {
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`;
}

function formatUtc(value: string, locale: string) {
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(date) : value;
}

function ruleTitle(id: string, copy: PerformanceCopy) {
  if (id === 'specific-active-energy-at-recorded-production-rate.v1') return copy.specificEnergy;
  if (id === 'recorded-differential-pressure-power.v1') return copy.pressurePower;
  if (id === 'recorded-pressure-power-input-ratio.v1') return copy.pressurePowerRatio;
  return id;
}

function errorText(code: string, copy: PerformanceCopy) {
  switch (code) {
    case 'INTELLIGENCE_PERFORMANCE_SIGNAL_SELECTION_INVALID':
    case 'INTELLIGENCE_PERFORMANCE_SIGNAL_IDENTITY_INVALID': return `${copy.queryFailed}: ${code}`;
    case 'INTELLIGENCE_PERFORMANCE_SEMANTICS_UNSUPPORTED': return `${copy.queryFailed}: ${copy.semanticUnsupported} (${code})`;
    case 'INTELLIGENCE_PERFORMANCE_UNITS_UNSUPPORTED': return `${copy.queryFailed}: ${copy.unitUnsupported} (${code})`;
    case 'INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE': return `${copy.queryFailed}: ${code}`;
    case 'INVALID_RESPONSE': return copy.invalidResponse;
    default: return `${copy.queryFailed}: ${code}`;
  }
}

class PerformanceQueryError extends Error {
  constructor(readonly code: string) { super(code); }
}

type PerformanceCopy = ReturnType<typeof performanceCopy>;
function performanceCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    open: 'Review recorded performance', hide: 'Hide performance', blocked: 'Analysis requires the current entitlement, TrendUse, one active Equipment assignment and execution permit, and a clean Engineering revision.', description: 'Choose one existing catalog rule. The calculation remains server-owned, read-only and explicitly requested; the report is transient. It does not establish field efficiency or physical simultaneity.',
    loadingCatalog: 'Loading the versioned performance catalog…', catalogUnavailable: 'The performance catalog is unavailable.',
    rule: 'Performance rule', chooseRule: 'Choose an eligible rule', formula: 'Catalog formula', inputSelection: 'Canonical input signals',
    bounds: 'Rule bounds', timeRequirement: 'Timestamp requirement', from: 'From (local time)', to: 'To (local time)',
    utcHint: 'Selected local times are sent as UTC. Maximum window: 31 days. No Historian query runs until Analyze is selected.',
    windowRequired: 'Choose both ends of the time window.', windowInvalid: 'The end must be after the start and the window cannot exceed 31 days.',
    analyze: 'Analyze performance', loading: 'Analyzing…', queryFailed: 'Performance query failed', networkError: 'The performance report could not be retrieved.',
    invalidResponse: 'The response does not match the selected rule, assignment, profile, project revision or window.',
    inputMissing: 'Missing a unique signal for one or more catalog roles.', inputAmbiguous: 'Multiple profile signals match a required role; resolve the assignment first.',
    tagUnavailable: 'A canonical TAG reference is unavailable.', numericTagRequired: 'A selected TAG is not numeric.',
    unitUnsupported: 'The TAG unit is missing or not accepted by the catalog rule.', semanticUnsupported: 'The canonical typed semantics or qualifiers do not match the rule.',
    duplicateTag: 'The selection contains duplicate signal or TAG identities.', unknown: 'unknown', none: 'none reported', queryWindow: 'Requested window:',
    status: 'Status', qualifiedTuples: 'Qualified tuples', minimum: 'Minimum', maximum: 'Maximum', arithmeticMean: 'Arithmetic mean',
    meanSemantics: 'Mean semantics', totalRawObservations: 'Total raw observations', reasonCodes: 'Reason codes', inputs: 'Input evidence',
    rawObservations: 'Raw observations', eligibleSynchronized: 'Eligible synchronized observations', paired: 'Paired tuples', qualified: 'Qualified tuples',
    qualityCounts: 'Quality counts', rejectionCounts: 'Rejection counts', provenance: 'Provenance and limits', dataset: 'Dataset',
    timestampPairing: 'Timestamp pairing', acquisitionAlignment: 'Acquisition alignment', regimeEquivalence: 'Operating-regime equivalence',
    expectedCadence: 'Expected cadence', completeness: 'Completeness', measurementInterpretation: 'Measurement interpretation',
    nonClaim: 'Unknown cadence/completeness and unverified physical acquisition/regime equivalence remain explicit. The selected rule is not a diagnosis, field-validated efficiency claim, or total-system measure unless its canonical contract says so.',
    specificEnergy: 'Specific active energy per recorded production', pressurePower: 'Recorded differential-pressure power', pressurePowerRatio: 'Recorded pressure-power / input ratio'
  };
  if (locale === 'es') return {
    open: 'Revisar rendimiento registrado', hide: 'Ocultar rendimiento', blocked: 'El análisis requiere licencia actual, TrendUse, una asignación Equipment activa, permiso de ejecución y una revisión Engineering sin cambios pendientes.', description: 'Elija una regla existente del catálogo. El cálculo sigue siendo del servidor, read-only y explícito; el informe es transitorio. No demuestra eficiencia de campo ni simultaneidad física.',
    loadingCatalog: 'Cargando el catálogo de rendimiento versionado…', catalogUnavailable: 'El catálogo de rendimiento no está disponible.',
    rule: 'Regla de rendimiento', chooseRule: 'Elija una regla elegible', formula: 'Fórmula del catálogo', inputSelection: 'Señales canónicas de entrada',
    bounds: 'Límites de la regla', timeRequirement: 'Requisito temporal', from: 'Desde (hora local)', to: 'Hasta (hora local)',
    utcHint: 'Las horas locales se envían como UTC. Intervalo máximo: 31 días. Historian no se consulta hasta pulsar Analizar.',
    windowRequired: 'Seleccione ambos extremos del intervalo.', windowInvalid: 'El final debe ser posterior al inicio y el intervalo no puede superar 31 días.',
    analyze: 'Analizar rendimiento', loading: 'Analizando…', queryFailed: 'Falló la consulta de rendimiento', networkError: 'No se pudo obtener el informe de rendimiento.',
    invalidResponse: 'La respuesta no coincide con la regla, asignación, perfil, revisión o intervalo seleccionado.',
    inputMissing: 'Falta una señal única para uno o más roles del catálogo.', inputAmbiguous: 'Varias señales del perfil coinciden con un rol requerido; resuelva la asignación primero.',
    tagUnavailable: 'No está disponible una referencia TAG canónica.', numericTagRequired: 'Una TAG seleccionada no es numérica.',
    unitUnsupported: 'La unidad TAG falta o no es aceptada por la regla del catálogo.', semanticUnsupported: 'La semántica tipada o los calificadores canónicos no coinciden con la regla.',
    duplicateTag: 'La selección contiene identidades de señales o TAG duplicadas.', unknown: 'desconocido', none: 'sin datos', queryWindow: 'Intervalo consultado:',
    status: 'Estado', qualifiedTuples: 'Tuplas calificadas', minimum: 'Mínimo', maximum: 'Máximo', arithmeticMean: 'Media aritmética',
    meanSemantics: 'Semántica de la media', totalRawObservations: 'Observaciones brutas totales', reasonCodes: 'Códigos de motivo', inputs: 'Evidencia de entradas',
    rawObservations: 'Observaciones brutas', eligibleSynchronized: 'Observaciones sincronizadas elegibles', paired: 'Tuplas emparejadas', qualified: 'Tuplas calificadas',
    qualityCounts: 'Conteo por calidad', rejectionCounts: 'Conteo de rechazos', provenance: 'Procedencia y límites', dataset: 'Dataset',
    timestampPairing: 'Emparejamiento temporal', acquisitionAlignment: 'Alineación de adquisición', regimeEquivalence: 'Equivalencia de régimen operativo',
    expectedCadence: 'Cadencia esperada', completeness: 'Completitud', measurementInterpretation: 'Interpretación de medición',
    nonClaim: 'La cadencia/completitud desconocida y la equivalencia física de adquisición/régimen no verificada siguen explícitas. La regla no es diagnóstico ni afirmación de eficiencia de campo validada.',
    specificEnergy: 'Energía activa específica por producción registrada', pressurePower: 'Potencia por presión diferencial registrada', pressurePowerRatio: 'Relación potencia de presión / entrada registrada'
  };
  return {
    open: 'Analisar desempenho registrado', hide: 'Ocultar desempenho', blocked: 'A análise exige licença válida, TrendUse, uma associação Equipment ativa, permit de execução e revisão Engineering sem alterações pendentes.', description: 'Escolha uma regra existente do catálogo. O cálculo permanece no servidor, read-only e sob solicitação explícita; o relatório é temporário. Ele não comprova eficiência em campo nem simultaneidade física.',
    loadingCatalog: 'Carregando catálogo versionado de desempenho…', catalogUnavailable: 'Catálogo de desempenho indisponível.',
    rule: 'Regra de desempenho', chooseRule: 'Escolha uma regra elegível', formula: 'Fórmula do catálogo', inputSelection: 'Sinais canônicos de entrada',
    bounds: 'Limites da regra', timeRequirement: 'Requisito temporal', from: 'De (horário local)', to: 'Até (horário local)',
    utcHint: 'Os horários locais são enviados em UTC. Janela máxima: 31 dias. O Historian só é consultado após clicar em Analisar.',
    windowRequired: 'Informe os dois extremos da janela.', windowInvalid: 'O fim deve ser posterior ao início e a janela não pode exceder 31 dias.',
    analyze: 'Analisar desempenho', loading: 'Analisando…', queryFailed: 'Falha na consulta de desempenho', networkError: 'Não foi possível obter o relatório de desempenho.',
    invalidResponse: 'A resposta não corresponde à regra, associação, perfil, revisão do projeto ou janela selecionada.',
    inputMissing: 'Falta um sinal único para um ou mais papéis do catálogo.', inputAmbiguous: 'Vários sinais do perfil correspondem a um papel obrigatório; resolva a associação primeiro.',
    tagUnavailable: 'Referência canônica de TAG indisponível.', numericTagRequired: 'Uma TAG selecionada não é numérica.',
    unitUnsupported: 'A unidade da TAG está ausente ou não é aceita pela regra do catálogo.', semanticUnsupported: 'A semântica tipada ou os qualificadores canônicos não correspondem à regra.',
    duplicateTag: 'A seleção contém identidade de sinal ou TAG duplicada.', unknown: 'desconhecido', none: 'nenhum informado', queryWindow: 'Janela consultada:',
    status: 'Estado', qualifiedTuples: 'Tuplas qualificadas', minimum: 'Mínimo', maximum: 'Máximo', arithmeticMean: 'Média aritmética',
    meanSemantics: 'Semântica da média', totalRawObservations: 'Observações brutas totais', reasonCodes: 'Códigos de motivo', inputs: 'Evidência das entradas',
    rawObservations: 'Observações brutas', eligibleSynchronized: 'Observações sincronizadas elegíveis', paired: 'Tuplas pareadas', qualified: 'Tuplas qualificadas',
    qualityCounts: 'Contagens por qualidade', rejectionCounts: 'Contagens de rejeição', provenance: 'Proveniência e limites', dataset: 'Dataset',
    timestampPairing: 'Pareamento de timestamps', acquisitionAlignment: 'Alinhamento da aquisição', regimeEquivalence: 'Equivalência do regime operacional',
    expectedCadence: 'Cadência esperada', completeness: 'Completude', measurementInterpretation: 'Interpretação da medição',
    nonClaim: 'Cadência/completude desconhecida e equivalência física de aquisição/regime não verificada permanecem explícitas. A regra não é diagnóstico nem alegação de eficiência de campo validada; não denomina potência de pressão como eficiência total da bomba.',
    specificEnergy: 'Energia ativa específica por produção registrada', pressurePower: 'Potência da pressão diferencial registrada', pressurePowerRatio: 'Relação potência de pressão / entrada registrada'
  };
}
