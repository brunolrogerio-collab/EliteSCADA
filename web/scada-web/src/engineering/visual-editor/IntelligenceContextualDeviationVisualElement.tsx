import { useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { useAuth } from '../../auth/AuthGate';
import type { EngineeringLocale } from '../i18n';
import { parseWindow, type WindowValue } from '../AssetProcessIntelligencePerformancePanel';
import { useIntelligencePerformanceRuntimeContext } from '../../visual-runtime';
import './IntelligenceContextualDeviationVisualElement.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const ROOT = '/api/engineering/asset-process-intelligence/analytics/contextual-deviation-vector/v2';
const RULE_ID = 'contextual-deviation-vector.v2';
const RULE_VERSION = '2.0.0';
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_VISIBLE_VECTORS = 30;

type Catalog = Readonly<{
  contractVersion: number;
  catalogVersion: number;
  ruleId: string;
  ruleVersion: string;
  maximumWindowDays: number;
  minimumTargetSignals: number;
  maximumTargetSignals: number;
  contextSignalCount: number;
}>;

type Signal = Readonly<{
  signalId: string;
  signalKey: string;
  tagId: string;
  tagPath: string;
  engineeringUnit: string | null;
  controlledUnitCode: string | null;
  canonicalUnit: string | null;
}>;

type ContextResult = Readonly<{
  signal: Signal;
  lowerInclusiveCanonical: number;
  upperExclusiveCanonical: number;
  canonicalUnit: string;
  completeReferenceVectorCount: number;
  referenceInRangeVectorCount: number;
  completeEvaluationVectorCount: number;
  evaluationInRangeVectorCount: number;
}>;

type TargetStatistics = Readonly<{
  signal: Signal;
  referenceMedian: number | null;
  referenceMad: number | null;
  completeReferenceVectorCount: number;
  jointContextReferenceVectorCount: number;
  completeEvaluationVectorCount: number;
  returnedJointContextEvaluationValueCount: number;
  status: string;
  scoreUnavailableReason: string | null;
}>;

type DeviationValue = Readonly<{
  targetSignalId: string;
  value: number;
  rawDifferenceFromReferenceMedian: number | null;
  robustScore: number | null;
  scoreUnavailableReason: string | null;
}>;

type EvaluationVector = Readonly<{
  sourceTimestampUtc: string;
  firstContextObservedTimestampUtc: string;
  firstContextValueCanonical: number;
  firstContextInRange: boolean;
  secondContextObservedTimestampUtc: string;
  secondContextValueCanonical: number;
  secondContextInRange: boolean;
  inJointContext: boolean;
  targets: readonly DeviationValue[];
}>;

type Provenance = Readonly<{
  dataset: string;
  datasetContractVersion: number;
  queryWindowTimestampBasis: string;
  vectorTimestampBasis: string;
  sourceTimestampEvidenceRequirement: string;
  contextQualification: string;
  regimeEquivalence: string;
  completeness: string;
  interpretation: string;
}>;

type Response = Readonly<{
  contractVersion: number;
  catalogVersion: number;
  ruleId: string;
  ruleVersion: string;
  projectKey: string;
  activeRevision: number;
  scopeKind: number;
  scopeId: string;
  assignmentId: string;
  equipmentId: string;
  profileId: string;
  targetSignals: readonly Signal[];
  contexts: readonly ContextResult[];
  referenceWindow: WindowValue;
  evaluationWindow: WindowValue;
  completeReferenceVectorCount: number;
  jointContextReferenceVectorCount: number;
  completeEvaluationVectorCount: number;
  jointContextEvaluationVectorCount: number;
  returnedEvaluationVectorCount: number;
  omittedEvaluationVectorCount: number;
  targetStatistics: readonly TargetStatistics[];
  evaluationVectors: readonly EvaluationVector[];
  totalRawObservationCount: number;
  provenance: Provenance;
}>;

type Props = Readonly<{
  scopeId: string;
  assignmentId: string;
  profileId: string;
  targetSignalIdsJson: string;
  firstContextSignalId: string;
  firstContextLowerInclusiveCanonical: number;
  firstContextUpperExclusiveCanonical: number;
  secondContextSignalId: string;
  secondContextLowerInclusiveCanonical: number;
  secondContextUpperExclusiveCanonical: number;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale: EngineeringLocale;
  title?: string;
}>;

export function IntelligenceContextualDeviationVisualElement({
  scopeId,
  assignmentId,
  profileId,
  targetSignalIdsJson,
  firstContextSignalId,
  firstContextLowerInclusiveCanonical,
  firstContextUpperExclusiveCanonical,
  secondContextSignalId,
  secondContextLowerInclusiveCanonical,
  secondContextUpperExclusiveCanonical,
  style,
  runtimeObjectId,
  locale,
  title
}: Props) {
  const runtime = useIntelligencePerformanceRuntimeContext();
  const { profile: authProfile } = useAuth();
  const copy = useMemo(() => visualCopy(locale), [locale]);
  const targetSignalIds = useMemo(() => parseSignalIds(targetSignalIdsJson), [targetSignalIdsJson]);
  const configValid = UUID_PATTERN.test(scopeId) && UUID_PATTERN.test(assignmentId) && UUID_PATTERN.test(profileId) &&
    targetSignalIds !== null && UUID_PATTERN.test(firstContextSignalId) && UUID_PATTERN.test(secondContextSignalId) &&
    !targetSignalIds?.some(id => id.toLowerCase() === firstContextSignalId.toLowerCase() || id.toLowerCase() === secondContextSignalId.toLowerCase()) &&
    firstContextSignalId.toLowerCase() !== secondContextSignalId.toLowerCase() &&
    validRange(firstContextLowerInclusiveCanonical, firstContextUpperExclusiveCanonical) &&
    validRange(secondContextLowerInclusiveCanonical, secondContextUpperExclusiveCanonical);
  const sessionKey = authProfile
    ? [authProfile.subjectId, authProfile.identityProvider ?? '', authProfile.expiresAtUtc ?? '', [...authProfile.roles].sort().join(',')].join('|')
    : 'anonymous';
  const contextKey = [runtime?.runtimeContextId ?? 'authoring', runtime?.projectKey ?? '', runtime?.activeRevision ?? '', runtime?.enabled ?? false, sessionKey].join('|');
  const [referenceFromLocal, setReferenceFromLocal] = useState('');
  const [referenceToLocal, setReferenceToLocal] = useState('');
  const [evaluationFromLocal, setEvaluationFromLocal] = useState('');
  const [evaluationToLocal, setEvaluationToLocal] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<Response | null>(null);
  const controller = useRef<AbortController | null>(null);
  const requestVersion = useRef(0);
  const referenceWindowResult = parseWindow(referenceFromLocal, referenceToLocal, 31);
  const evaluationWindowResult = parseWindow(evaluationFromLocal, evaluationToLocal, 31);
  const windowsChronological = Boolean(referenceWindowResult.window && evaluationWindowResult.window &&
    new Date(referenceWindowResult.window.toUtc).getTime() < new Date(evaluationWindowResult.window.fromUtc).getTime());
  const windowsValid = Boolean(referenceWindowResult.window && evaluationWindowResult.window && windowsChronological);

  const invalidate = () => {
    requestVersion.current += 1;
    controller.current?.abort();
    controller.current = null;
    setLoading(false);
    setError(null);
    setResult(null);
  };

  useEffect(() => {
    invalidate();
    return () => controller.current?.abort();
  }, [contextKey, scopeId, assignmentId, profileId, targetSignalIdsJson, firstContextSignalId,
    firstContextLowerInclusiveCanonical, firstContextUpperExclusiveCanonical, secondContextSignalId,
    secondContextLowerInclusiveCanonical, secondContextUpperExclusiveCanonical,
    referenceFromLocal, referenceToLocal, evaluationFromLocal, evaluationToLocal]);

  const analyze = async () => {
    if (!runtime?.enabled || !runtime.projectKey || runtime.activeRevision === null || !configValid ||
        !targetSignalIds || !referenceWindowResult.window || !evaluationWindowResult.window ||
        !windowsChronological || loading) return;

    requestVersion.current += 1;
    const version = requestVersion.current;
    controller.current?.abort();
    const requestController = new AbortController();
    controller.current = requestController;
    setResult(null);
    setError(null);
    setLoading(true);
    try {
      const catalogResponse = await fetch(`${API}${ROOT}/catalog`, {
        credentials: 'include', headers: { accept: 'application/json' }, signal: requestController.signal
      });
      if (!catalogResponse.ok) throw new DeviationVisualError(`HTTP_${catalogResponse.status}`);
      const catalog = await catalogResponse.json() as Catalog;
      if (!isValidCatalog(catalog)) throw new DeviationVisualError('INVALID_CATALOG');
      const request = {
        scopeKind: 0,
        scopeId,
        assignmentId,
        targetSignalIds,
        firstContextSignalId,
        firstContextLowerInclusiveCanonical,
        firstContextUpperExclusiveCanonical,
        secondContextSignalId,
        secondContextLowerInclusiveCanonical,
        secondContextUpperExclusiveCanonical,
        referenceWindow: referenceWindowResult.window,
        evaluationWindow: evaluationWindowResult.window
      } as const;
      const response = await fetch(`${API}${ROOT}/query`, {
        method: 'POST', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify(request), signal: requestController.signal
      });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok) {
        const errorPayload = isRecord(payload) ? payload : null;
        const code = errorPayload && typeof errorPayload.code === 'string' ? errorPayload.code : `HTTP_${response.status}`;
        throw new DeviationVisualError(code);
      }
      if (!matchesResponse(payload, {
        scopeId, assignmentId, profileId, projectKey: runtime.projectKey, activeRevision: runtime.activeRevision,
        targetSignalIds, firstContextSignalId, firstContextLowerInclusiveCanonical, firstContextUpperExclusiveCanonical,
        secondContextSignalId, secondContextLowerInclusiveCanonical, secondContextUpperExclusiveCanonical,
        referenceWindow: referenceWindowResult.window, evaluationWindow: evaluationWindowResult.window,
        catalogVersion: catalog.catalogVersion
      })) throw new DeviationVisualError('INVALID_RESPONSE');
      if (requestVersion.current === version && !requestController.signal.aborted) setResult(payload);
    } catch (reason) {
      if (!requestController.signal.aborted && requestVersion.current === version) {
        const code = reason instanceof DeviationVisualError ? reason.code
          : reason instanceof Error ? reason.message : 'NETWORK_ERROR';
        setError((copy.errors as Readonly<Record<string, string>>)[code] ?? `${copy.requestFailed}: ${code}`);
      }
    } finally {
      if (requestVersion.current === version) setLoading(false);
    }
  };

  const rootStyle: CSSProperties = {
    ...style, boxSizing: 'border-box', overflow: 'auto', padding: 12,
    border: '1px solid var(--app-border, #475569)', borderRadius: 8,
    background: 'var(--app-surface, #111827)', color: 'var(--app-text-primary, #E2E8F0)'
  };

  return <div
    className="visual-editor-object runtime-intelligence-contextual-deviation"
    style={rootStyle}
    data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId}
    data-testid="runtime-intelligence-contextual-deviation-v2"
    data-intelligence-runtime-enabled={runtime?.enabled === true}
    data-runtime-session-control="intelligence-analytics"
    title={title}
  >
    <header className="runtime-intelligence-contextual-deviation__header">
      <strong>{title || copy.title}</strong>
      <span>{copy.transient}</span>
    </header>
    {!configValid ? <p className="runtime-intelligence-contextual-deviation__notice" role="alert">{copy.invalidConfig}</p>
      : !runtime ? <p className="runtime-intelligence-contextual-deviation__notice" role="note">{copy.runtimeOnly}</p>
        : !runtime.enabled || !runtime.projectKey || runtime.activeRevision === null
          ? <p className="runtime-intelligence-contextual-deviation__notice" role="note">{copy.runtimeUnavailable}</p>
          : <>
            <p className="runtime-intelligence-contextual-deviation__notice">{copy.operatorAction}</p>
            <form className="runtime-intelligence-contextual-deviation__form" onSubmit={event => { event.preventDefault(); void analyze(); }}>
              <fieldset>
                <legend>{copy.reference}</legend>
                <label>{copy.referenceFrom}<input type="datetime-local" value={referenceFromLocal} disabled={loading} onChange={event => { invalidate(); setReferenceFromLocal(event.currentTarget.value); }}/></label>
                <label>{copy.referenceTo}<input type="datetime-local" value={referenceToLocal} disabled={loading} onChange={event => { invalidate(); setReferenceToLocal(event.currentTarget.value); }}/></label>
              </fieldset>
              <fieldset>
                <legend>{copy.evaluation}</legend>
                <label>{copy.evaluationFrom}<input type="datetime-local" value={evaluationFromLocal} disabled={loading} onChange={event => { invalidate(); setEvaluationFromLocal(event.currentTarget.value); }}/></label>
                <label>{copy.evaluationTo}<input type="datetime-local" value={evaluationToLocal} disabled={loading} onChange={event => { invalidate(); setEvaluationToLocal(event.currentTarget.value); }}/></label>
              </fieldset>
              <button type="submit" disabled={loading || !configValid || !windowsValid}>
                {loading ? copy.loading : copy.analyze}
              </button>
            </form>
            {!referenceWindowResult.window || !evaluationWindowResult.window
              ? <p className="runtime-intelligence-contextual-deviation__error" role="alert">{copy.windowInvalid}</p>
              : !windowsChronological
                ? <p className="runtime-intelligence-contextual-deviation__error" role="alert">{copy.windowsOrder}</p>
                : <p className="runtime-intelligence-contextual-deviation__hint">{copy.windowHint}</p>}
            {error ? <p className="runtime-intelligence-contextual-deviation__error" role="alert">{error}</p> : null}
            {result ? <DeviationResult result={result} locale={locale} copy={copy}/> : null}
          </>}
  </div>;
}

