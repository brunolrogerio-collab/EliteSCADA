import { useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties } from 'react';
import { useAuth } from '../../auth/AuthGate';
import type { EngineeringLocale } from '../i18n';
import {
  PerformanceResult,
  errorText,
  isValidCatalog,
  localDateTimeInput,
  matchesResponse,
  parseWindow,
  performanceCopy,
  ruleTitle,
  type PerformanceCatalog,
  type PerformanceResponse,
  type PerformanceRule
} from '../AssetProcessIntelligencePerformancePanel';
import { useIntelligencePerformanceRuntimeContext } from '../../visual-runtime';
import './IntelligencePerformanceVisualElement.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const PERFORMANCE_API = '/api/engineering/asset-process-intelligence/analytics/performance/v1';
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type Props = Readonly<{
  scopeId: string;
  assignmentId: string;
  profileId: string;
  ruleId: string;
  ruleVersion: string;
  signalIdsJson: string;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale: EngineeringLocale;
  title?: string;
}>;

export function IntelligencePerformanceVisualElement({
  scopeId,
  assignmentId,
  profileId,
  ruleId,
  ruleVersion,
  signalIdsJson,
  style,
  runtimeObjectId,
  locale,
  title
}: Props) {
  const runtime = useIntelligencePerformanceRuntimeContext();
  const { profile: authProfile } = useAuth();
  const copy = useMemo(() => performanceCopy(locale), [locale]);
  const signals = useMemo(() => parseSignalIds(signalIdsJson), [signalIdsJson]);
  const configValid = UUID_PATTERN.test(scopeId) && UUID_PATTERN.test(assignmentId) && UUID_PATTERN.test(profileId) &&
    ruleId.trim() === ruleId && ruleId.length > 0 && ruleId.length <= 128 &&
    ruleVersion.trim() === ruleVersion && ruleVersion.length > 0 && ruleVersion.length <= 32 &&
    signals !== null;
  const sessionKey = authProfile
    ? [authProfile.subjectId, authProfile.identityProvider ?? '', authProfile.expiresAtUtc ?? '', [...authProfile.roles].sort().join(',')].join('|')
    : 'anonymous';
  const contextKey = [runtime?.runtimeContextId ?? 'authoring', runtime?.projectKey ?? '', runtime?.activeRevision ?? '', runtime?.enabled ?? false, sessionKey].join('|');
  const [fromLocal, setFromLocal] = useState(() => localDateTimeInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [toLocal, setToLocal] = useState(() => localDateTimeInput(new Date()));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<Readonly<{ response: PerformanceResponse; rule: PerformanceRule }> | null>(null);
  const controller = useRef<AbortController | null>(null);
  const requestVersion = useRef(0);
  const windowResult = parseWindow(fromLocal, toLocal, 31);

  useEffect(() => {
    requestVersion.current += 1;
    controller.current?.abort();
    controller.current = null;
    setLoading(false);
    setError(null);
    setResult(null);
    return () => controller.current?.abort();
  }, [contextKey, scopeId, assignmentId, profileId, ruleId, ruleVersion, signalIdsJson]);

  const analyze = async () => {
    if (!runtime?.enabled || !runtime.projectKey || runtime.activeRevision === null ||
        !configValid || !signals || !windowResult.window || loading) return;

    requestVersion.current += 1;
    const version = requestVersion.current;
    controller.current?.abort();
    const requestController = new AbortController();
    controller.current = requestController;
    setResult(null);
    setError(null);
    setLoading(true);

    try {
      // Catalog and Historian data are fetched only after this explicit operator action.
      const catalogResponse = await fetch(`${API}${PERFORMANCE_API}/catalog`, {
        credentials: 'include', headers: { accept: 'application/json' }, signal: requestController.signal
      });
      if (!catalogResponse.ok) throw new PerformanceVisualError(`HTTP_${catalogResponse.status}`);
      const catalog = await catalogResponse.json() as PerformanceCatalog;
      if (!isValidCatalog(catalog)) throw new PerformanceVisualError('INVALID_CATALOG');
      const rule = catalog.rules.find(candidate => candidate.id === ruleId && candidate.version === ruleVersion);
      if (!rule) throw new PerformanceVisualError('CONFIGURED_RULE_NOT_IN_CATALOG');
      const boundedWindow = parseWindow(fromLocal, toLocal, catalog.maximumWindowDays);
      if (!boundedWindow.window) throw new PerformanceVisualError(boundedWindow.error === 'missing' ? 'WINDOW_REQUIRED' : 'WINDOW_INVALID');

      const response = await fetch(`${API}${PERFORMANCE_API}/query`, {
        method: 'POST', credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({
          scopeKind: 0,
          scopeId,
          assignmentId,
          profileId,
          ruleId,
          signalIds: signals,
          window: boundedWindow.window
        }),
        signal: requestController.signal
      });
      const payload = await response.json().catch(() => null) as PerformanceResponse | { code?: unknown } | null;
      if (!response.ok) {
        const code = payload && 'code' in payload && typeof payload.code === 'string'
          ? payload.code
          : `HTTP_${response.status}`;
        throw new PerformanceVisualError(code);
      }
      if (!matchesResponse(
        payload,
        scopeId,
        assignmentId,
        profileId,
        runtime.projectKey,
        runtime.activeRevision,
        rule,
        catalog.catalogVersion,
        signals,
        boundedWindow.window
      )) throw new PerformanceVisualError('INVALID_RESPONSE');

      if (requestVersion.current === version && !requestController.signal.aborted) setResult({ response: payload, rule });
    } catch (reason) {
      if (!requestController.signal.aborted && requestVersion.current === version) {
        const code = reason instanceof PerformanceVisualError ? reason.code
          : reason instanceof Error ? reason.message : 'NETWORK_ERROR';
        setError(runtimeErrorText(code, copy, locale));
      }
    } finally {
      if (requestVersion.current === version) setLoading(false);
    }
  };

  const runtimeCopy = runtimeElementCopy(locale);
  const rootStyle: CSSProperties = {
    ...style,
    boxSizing: 'border-box',
    overflow: 'auto',
    padding: 12,
    border: '1px solid var(--app-border, #475569)',
    borderRadius: 8,
    background: 'var(--app-surface, #111827)',
    color: 'var(--app-text-primary, #E2E8F0)'
  };

  return <div
    className="visual-editor-object runtime-intelligence-performance"
    style={rootStyle}
    data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId}
    data-testid="runtime-intelligence-performance"
    data-intelligence-runtime-enabled={runtime?.enabled === true}
    data-runtime-session-control="intelligence-analytics"
    title={title}
  >
    <header className="runtime-intelligence-performance__header">
      <strong>{runtimeCopy.title}</strong>
      <span>{ruleTitle(ruleId, copy)} · {runtimeCopy.transient}</span>
    </header>
    {!configValid ? <p className="runtime-intelligence-performance__notice" role="alert">{runtimeCopy.invalidConfiguration}</p>
      : !runtime ? <p className="runtime-intelligence-performance__notice" role="note">{runtimeCopy.runtimeOnly}</p>
        : !runtime.enabled ? <p className="runtime-intelligence-performance__notice" role="note">{runtimeCopy.liveRuntimeRequired}</p>
          : <>
            <p className="runtime-intelligence-performance__notice">{runtimeCopy.operatorAction}</p>
            <form className="runtime-intelligence-performance__form" onSubmit={event => { event.preventDefault(); void analyze(); }}>
              <label>{copy.from}<input type="datetime-local" value={fromLocal} disabled={loading}
                onChange={event => { setFromLocal(event.currentTarget.value); setResult(null); setError(null); }}/></label>
              <label>{copy.to}<input type="datetime-local" value={toLocal} disabled={loading}
                onChange={event => { setToLocal(event.currentTarget.value); setResult(null); setError(null); }}/></label>
              <button type="submit" disabled={loading || !windowResult.window}>
                {loading ? runtimeCopy.loading : runtimeCopy.analyze}
              </button>
            </form>
            {!windowResult.window ? <p className="runtime-intelligence-performance__error" role="alert">
              {windowResult.error === 'missing' ? copy.windowRequired : copy.windowInvalid}
            </p> : <p className="runtime-intelligence-performance__hint">{runtimeCopy.windowLimit}</p>}
            {error ? <p className="runtime-intelligence-performance__error" role="alert">{error}</p> : null}
            {result ? <PerformanceResult result={result.response} rule={result.rule} locale={locale} copy={copy}/> : null}
          </>}
  </div>;
}

