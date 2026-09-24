import { useEffect, useRef, useState } from 'react';
import {
  admitRuntimeSession,
  releaseRuntimeSession,
  type RuntimeSessionAdmissionOutcome,
  type RuntimeSessionConnectionClass
} from '../runtimeSessionAdmissionApi';
import type { EngineeringLocale } from '../../engineering/i18n';

type Copy = { title: string; viewOnly: string; interactive: string; end: string; requested: string; granted: string; admission: string; capacity: string; unavailable: string; ending: string };

const copy: Record<EngineeringLocale, Copy> = {
  'pt-BR': { title: 'Classe da sessão Runtime', viewOnly: 'Solicitar somente leitura', interactive: 'Solicitar interativa', end: 'Encerrar sessão', requested: 'Solicitada', granted: 'Concedida', admission: 'Motivo de admissão', capacity: 'Motivo de capacidade', unavailable: 'Sem sessão Runtime ativa.', ending: 'Encerrando…' },
  en: { title: 'Runtime session class', viewOnly: 'Request view-only', interactive: 'Request interactive', end: 'End session', requested: 'Requested', granted: 'Granted', admission: 'Admission reason', capacity: 'Capacity reason', unavailable: 'No Runtime session is active.', ending: 'Ending…' },
  es: { title: 'Clase de sesión Runtime', viewOnly: 'Solicitar solo lectura', interactive: 'Solicitar interactiva', end: 'Finalizar sesión', requested: 'Solicitada', granted: 'Concedida', admission: 'Motivo de admisión', capacity: 'Motivo de capacidad', unavailable: 'No hay sesión Runtime activa.', ending: 'Finalizando…' }
};

/** A user-owned lease surface. It never turns a viewOnly grant into a mutation path. */
export function RuntimeSessionClassPanel({ locale }: { locale: EngineeringLocale }) {
  const text = copy[locale];
  const lease = useRef<RuntimeSessionAdmissionOutcome | null>(null);
  const [outcome, setOutcome] = useState<RuntimeSessionAdmissionOutcome | null>(null);
  const [busy, setBusy] = useState<RuntimeSessionConnectionClass | 'end' | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => () => {
    const current = lease.current;
    if (current) void releaseRuntimeSession(current);
  }, []);

  async function request(requestedClass: RuntimeSessionConnectionClass) {
    setBusy(requestedClass); setError(null);
    try {
      if (lease.current) await releaseRuntimeSession(lease.current);
      const next = await admitRuntimeSession(requestedClass);
      lease.current = next;
      setOutcome(next);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally { setBusy(null); }
  }

  async function end() {
    if (!lease.current) return;
    setBusy('end'); setError(null);
    try {
      await releaseRuntimeSession(lease.current);
      lease.current = null; setOutcome(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally { setBusy(null); }
  }

  return <details className="runtime-session-class" data-testid="runtime-session-class">
    <summary>{text.title}</summary>
    <div className="runtime-session-class__controls">
      <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-viewOnly" disabled={busy !== null} onClick={() => void request('viewOnly')}>{text.viewOnly}</button>
      <button type="button" className="runtime-operator-button" data-testid="runtime-session-request-interactive" disabled={busy !== null} onClick={() => void request('interactive')}>{text.interactive}</button>
      {outcome ? <button type="button" className="runtime-operator-button" data-testid="runtime-session-end" disabled={busy !== null} onClick={() => void end()}>{busy === 'end' ? text.ending : text.end}</button> : null}
    </div>
    {outcome ? <dl className="runtime-session-class__status" data-testid="runtime-session-status">
      <dt>{text.requested}</dt><dd>{outcome.requestedClass ?? '—'}</dd>
      <dt>{text.granted}</dt><dd>{outcome.grantedClass ?? '—'}</dd>
      <dt>{text.admission}</dt><dd>{outcome.admissionReasonCode ?? '—'}</dd>
      <dt>{text.capacity}</dt><dd>{outcome.capacityReasonCode ?? '—'}</dd>
    </dl> : <p>{text.unavailable}</p>}
    {error ? <p role="alert">{error}</p> : null}
  </details>;
}