function DeviationResult({ result, locale, copy }: { result: Response; locale: EngineeringLocale; copy: ReturnType<typeof visualCopy> }) {
  const formatter = new Intl.NumberFormat(locale === 'pt-BR' ? 'pt-BR' : locale, { maximumFractionDigits: 5 });
  const targets = new Map(result.targetStatistics.map(statistic => [statistic.signal.signalId, statistic.signal.signalKey]));
  const visibleVectors = result.evaluationVectors.slice(-MAX_VISIBLE_VECTORS);
  return <section className="runtime-intelligence-contextual-deviation__result" data-testid="runtime-intelligence-contextual-deviation-result" aria-live="polite">
    <section>
      <h3>{copy.summary}</h3>
      <dl>
        <Fact label={copy.referenceVectors} value={formatCount(result.jointContextReferenceVectorCount, locale)}/>
        <Fact label={copy.evaluationVectors} value={formatCount(result.completeEvaluationVectorCount, locale)}/>
        <Fact label={copy.inJointContext} value={formatCount(result.jointContextEvaluationVectorCount, locale)}/>
        <Fact label={copy.rawRows} value={formatCount(result.totalRawObservationCount, locale)}/>
        <Fact label={copy.provenance} value={result.provenance.vectorTimestampBasis}/>
      </dl>
      <p>{copy.noCombinedDiagnosis}</p>
    </section>
    <section>
      <h3>{copy.perTarget}</h3>
      <div className="runtime-intelligence-contextual-deviation__targets">
        {result.targetStatistics.map(statistic => <article key={statistic.signal.signalId}>
          <h4>{statistic.signal.signalKey}</h4>
          <dl>
            <Fact label={copy.referenceMedian} value={formatOptional(statistic.referenceMedian, formatter)}/>
            <Fact label={copy.referenceMad} value={formatOptional(statistic.referenceMad, formatter)}/>
            <Fact label={copy.referenceCount} value={formatCount(statistic.jointContextReferenceVectorCount, locale)}/>
            <Fact label={copy.evaluationCount} value={formatCount(statistic.returnedJointContextEvaluationValueCount, locale)}/>
            <Fact label={copy.robustScore} value={statistic.status === 'available' ? copy.available : unavailableReason(statistic.scoreUnavailableReason, copy)}/>
          </dl>
        </article>)}
      </div>
    </section>
    <section>
      <h3>{copy.evaluationEvidence}</h3>
      <p>{copy.showingVectors(visibleVectors.length, result.returnedEvaluationVectorCount, result.omittedEvaluationVectorCount)}</p>
      <div className="runtime-intelligence-contextual-deviation__vectors">
        {visibleVectors.map(vector => <article key={vector.sourceTimestampUtc}>
          <h4>{formatTimestamp(vector.sourceTimestampUtc, locale)}</h4>
          <p>{copy.contextValues}: {formatNumber(vector.firstContextValueCanonical, formatter)} · {formatNumber(vector.secondContextValueCanonical, formatter)} · {vector.inJointContext ? copy.inJointContextLabel : copy.outsideJointContext}</p>
          <ul>{vector.targets.map(value => <li key={value.targetSignalId}>
            <strong>{targets.get(value.targetSignalId) ?? value.targetSignalId}</strong>: {formatNumber(value.value, formatter)}
            {' · '}{copy.difference}: {formatOptional(value.rawDifferenceFromReferenceMedian, formatter)}
            {' · '}{copy.robustScore}: {value.robustScore === null ? unavailableReason(value.scoreUnavailableReason, copy) : formatNumber(value.robustScore, formatter)}
          </li>)}</ul>
        </article>)}
        {visibleVectors.length === 0 ? <p>{copy.noEvaluationVectors}</p> : null}
      </div>
    </section>
  </section>;
}