function parseSignalIds(value: string): string[] | null {
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed) || parsed.length < 2 || parsed.length > 3 ||
        parsed.some(item => typeof item !== 'string' || !UUID_PATTERN.test(item))) return null;
    const normalized = parsed as string[];
    return new Set(normalized.map(item => item.toLowerCase())).size === normalized.length ? normalized : null;
  } catch {
    return null;
  }
}

function runtimeErrorText(code: string, copy: ReturnType<typeof performanceCopy>, locale: EngineeringLocale): string {
  if (code === 'CONFIGURED_RULE_NOT_IN_CATALOG') return runtimeElementCopy(locale).staleRule;
  if (code === 'WINDOW_REQUIRED') return copy.windowRequired;
  if (code === 'WINDOW_INVALID') return copy.windowInvalid;
  return errorText(code, copy);
}

function runtimeElementCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Recorded performance', transient: 'temporary result', runtimeOnly: 'This reference is interactive only in the live Runtime. The Engineering editor never queries Historian.',
    liveRuntimeRequired: 'Performance queries are unavailable during historical playback or when Runtime View/TrendUse is not granted.',
    invalidConfiguration: 'The configured scope, assignment, profile, rule or signals are invalid. The indicator fails closed.',
    operatorAction: 'A Historian query runs only after you select Analyze. Each query rechecks Authority, entitlement and execution permit on the server.',
    analyze: 'Analyze', loading: 'Querying…', windowLimit: 'Times use local input and are sent as UTC. Maximum window: 31 days.', staleRule: 'The configured rule version is no longer available in the current catalog.'
  };
  if (locale === 'es') return {
    title: 'Rendimiento registrado', transient: 'resultado temporal', runtimeOnly: 'Esta referencia solo es interactiva en el Runtime en vivo. Engineering nunca consulta Historian.',
    liveRuntimeRequired: 'Las consultas de rendimiento no están disponibles durante la reproducción histórica o sin permisos Runtime View/TrendUse.',
    invalidConfiguration: 'El alcance, asignación, perfil, regla o señales configurados no son válidos. El indicador se cierra por seguridad.',
    operatorAction: 'Historian solo se consulta después de seleccionar Analizar. Cada consulta vuelve a comprobar Authority, licencia y permiso de ejecución en el servidor.',
    analyze: 'Analizar', loading: 'Consultando…', windowLimit: 'Las horas locales se envían como UTC. Intervalo máximo: 31 días.', staleRule: 'La versión de la regla configurada ya no está disponible en el catálogo actual.'
  };
  return {
    title: 'Desempenho registrado', transient: 'resultado temporário', runtimeOnly: 'Esta referência só é interativa no Runtime ao vivo. O Engineering não consulta o Historian.',
    liveRuntimeRequired: 'Consultas de desempenho não ficam disponíveis durante reprodução histórica nem sem as permissões Runtime View/TrendUse.',
    invalidConfiguration: 'Escopo, associação, perfil, regra ou sinais inválidos. O indicador falha fechado.',
    operatorAction: 'O Historian só é consultado após selecionar Analisar. Cada consulta revalida Authority, licença e permit de execução no servidor.',
    analyze: 'Analisar', loading: 'Consultando…', windowLimit: 'Horários locais são enviados em UTC. Janela máxima: 31 dias.', staleRule: 'A versão configurada da regra não está mais disponível no catálogo atual.'
  };
}

class PerformanceVisualError extends Error {
  constructor(readonly code: string) { super(code); }
}
