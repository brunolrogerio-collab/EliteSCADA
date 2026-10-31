import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import {
  admitRuntimeSession,
  getActiveRuntimeSession,
  heartbeatRuntimeSession,
  releaseRuntimeSession,
  setActiveRuntimeSession,
  RuntimeSessionAdmissionError,
  type RuntimeSessionAdmissionOutcome,
  type RuntimeSessionConnectionClass
} from '../runtimeSessionAdmissionApi';
import type { EngineeringLocale } from '../../engineering/i18n';

type Copy = {
  title: string;
  viewOnly: string;
  interactive: string;
  end: string;
  requested: string;
  granted: string;
  admission: string;
  capacity: string;
  technicalDetails: string;
  unavailable: string;
  ending: string;
  fallback: string;
  viewOnlyGranted: string;
  rejected: string;
};

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': {
    title: 'Classe da sessão Runtime',
    viewOnly: 'Visualização',
    interactive: 'Interativa',
    end: 'Encerrar sessão',
    requested: 'Solicitada',
    granted: 'Concedida',
    admission: 'Motivo de admissão',
    capacity: 'Motivo de capacidade',
    technicalDetails: 'Detalhes técnicos',
    unavailable: 'Sem sessão Runtime ativa.',
    ending: 'Encerrando…',
    fallback: 'As vagas interativas estão indisponíveis; o servidor abriu esta sessão em Somente visualização.',
    viewOnlyGranted: 'Sessão aberta em Somente visualização conforme solicitado.',
    rejected: 'A sessão Runtime não pôde ser admitida.'
  },
  en: {
    title: 'Runtime session class',
    viewOnly: 'View only',
    interactive: 'Interactive',
    end: 'End session',
    requested: 'Requested',
    granted: 'Granted',
    admission: 'Admission reason',
    capacity: 'Capacity reason',
    technicalDetails: 'Technical details',
    unavailable: 'No Runtime session is active.',
    ending: 'Ending…',
    fallback: 'Interactive capacity is unavailable; the server opened this session as View Only.',
    viewOnlyGranted: 'Session opened as View Only as requested.',
    rejected: 'The Runtime session could not be admitted.'
  },
  es: {
    title: 'Clase de sesión Runtime',
    viewOnly: 'Visualización',
    interactive: 'Interactiva',
    end: 'Finalizar sesión',
    requested: 'Solicitada',
    granted: 'Concedida',
    admission: 'Motivo de admisión',
    capacity: 'Motivo de capacidad',
    technicalDetails: 'Detalles técnicos',
    unavailable: 'No hay sesión Runtime activa.',
    ending: 'Finalizando…',
    fallback: 'La capacidad interactiva no está disponible; el servidor abrió esta sesión como Solo visualización.',
    viewOnlyGranted: 'Sesión abierta como Solo visualización según lo solicitado.',
    rejected: 'No fue posible admitir la sesión Runtime.'
  }
};

type RuntimeSessionContextValue = {
  admissionSupported: boolean;
  outcome: RuntimeSessionAdmissionOutcome | null;
  capacityReasonCode: string | null;
  busy: RuntimeSessionConnectionClass | 'end' | null;
  error: string | null;
  notice: string | null;
  lastRequestedClass: RuntimeSessionConnectionClass | null;
  request: (requestedClass: RuntimeSessionConnectionClass, locale: EngineeringLocale) => Promise<void>;
  ensure: (requestedClass: RuntimeSessionConnectionClass, locale: EngineeringLocale) => Promise<void>;
  end: (locale: EngineeringLocale) => Promise<void>;
};

const RuntimeSessionContext = createContext<RuntimeSessionContextValue | null>(null);
const SESSION_HEARTBEAT_INTERVAL_MS = 20_000;

