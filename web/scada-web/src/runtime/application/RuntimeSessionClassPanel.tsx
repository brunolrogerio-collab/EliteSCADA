import { useEffect, useRef, useState } from 'react';
import {
  admitRuntimeSession,
  releaseRuntimeSession,
  RuntimeSessionAdmissionError,
  type RuntimeSessionAdmissionOutcome,
  type RuntimeSessionConnectionClass
} from '../runtimeSessionAdmissionApi';
import type { EngineeringLocale } from '../../engineering/i18n';

type Copy = { title: string; viewOnly: string; interactive: string; end: string; requested: string; granted: string; admission: string; capacity: string; unavailable: string; ending: string; fallback: string; viewOnlyGranted: string; rejected: string };

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': { title: 'Classe da sessão Runtime', viewOnly: 'Solicitar somente visualização', interactive: 'Solicitar interativa', end: 'Encerrar sessão', requested: 'Solicitada', granted: 'Concedida', admission: 'Motivo de admissão', capacity: 'Motivo de capacidade', unavailable: 'Sem sessão Runtime ativa.', ending: 'Encerrando…', fallback: 'As vagas interativas estão indisponíveis; o servidor abriu esta sessão em Somente visualização.', viewOnlyGranted: 'Sessão aberta em Somente visualização conforme solicitado.', rejected: 'A sessão Runtime não pôde ser admitida.' },
  en: { title: 'Runtime session class', viewOnly: 'Request View Only', interactive: 'Request Interactive', end: 'End session', requested: 'Requested', granted: 'Granted', admission: 'Admission reason', capacity: 'Capacity reason', unavailable: 'No Runtime session is active.', ending: 'Ending…', fallback: 'Interactive capacity is unavailable; the server opened this session as View Only.', viewOnlyGranted: 'Session opened as View Only as requested.', rejected: 'The Runtime session could not be admitted.' },
  es: { title: 'Clase de sesión Runtime', viewOnly: 'Solicitar Solo visualización', interactive: 'Solicitar interactiva', end: 'Finalizar sesión', requested: 'Solicitada', granted: 'Concedida', admission: 'Motivo de admisión', capacity: 'Motivo de capacidad', unavailable: 'No hay sesión Runtime activa.', ending: 'Finalizando…', fallback: 'La capacidad interactiva no está disponible; el servidor abrió esta sesión como Solo visualización.', viewOnlyGranted: 'Sesión abierta como Solo visualización según lo solicitado.', rejected: 'No fue posible admitir la sesión Runtime.' }
};

/** A user-owned lease surface. It never turns a viewOnly grant into a mutation path. */
export function RuntimeSessionClassPanel({ locale }: { locale: EngineeringLocale }) {
  const text = copy[locale];
  const lease = useRef<RuntimeSessionAdmissionOutcome | null>(null);
  const [outcome, setOutcome] = useState<RuntimeSessionAdmissionOutcome | null>(null);
  const [busy, setBusy] = useState<RuntimeSessionConnectionClass | 'end' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => () => {
    const current = lease.current;
    if (current) void releaseRuntimeSession(current);
  }, []);

  async function request(requestedClass: RuntimeSessionConnectionClass) {
    setBusy(requestedClass); setError(null); setNotice(null);
    try {
      if (lease.current) await releaseRuntimeSession(lease.current);
      const next = await admitRuntimeSession(requestedClass);
      lease.current = next;
      setOutcome(next);
      if (requestedClass === 'interactive' && next.grantedClass === 'viewOnly') setNotice(text.fallback);
      else if (requestedClass === 'viewOnly' && next.grantedClass === 'viewOnly') setNotice(text.viewOnlyGranted);
    } catch (reason) {
      if (reason instanceof RuntimeSessionAdmissionError) {
        setError([text.rejected, reason.capacityReasonCode, reason.message].filter(Boolean).join(' '));
      } else {
        setError(reason instanceof Error ? reason.message : String(reason));
      }
    } finally { setBusy(null); }
  }

  async function end() {
    if (!lease.current) return;
    setBusy('end'); setError(null); setNotice(null);
    try {
      await releaseRuntimeSession(lease.current);
      lease.current = null; setOutcome(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally { setBusy(null); }
  }

  return <details className="runtime-session-class" data-testid="runtime-session-class">
    <summary>{text.title}</summary>
    <div className="runtime-session-class__popover">
      <div className="runtime-session-class__controls">
        <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-viewOnly" disabled={busy !== null} onClick={() => void request('viewOnly')}>{text.viewOnly}</button>
        <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-interactive" disabled={busy !== null} onClick={() => void request('interactive')}>{text.interactive}</button>
        {outcome ? <button type="button" className="runtime-operator-button" data-testid="runtime-session-end" disabled={busy !== null} onClick={() => void end()}>{busy === 'end' ? text.ending : text.end}</button> : null}
      </div>
      {notice ? <p className="runtime-session-class__notice" role="status" data-testid="runtime-session-notice">{notice}</p> : null}
      {outcome ? <dl className="runtime-session-class__status" data-testid="runtime-session-status">
        <dt>{text.requested}</dt><dd>{outcome.requestedClass ?? '—'}</dd>
        <dt>{text.granted}</dt><dd>{outcome.grantedClass ?? '—'}</dd>
        <dt>{text.admission}</dt><dd>{outcome.admissionReasonCode ?? '—'}</dd>
        <dt>{text.capacity}</dt><dd>{outcome.capacityReasonCode ?? '—'}</dd>
      </dl> : <p>{text.unavailable}</p>}
      {error ? <p role="alert">{error}</p> : null}
    </div>
  </details>;
}