function Fact({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>;
}

function parseSignalIds(value: string): string[] | null {
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed) || parsed.length < 2 || parsed.length > 8 ||
        parsed.some(item => typeof item !== 'string' || !UUID_PATTERN.test(item))) return null;
    const ids = parsed as string[];
    return new Set(ids.map(id => id.toLowerCase())).size === ids.length ? ids : null;
  } catch { return null; }
}

function validRange(lower: number, upper: number): boolean {
  return Number.isFinite(lower) && Number.isFinite(upper) && lower < upper;
}

function isValidCatalog(value: Catalog): boolean {
  return Boolean(value && value.contractVersion === 2 && value.catalogVersion === 2 && value.ruleId === RULE_ID &&
    value.ruleVersion === RULE_VERSION && Number.isInteger(value.maximumWindowDays) && value.maximumWindowDays > 0 &&
    value.maximumWindowDays <= 31 && value.minimumTargetSignals === 2 && value.maximumTargetSignals === 8 && value.contextSignalCount === 2);
}

function matchesResponse(value: unknown, expected: {
  scopeId: string; assignmentId: string; profileId: string; projectKey: string; activeRevision: number;
  targetSignalIds: readonly string[]; firstContextSignalId: string; firstContextLowerInclusiveCanonical: number;
  firstContextUpperExclusiveCanonical: number; secondContextSignalId: string; secondContextLowerInclusiveCanonical: number;
  secondContextUpperExclusiveCanonical: number; referenceWindow: WindowValue; evaluationWindow: WindowValue; catalogVersion: number;
}): value is Response {
  if (!isResponse(value)) return false;
  if (value.contractVersion !== 2 || value.catalogVersion !== expected.catalogVersion ||
      value.ruleId !== RULE_ID || value.ruleVersion !== RULE_VERSION || value.projectKey !== expected.projectKey ||
      value.activeRevision !== expected.activeRevision || !sameId(value.scopeId, expected.scopeId) ||
      value.scopeKind !== 0 ||
      !sameId(value.equipmentId, expected.scopeId) || !sameId(value.assignmentId, expected.assignmentId) ||
      !sameId(value.profileId, expected.profileId) || !sameWindow(value.referenceWindow, expected.referenceWindow) ||
      !sameWindow(value.evaluationWindow, expected.evaluationWindow) ||
      value.targetSignals?.length !== expected.targetSignalIds.length || value.contexts?.length !== 2 ||
      value.targetStatistics?.length !== expected.targetSignalIds.length || !Array.isArray(value.evaluationVectors) ||
      value.evaluationVectors.length > 500 || value.returnedEvaluationVectorCount !== value.evaluationVectors.length) return false;
  if (!value.targetSignals.every((signal, index) => sameId(signal.signalId, expected.targetSignalIds[index])) ||
      !value.targetStatistics.every((statistic, index) => sameId(statistic.signal.signalId, expected.targetSignalIds[index]))) return false;
  const first = value.contexts[0];
  const second = value.contexts[1];
  if (!sameId(first.signal.signalId, expected.firstContextSignalId) ||
      first.lowerInclusiveCanonical !== expected.firstContextLowerInclusiveCanonical ||
      first.upperExclusiveCanonical !== expected.firstContextUpperExclusiveCanonical ||
      !sameId(second.signal.signalId, expected.secondContextSignalId) ||
      second.lowerInclusiveCanonical !== expected.secondContextLowerInclusiveCanonical ||
      second.upperExclusiveCanonical !== expected.secondContextUpperExclusiveCanonical) return false;
  const vectors = value.evaluationVectors as readonly EvaluationVector[];
  return vectors.every(vector => vector.targets.length === expected.targetSignalIds.length &&
    vector.targets.every((target: DeviationValue, index: number) => sameId(target.targetSignalId, expected.targetSignalIds[index])));
}