export function RuntimeSessionClassProvider({ children, authenticationEnabled }: { children: ReactNode; authenticationEnabled: boolean }) {
  const initialSession = getActiveRuntimeSession();
  const lease = useRef<RuntimeSessionAdmissionOutcome | null>(initialSession);
  const ensureInFlight = useRef<Promise<void> | null>(null);
  const endInFlight = useRef<Promise<void> | null>(null);
  const currentLocale = useRef<EngineeringLocale>('pt-BR');
  const [outcome, setOutcome] = useState<RuntimeSessionAdmissionOutcome | null>(initialSession);
  const [capacityReasonCode, setCapacityReasonCode] = useState<string | null>(null);
  const [busy, setBusy] = useState<RuntimeSessionConnectionClass | 'end' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [lastRequestedClass, setLastRequestedClass] = useState<RuntimeSessionConnectionClass | null>(null);

  useEffect(() => () => {
    const current = lease.current;
    lease.current = null;
    setActiveRuntimeSession(null);
    if (current) void releaseRuntimeSession(current);
  }, []);

  useEffect(() => {
    if (!outcome) return;
    let disposed = false;
    let inFlight = false;
    const timer = window.setInterval(() => {
      if (disposed || inFlight || !lease.current) return;
      inFlight = true;
      const current = lease.current;
      void heartbeatRuntimeSession(current).then(renewed => {
        if (disposed || lease.current?.sessionId !== current.sessionId) return;
        lease.current = renewed;
        setOutcome(renewed);
        setCapacityReasonCode(null);
        setActiveRuntimeSession(renewed);
      }).catch(reason => {
        if (disposed || lease.current?.sessionId !== current.sessionId) return;
        lease.current = null;
        setOutcome(null);
        setActiveRuntimeSession(null);
        setCapacityReasonCode(reason instanceof RuntimeSessionAdmissionError ? reason.capacityReasonCode ?? null : null);
        const text = copy[currentLocale.current];
        const detail = reason instanceof Error ? reason.message : String(reason);
        setError([text.rejected, detail].filter(Boolean).join(' '));
        setNotice(null);
      }).finally(() => { inFlight = false; });
    }, SESSION_HEARTBEAT_INTERVAL_MS);
    return () => {
      disposed = true;
      window.clearInterval(timer);
    };
  }, [outcome?.sessionId]);

  const request = useCallback(async (requestedClass: RuntimeSessionConnectionClass, locale: EngineeringLocale) => {
    if (!authenticationEnabled) return;
    currentLocale.current = locale;
    setLastRequestedClass(requestedClass);
    const text = copy[locale];
    setBusy(requestedClass); setError(null); setNotice(null); setCapacityReasonCode(null);
    try {
      if (lease.current) {
        await releaseRuntimeSession(lease.current);
        lease.current = null;
        setOutcome(null);
        setActiveRuntimeSession(null);
      }
      const next = await admitRuntimeSession(requestedClass);
      lease.current = next;
      setOutcome(next);
      setCapacityReasonCode(null);
      setActiveRuntimeSession(next);
      if (requestedClass === 'interactive' && next.grantedClass === 'viewOnly') setNotice(text.fallback);
      else if (requestedClass === 'viewOnly' && next.grantedClass === 'viewOnly') setNotice(text.viewOnlyGranted);
    } catch (reason) {
      if (reason instanceof RuntimeSessionAdmissionError) {
        setCapacityReasonCode(reason.capacityReasonCode ?? null);
        setError([text.rejected, reason.capacityReasonCode, reason.message].filter(Boolean).join(' '));
      } else {
        setCapacityReasonCode(null);
        setError(reason instanceof Error ? reason.message : String(reason));
      }
    } finally { setBusy(null); }
  }, [authenticationEnabled]);

  const ensure = useCallback(async (requestedClass: RuntimeSessionConnectionClass, locale: EngineeringLocale) => {
    // The server does not expose seat leases when global Authority authentication is
    // disabled. Keep the existing anonymous/demo Runtime path intact; optional public
    // Runtime seat policy belongs to the separate #782 contract.
    if (!authenticationEnabled) return;
    currentLocale.current = locale;
    setLastRequestedClass(requestedClass);
    if (lease.current) return;
    if (ensureInFlight.current) return ensureInFlight.current;

    const text = copy[locale];
    const operation = (async () => {
      setBusy(requestedClass); setError(null); setNotice(null); setCapacityReasonCode(null);
      try {
        const next = await admitRuntimeSession(requestedClass);
        lease.current = next;
        setOutcome(next);
        setCapacityReasonCode(null);
        setActiveRuntimeSession(next);
        if (requestedClass === 'interactive' && next.grantedClass === 'viewOnly') setNotice(text.fallback);
        else if (requestedClass === 'viewOnly' && next.grantedClass === 'viewOnly') setNotice(text.viewOnlyGranted);
      } catch (reason) {
        if (reason instanceof RuntimeSessionAdmissionError) {
          setCapacityReasonCode(reason.capacityReasonCode ?? null);
          setError([text.rejected, reason.capacityReasonCode, reason.message].filter(Boolean).join(' '));
        } else {
          setCapacityReasonCode(null);
          setError(reason instanceof Error ? reason.message : String(reason));
        }
      } finally { setBusy(null); }
    })();
    ensureInFlight.current = operation;
    try { await operation; }
    finally { if (ensureInFlight.current === operation) ensureInFlight.current = null; }
  }, [authenticationEnabled]);

  const end = useCallback(async (locale: EngineeringLocale) => {
    currentLocale.current = locale;
    if (endInFlight.current) return endInFlight.current;
    if (!lease.current) return;
    const current = lease.current;
    setBusy('end'); setError(null); setNotice(null); setCapacityReasonCode(null);
    const operation = (async () => {
      try {
        await releaseRuntimeSession(current);
        if (lease.current?.sessionId === current.sessionId) {
          lease.current = null; setOutcome(null);
          setActiveRuntimeSession(null);
        }
      } catch (reason) {
        setCapacityReasonCode(reason instanceof RuntimeSessionAdmissionError ? reason.capacityReasonCode ?? null : null);
        setError(reason instanceof Error ? reason.message : String(reason));
      } finally { setBusy(null); }
    })();
    endInFlight.current = operation;
    try { await operation; }
    finally { if (endInFlight.current === operation) endInFlight.current = null; }
  }, []);

  return <RuntimeSessionContext.Provider value={{ admissionSupported: authenticationEnabled, outcome, capacityReasonCode, busy, error, notice, lastRequestedClass, request, ensure, end }}>
    {children}
  </RuntimeSessionContext.Provider>;
}

