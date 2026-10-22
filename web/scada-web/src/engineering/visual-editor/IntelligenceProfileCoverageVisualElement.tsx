import { useEffect, useMemo, useRef, useState } from 'react';
import type { CSSProperties, FormEvent } from 'react';
import { useAuth } from '../../auth/AuthGate';
import type { EngineeringLocale } from '../i18n';
import {
  CoverageResult,
  coverageCopy,
  coverageErrorText,
  isMatchingResponse,
  localDateTimeInput,
  parseWindow,
  type ProfileCoverageResponse
} from '../AssetProcessIntelligenceCoveragePanel';
import { useIntelligenceAnalyticsRuntimeContext } from '../../visual-runtime';
import './IntelligenceProfileCoverageVisualElement.css';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
const COVERAGE_API = '/api/engineering/asset-process-intelligence/analytics/profile-coverage';
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DEFAULT_BUCKET_SECONDS = 900;

type Props = Readonly<{
  scopeId: string;
  profileId: string;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale: EngineeringLocale;
  title?: string;
  requiredAssignmentId?: string;
}>;

export function IntelligenceProfileCoverageVisualElement({
  scopeId,
  profileId,
  style,
  runtimeObjectId,
  locale,
  title,
  requiredAssignmentId
}: Props) {
  const runtime = useIntelligenceAnalyticsRuntimeContext();
  const { profile: authProfile } = useAuth();
  const copy = useMemo(() => coverageCopy(locale), [locale]);
  const configValid = UUID_PATTERN.test(scopeId) && UUID_PATTERN.test(profileId);
  const sessionKey = authProfile
    ? [authProfile.subjectId, authProfile.identityProvider ?? '', authProfile.expiresAtUtc ?? '', [...authProfile.roles].sort().join(',')].join('|')
    : 'anonymous';
  const contextKey = [runtime?.runtimeContextId ?? 'authoring', runtime?.projectKey ?? '', runtime?.activeRevision ?? '', runtime?.enabled ?? false, sessionKey].join('|');
  const [fromLocal, setFromLocal] = useState(() => localDateTimeInput(new Date(Date.now() - 24 * 60 * 60 * 1000)));
  const [toLocal, setToLocal] = useState(() => localDateTimeInput(new Date()));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ProfileCoverageResponse | null>(null);
  const controller = useRef<AbortController | null>(null);
  const requestVersion = useRef(0);
  const windowResult = parseWindow(fromLocal, toLocal);

  useEffect(() => {
    requestVersion.current += 1;
    controller.current?.abort();
    controller.current = null;
    setLoading(false);
    setError(null);
    setResult(null);
    return () => controller.current?.abort();
  }, [contextKey, scopeId, profileId, fromLocal, toLocal]);

  const analyze = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!runtime?.enabled || !runtime.projectKey || runtime.activeRevision === null ||
        !configValid || !windowResult.window || loading) return;

    requestVersion.current += 1;
    const version = requestVersion.current;
    controller.current?.abort();
    const requestController = new AbortController();
    controller.current = requestController;
    setResult(null);
    setError(null);
    setLoading(true);
    try {
      // The backend remains the authority for TrendUse, license, active scope, permit, HA fence and per-TAG TagRead.
      const response = await fetch(`${API}${COVERAGE_API}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', accept: 'application/json' },
        body: JSON.stringify({
          scopeKind: 0,
          scopeId,
          profileId,
          window: windowResult.window,
          bucketSeconds: DEFAULT_BUCKET_SECONDS
        }),
        signal: requestController.signal
      });
      const payload = await response.json().catch(() => null) as ProfileCoverageResponse | { code?: unknown } | null;
      if (!response.ok) {
        const code = payload && 'code' in payload && typeof payload.code === 'string'
          ? payload.code
          : `HTTP_${response.status}`;
        throw new CoverageVisualError(code);
      }
      if (!isMatchingResponse(
        payload,
        scopeId,
        profileId,
        runtime.projectKey,
        runtime.activeRevision,
        windowResult.window,
        requiredAssignmentId
      )) throw new CoverageVisualError('INVALID_RESPONSE');
      if (requestVersion.current === version && !requestController.signal.aborted) setResult(payload);
    } catch (reason) {
      if (!requestController.signal.aborted && requestVersion.current === version) {
        const code = reason instanceof CoverageVisualError ? reason.code
          : reason instanceof Error ? reason.message : 'NETWORK_ERROR';
        setError(coverageErrorText(code, copy));
      }
    } finally {
      if (requestVersion.current === version) setLoading(false);
    }
  };

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
    className="visual-editor-object runtime-intelligence-coverage"
    style={rootStyle}
    data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId}
    data-testid="runtime-intelligence-profile-coverage"
    data-intelligence-runtime-enabled={runtime?.enabled === true}
    data-runtime-session-control="intelligence-analytics"
    title={title}
  >
    <header className="runtime-intelligence-coverage__header">
      <strong>{title || copy.runtimeHeader}</strong>
      <span>{copy.runtimeDescription}</span>
    </header>
    {!configValid ? <p className="runtime-intelligence-coverage__notice" role="alert">{copy.unavailableResponse}</p>
      : !runtime ? <p className="runtime-intelligence-coverage__notice" role="note">{copy.runtimeOnly}</p>
        : !runtime.enabled || !runtime.projectKey || runtime.activeRevision === null
          ? <p className="runtime-intelligence-coverage__notice" role="note">{copy.runtimeUnavailable}</p>
          : <>
            <form className="runtime-intelligence-coverage__form" onSubmit={event => void analyze(event)}>
              <label>{copy.from}<input type="datetime-local" value={fromLocal} disabled={loading}
                onChange={event => { setFromLocal(event.currentTarget.value); setResult(null); setError(null); }}/></label>
              <label>{copy.to}<input type="datetime-local" value={toLocal} disabled={loading}
                onChange={event => { setToLocal(event.currentTarget.value); setResult(null); setError(null); }}/></label>
              <button type="submit" disabled={loading || !windowResult.window}>
                {loading ? copy.loading : copy.analyze}
              </button>
            </form>
            {!windowResult.window ? <p className="runtime-intelligence-coverage__error" role="alert">
              {windowResult.error === 'missing' ? copy.windowRequired : copy.windowInvalid}
            </p> : <p className="runtime-intelligence-coverage__notice">{copy.utcHint}</p>}
            {error ? <p className="runtime-intelligence-coverage__error" role="alert">{error}</p> : null}
            {result ? <CoverageResult result={result} locale={locale} copy={copy}/> : null}
          </>}
  </div>;
}

class CoverageVisualError extends Error {
  constructor(readonly code: string) { super(code); }
}