function sameId(left: string, right: string): boolean { return typeof left === 'string' && left.toLowerCase() === right.toLowerCase(); }
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
function isResponse(value: unknown): value is Response {
  if (!isRecord(value)) return false;
  return Number.isInteger(value.contractVersion) && Number.isInteger(value.catalogVersion) &&
    typeof value.ruleId === 'string' && typeof value.ruleVersion === 'string' && typeof value.projectKey === 'string' &&
    Number.isInteger(value.activeRevision) && value.scopeKind === 0 && typeof value.scopeId === 'string' && typeof value.assignmentId === 'string' &&
    typeof value.equipmentId === 'string' && typeof value.profileId === 'string' && isWindow(value.referenceWindow) &&
    isWindow(value.evaluationWindow) && isNonNegativeInteger(value.completeReferenceVectorCount) &&
    isNonNegativeInteger(value.jointContextReferenceVectorCount) && isNonNegativeInteger(value.completeEvaluationVectorCount) &&
    isNonNegativeInteger(value.jointContextEvaluationVectorCount) && isNonNegativeInteger(value.returnedEvaluationVectorCount) &&
    isNonNegativeInteger(value.omittedEvaluationVectorCount) && isNonNegativeInteger(value.totalRawObservationCount) &&
    Array.isArray(value.targetSignals) && value.targetSignals.length >= 2 && value.targetSignals.length <= 8 &&
    value.targetSignals.every(isSignal) && Array.isArray(value.contexts) && value.contexts.length === 2 &&
    value.contexts.every(isContextResult) && Array.isArray(value.targetStatistics) && value.targetStatistics.every(isTargetStatistics) &&
    Array.isArray(value.evaluationVectors) && value.evaluationVectors.every(isEvaluationVector) &&
    isProvenance(value.provenance);
}
function isProvenance(value: unknown): value is Provenance {
  return isRecord(value) && typeof value.dataset === 'string' && isNonNegativeInteger(value.datasetContractVersion) &&
    typeof value.queryWindowTimestampBasis === 'string' && typeof value.vectorTimestampBasis === 'string' &&
    typeof value.sourceTimestampEvidenceRequirement === 'string' && typeof value.contextQualification === 'string' &&
    typeof value.regimeEquivalence === 'string' && typeof value.completeness === 'string' && typeof value.interpretation === 'string';
}
function isSignal(value: unknown): value is Signal {
  if (!isRecord(value)) return false;
  return typeof value.signalId === 'string' && typeof value.signalKey === 'string' && typeof value.tagId === 'string' &&
    typeof value.tagPath === 'string' && (typeof value.engineeringUnit === 'string' || value.engineeringUnit === null) &&
    (typeof value.controlledUnitCode === 'string' || value.controlledUnitCode === null) &&
    (typeof value.canonicalUnit === 'string' || value.canonicalUnit === null);
}
function isWindow(value: unknown): value is WindowValue {
  return isRecord(value) && typeof value.fromUtc === 'string' && typeof value.toUtc === 'string';
}
function isContextResult(value: unknown): value is ContextResult {
  return isRecord(value) && isSignal(value.signal) && isFiniteNumber(value.lowerInclusiveCanonical) &&
    isFiniteNumber(value.upperExclusiveCanonical) && typeof value.canonicalUnit === 'string' &&
    isNonNegativeInteger(value.completeReferenceVectorCount) && isNonNegativeInteger(value.referenceInRangeVectorCount) &&
    isNonNegativeInteger(value.completeEvaluationVectorCount) && isNonNegativeInteger(value.evaluationInRangeVectorCount);
}
function isTargetStatistics(value: unknown): value is TargetStatistics {
  return isRecord(value) && isSignal(value.signal) && isNullableFiniteNumber(value.referenceMedian) &&
    isNullableFiniteNumber(value.referenceMad) && isNonNegativeInteger(value.completeReferenceVectorCount) &&
    isNonNegativeInteger(value.jointContextReferenceVectorCount) && isNonNegativeInteger(value.completeEvaluationVectorCount) &&
    isNonNegativeInteger(value.returnedJointContextEvaluationValueCount) && typeof value.status === 'string' &&
    (typeof value.scoreUnavailableReason === 'string' || value.scoreUnavailableReason === null);
}
function isDeviationValue(value: unknown): value is DeviationValue {
  return isRecord(value) && typeof value.targetSignalId === 'string' && isFiniteNumber(value.value) &&
    isNullableFiniteNumber(value.rawDifferenceFromReferenceMedian) && isNullableFiniteNumber(value.robustScore) &&
    (typeof value.scoreUnavailableReason === 'string' || value.scoreUnavailableReason === null);
}
function isEvaluationVector(value: unknown): value is EvaluationVector {
  return isRecord(value) && typeof value.sourceTimestampUtc === 'string' &&
    typeof value.firstContextObservedTimestampUtc === 'string' && isFiniteNumber(value.firstContextValueCanonical) &&
    typeof value.firstContextInRange === 'boolean' && typeof value.secondContextObservedTimestampUtc === 'string' &&
    isFiniteNumber(value.secondContextValueCanonical) && typeof value.secondContextInRange === 'boolean' &&
    typeof value.inJointContext === 'boolean' && Array.isArray(value.targets) && value.targets.every(isDeviationValue);
}
function isFiniteNumber(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function isNullableFiniteNumber(value: unknown): value is number | null { return value === null || isFiniteNumber(value); }
function isNonNegativeInteger(value: unknown): value is number { return Number.isInteger(value) && typeof value === 'number' && value >= 0; }
function sameWindow(left: WindowValue, right: WindowValue): boolean {
  return Date.parse(left?.fromUtc ?? '') === Date.parse(right.fromUtc) && Date.parse(left?.toUtc ?? '') === Date.parse(right.toUtc);
}
function formatCount(value: number, locale: EngineeringLocale): string {
  return Number.isInteger(value) && value >= 0 ? new Intl.NumberFormat(locale === 'pt-BR' ? 'pt-BR' : locale).format(value) : '—';
}
function formatTimestamp(value: string, locale: EngineeringLocale): string {
  const parsed = new Date(value);
  return Number.isFinite(parsed.getTime()) ? new Intl.DateTimeFormat(locale === 'pt-BR' ? 'pt-BR' : locale, { dateStyle: 'short', timeStyle: 'medium', timeZone: 'UTC' }).format(parsed) : value;
}
function formatNumber(value: number, formatter: Intl.NumberFormat): string { return Number.isFinite(value) ? formatter.format(value) : '—'; }
function formatOptional(value: number | null, formatter: Intl.NumberFormat): string { return value === null ? '—' : formatNumber(value, formatter); }
function unavailableReason(reason: string | null, copy: ReturnType<typeof visualCopy>): string {
  return reason ? (copy.reasons as Readonly<Record<string, string>>)[reason] ?? reason : copy.unavailable;
}

function visualCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Contextual deviation', transient: 'V2 · temporary read-only evidence', runtimeOnly: 'This visual is interactive only in live Runtime. The Engineering editor never queries Historian.',
    runtimeUnavailable: 'Queries are unavailable during playback or unless Runtime View and TrendUse are effective.', invalidConfig: 'The configured scope, assignment, profile, signals or canonical ranges are invalid. This visual fails closed.',
    operatorAction: 'Historian is queried only after selecting Analyze. Reference and evaluation windows must be separate and chronological; each request rechecks server authorization.',
    reference: 'Reference window', evaluation: 'Evaluation window', referenceFrom: 'Reference from (local time)', referenceTo: 'Reference to (local time)', evaluationFrom: 'Evaluation from (local time)', evaluationTo: 'Evaluation to (local time)', analyze: 'Analyze', loading: 'Querying…',
    windowInvalid: 'Both windows must be valid and no longer than 31 days each.', windowsOrder: 'The reference window must end before the evaluation window begins.',
    windowHint: 'Local times are sent as UTC. Windows must not overlap. Results and samples stay only in this Runtime view.', requestFailed: 'Request failed',
    summary: 'Evidence summary', referenceVectors: 'Joint-context reference vectors', evaluationVectors: 'Complete evaluation vectors', inJointContext: 'Evaluation vectors in declared context', rawRows: 'Raw observations read', provenance: 'Timestamp basis',
    noCombinedDiagnosis: 'Each target is described independently against the joint-context reference cohort. No combined anomaly, diagnosis, fault or remaining-life estimate is produced.',
    perTarget: 'Per-target reference', referenceMedian: 'Reference median', referenceMad: 'Reference MAD', referenceCount: 'Joint reference count', evaluationCount: 'Returned evaluation values', robustScore: 'Robust score', available: 'Available per sample below', unavailable: 'Unavailable',
    evaluationEvidence: 'Evaluation evidence', showingVectors: (shown: number, returned: number, omitted: number) => `Showing ${shown} latest of ${returned} returned vectors; ${omitted} additional vectors were omitted by the server limit.`,
    contextValues: 'Context values (canonical)', inJointContextLabel: 'inside both declared ranges', outsideJointContext: 'outside joint context', difference: 'difference from reference median', noEvaluationVectors: 'No complete evaluation vectors were returned.',
    errors: {
      INVALID_CATALOG: 'The active contextual-deviation catalog is unavailable or incompatible.', INVALID_RESPONSE: 'The server response does not match this Screen configuration or active Runtime revision.',
      INTELLIGENCE_ENTITLEMENT_REQUIRED: 'The Intelligence license is not effective for this Runtime.', INTELLIGENCE_EXECUTION_PERMIT_REQUIRED: 'An active Intelligence execution permit is required.',
      INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE: 'The selected Intelligence scope is not active in this Runtime revision.',
      INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_V2_CONTEXT_UNIT_UNSUPPORTED: 'Both context signals need compatible controlled units.',
      INTELLIGENCE_CONTEXTUAL_DEVIATION_VECTOR_V2_CONTEXT_RANGE_INVALID: 'A declared canonical context range is invalid.'
    },
    reasons: { fewer_than_30_complete_joint_context_reference_vectors: 'Fewer than 30 reference vectors', reference_mad_zero: 'Reference MAD is zero', no_complete_joint_context_reference_vectors: 'No complete joint-context reference vectors', outside_joint_declared_context: 'Outside declared context' }
  };
  if (locale === 'es') return {
    title: 'Desviación contextual', transient: 'V2 · evidencia temporal de solo lectura', runtimeOnly: 'Este visual solo es interactivo en Runtime en vivo. Engineering nunca consulta Historian.',
    runtimeUnavailable: 'No se puede consultar durante la reproducción ni sin Runtime View y TrendUse efectivos.', invalidConfig: 'El alcance, asignación, perfil, señales o rangos canónicos no son válidos. El visual se cierra de forma segura.',
    operatorAction: 'Historian solo se consulta al seleccionar Analizar. Las ventanas deben ser cronológicas y separadas; el servidor revalida cada solicitud.',
    reference: 'Ventana de referencia', evaluation: 'Ventana de evaluación', referenceFrom: 'Desde referencia (hora local)', referenceTo: 'Hasta referencia (hora local)', evaluationFrom: 'Desde evaluación (hora local)', evaluationTo: 'Hasta evaluación (hora local)', analyze: 'Analizar', loading: 'Consultando…',
    windowInvalid: 'Cada ventana debe ser válida y tener como máximo 31 días.', windowsOrder: 'La ventana de referencia debe terminar antes de que comience la de evaluación.',
    windowHint: 'Las horas locales se envían como UTC. Las ventanas no se superponen. Los resultados quedan solo en esta vista Runtime.', requestFailed: 'Falló la solicitud',
    summary: 'Resumen de evidencia', referenceVectors: 'Vectores de referencia en contexto conjunto', evaluationVectors: 'Vectores completos de evaluación', inJointContext: 'Vectores de evaluación en el contexto declarado', rawRows: 'Observaciones leídas', provenance: 'Base temporal',
    noCombinedDiagnosis: 'Cada objetivo se describe por separado frente a la cohorte de referencia. No se genera anomalía combinada, diagnóstico, falla ni vida restante.',
    perTarget: 'Referencia por objetivo', referenceMedian: 'Mediana de referencia', referenceMad: 'MAD de referencia', referenceCount: 'Conteo de referencia conjunta', evaluationCount: 'Valores de evaluación devueltos', robustScore: 'Puntuación robusta', available: 'Disponible por muestra abajo', unavailable: 'No disponible',
    evaluationEvidence: 'Evidencia de evaluación', showingVectors: (shown: number, returned: number, omitted: number) => `Se muestran ${shown} vectores más recientes de ${returned}; el servidor omitió ${omitted} adicionales por límite.`,
    contextValues: 'Valores de contexto (canónicos)', inJointContextLabel: 'dentro de ambos rangos', outsideJointContext: 'fuera del contexto conjunto', difference: 'diferencia frente a mediana', noEvaluationVectors: 'No se devolvieron vectores completos.',
    errors: { INVALID_CATALOG: 'El catálogo activo no está disponible o es incompatible.', INVALID_RESPONSE: 'La respuesta no coincide con esta pantalla o revisión Runtime.', INTELLIGENCE_ENTITLEMENT_REQUIRED: 'La licencia Intelligence no está activa para este Runtime.', INTELLIGENCE_EXECUTION_PERMIT_REQUIRED: 'Se requiere un permiso Intelligence activo.', INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE: 'El alcance seleccionado no está activo en esta revisión Runtime.' },
    reasons: { fewer_than_30_complete_joint_context_reference_vectors: 'Menos de 30 vectores de referencia', reference_mad_zero: 'MAD de referencia igual a cero', no_complete_joint_context_reference_vectors: 'Sin vectores completos de referencia conjunta', outside_joint_declared_context: 'Fuera del contexto declarado' }
  };
  return {
    title: 'Desvio contextual', transient: 'V2 · evidência temporária somente leitura', runtimeOnly: 'Este visual só é interativo no Runtime ao vivo. O Engineering nunca consulta o Historian.',
    runtimeUnavailable: 'Consultas não ficam disponíveis durante reprodução nem sem Runtime View e TrendUse efetivos.', invalidConfig: 'Escopo, associação, perfil, sinais ou faixas canônicas inválidas. O visual falha fechado.',
    operatorAction: 'O Historian só é consultado ao selecionar Analisar. As janelas devem ser cronológicas e separadas; cada solicitação é revalidada pelo servidor.',
    reference: 'Janela de referência', evaluation: 'Janela de avaliação', referenceFrom: 'Início da referência (horário local)', referenceTo: 'Fim da referência (horário local)', evaluationFrom: 'Início da avaliação (horário local)', evaluationTo: 'Fim da avaliação (horário local)', analyze: 'Analisar', loading: 'Consultando…',
    windowInvalid: 'Cada janela precisa ser válida e ter no máximo 31 dias.', windowsOrder: 'A janela de referência deve terminar antes do início da janela de avaliação.',
    windowHint: 'Horários locais são enviados em UTC. As janelas não podem se sobrepor. Resultados e amostras ficam apenas nesta visualização Runtime.', requestFailed: 'Falha na solicitação',
    summary: 'Resumo das evidências', referenceVectors: 'Vetores de referência no contexto conjunto', evaluationVectors: 'Vetores completos de avaliação', inJointContext: 'Vetores de avaliação no contexto declarado', rawRows: 'Observações lidas', provenance: 'Base temporal',
    noCombinedDiagnosis: 'Cada alvo é descrito individualmente contra a coorte de referência. Não é produzida anomalia combinada, diagnóstico, falha ou estimativa de vida restante.',
    perTarget: 'Referência por alvo', referenceMedian: 'Mediana de referência', referenceMad: 'MAD de referência', referenceCount: 'Contagem na referência conjunta', evaluationCount: 'Valores de avaliação retornados', robustScore: 'Escore robusto', available: 'Disponível por amostra abaixo', unavailable: 'Indisponível',
    evaluationEvidence: 'Evidências da avaliação', showingVectors: (shown: number, returned: number, omitted: number) => `Exibindo ${shown} vetores mais recentes dos ${returned} retornados; o servidor omitiu outros ${omitted} pelo limite.`,
    contextValues: 'Valores de contexto (canônicos)', inJointContextLabel: 'dentro das duas faixas', outsideJointContext: 'fora do contexto conjunto', difference: 'diferença da mediana de referência', noEvaluationVectors: 'Nenhum vetor completo de avaliação foi retornado.',
    errors: { INVALID_CATALOG: 'O catálogo ativo de desvio contextual está indisponível ou incompatível.', INVALID_RESPONSE: 'A resposta do servidor não corresponde à configuração desta tela ou à revisão Runtime ativa.', INTELLIGENCE_ENTITLEMENT_REQUIRED: 'A licença de Intelligence não está efetiva neste Runtime.', INTELLIGENCE_EXECUTION_PERMIT_REQUIRED: 'É necessária uma permissão ativa de execução de Intelligence.', INTELLIGENCE_RUNTIME_NOT_EFFECTIVE_ACTIVE: 'O escopo Intelligence selecionado não está ativo nesta revisão Runtime.' },
    reasons: { fewer_than_30_complete_joint_context_reference_vectors: 'Menos de 30 vetores de referência', reference_mad_zero: 'MAD de referência igual a zero', no_complete_joint_context_reference_vectors: 'Sem vetores completos de referência no contexto conjunto', outside_joint_declared_context: 'Fora do contexto declarado' }
  };
}

class DeviationVisualError extends Error {
  constructor(readonly code: string) { super(code); }
}