export function useRuntimeSessionAdmission(): RuntimeSessionContextValue {
  const session = useContext(RuntimeSessionContext);
  if (!session) throw new Error('Runtime session admission requires RuntimeSessionClassProvider.');
  return session;
}

function sessionClassLabel(value: RuntimeSessionConnectionClass | null | undefined, text: Copy) {
  if (value === 'viewOnly') return text.viewOnly;
  if (value === 'interactive') return text.interactive;
  return '—';
}

/** Shared user-owned controls: closing the account menu does not end the Runtime session. */
export function RuntimeSessionClassPanel({ locale }: { locale: EngineeringLocale }) {
  const text = copy[locale];
  const session = useContext(RuntimeSessionContext);
  if (!session) throw new Error('RuntimeSessionClassPanel requires RuntimeSessionClassProvider.');
  if (session.outcome?.publicRuntime) return null;
  if (!session.admissionSupported) return null;
  const { outcome, busy, error, notice } = session;

  return <div className="runtime-session-class" data-testid="runtime-session-class">
    <strong className="runtime-session-class__title">{text.title}</strong>
    <div className="runtime-session-class__controls">
      <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-viewOnly" disabled={busy !== null} onClick={() => void session.request('viewOnly', locale)}>{text.viewOnly}</button>
      <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-interactive" disabled={busy !== null} onClick={() => void session.request('interactive', locale)}>{text.interactive}</button>
      {outcome ? <button type="button" className="runtime-operator-button runtime-session-class__end" data-testid="runtime-session-end" disabled={busy !== null} onClick={() => void session.end(locale)}>{busy === 'end' ? text.ending : text.end}</button> : null}
    </div>
    {notice ? <p className="runtime-session-class__notice" role="status" data-testid="runtime-session-notice">{notice}</p> : null}
    {outcome ? <>
      <dl className="runtime-session-class__status" data-testid="runtime-session-status">
        <dt>{text.requested}</dt><dd>{sessionClassLabel(outcome.requestedClass, text)}</dd>
        <dt>{text.granted}</dt><dd>{sessionClassLabel(outcome.grantedClass, text)}</dd>
      </dl>
      {(outcome.admissionReasonCode || outcome.capacityReasonCode) ? <details className="runtime-session-class__diagnostics" data-testid="runtime-session-diagnostics">
        <summary>{text.technicalDetails}</summary>
        <dl>
          <dt>{text.admission}</dt><dd>{outcome.admissionReasonCode ?? '—'}</dd>
          <dt>{text.capacity}</dt><dd>{outcome.capacityReasonCode ?? '—'}</dd>
        </dl>
      </details> : null}
    </> : <p className="runtime-session-class__empty" data-testid="runtime-session-empty">{text.unavailable}</p>}
    {error ? <p className="runtime-session-class__error" role="alert">{error}</p> : null}
  </div>;
}
