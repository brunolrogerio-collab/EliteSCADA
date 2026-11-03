import { useMemo, type CSSProperties } from 'react';
import { useAuth } from '../../auth/AuthGate';
import { useIntelligenceAnalyticsRuntimeContext } from '../../visual-runtime';
import type { EngineeringLocale } from '../i18n';
import { IntelligenceSingleTargetDeviationPanel } from '../IntelligenceSingleTargetDeviationPanel';
import { readSingleTargetSignals } from '../intelligenceSingleTargetReferences';
import './IntelligenceReferenceEvidenceVisualElement.css';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function IntelligenceSingleTargetVisualElement({ scopeId, assignmentId, profileId, signalsJson, style, runtimeObjectId, locale, title }: {
  scopeId: string; assignmentId: string; profileId: string; signalsJson: string;
  style: CSSProperties; runtimeObjectId?: string; locale: EngineeringLocale; title?: string;
}) {
  const runtime = useIntelligenceAnalyticsRuntimeContext();
  const { profile } = useAuth();
  const signals = useMemo(() => readSingleTargetSignals(signalsJson), [signalsJson]);
  const sessionKey = profile ? [profile.subjectId, profile.identityProvider ?? '', profile.expiresAtUtc ?? '', [...profile.roles].sort().join(',')].join('|') : 'anonymous';
  const key = JSON.stringify([scopeId, assignmentId, profileId, signalsJson, runtime?.runtimeContextId,
    runtime?.projectKey, runtime?.activeRevision, runtime?.enabled, sessionKey]);
  const valid = [scopeId, assignmentId, profileId].every(id => UUID.test(id) && id !== '00000000-0000-0000-0000-000000000000') && signals !== null;
  const canQuery = Boolean(valid && runtime?.enabled && runtime.projectKey && runtime.activeRevision !== null);
  return <section className="visual-editor-object runtime-intelligence-reference-evidence runtime-intelligence-single-target"
    data-testid="runtime-intelligence-single-target" data-object-id={runtimeObjectId} data-runtime-object-id={runtimeObjectId} title={title}
    style={{ ...style, flexDirection: 'column', alignItems: 'stretch', whiteSpace: 'normal',
      boxSizing: 'border-box', overflow: 'auto', padding: 12, fontSize: 16,
      background: 'var(--app-surface, #111827)', color: 'var(--app-text-primary, #E2E8F0)' }}>
    {valid && !canQuery && <p>{locale==='pt-BR'?'No editor e na reprodução histórica, esta vista não consulta análises.':locale==='es'?'En el editor y la reproducción histórica esta vista no consulta análisis.':'In the editor and historical playback, this view does not query analytics.'}</p>}
    {valid ? <IntelligenceSingleTargetDeviationPanel key={key} locale={locale} scopeId={scopeId}
      assignmentId={assignmentId} profileId={profileId} signalReferences={signals!} tags={[]} projectKey={runtime?.projectKey ?? null}
      activeRevision={runtime?.activeRevision ?? null} canQuery={canQuery}/>
      : <p role="alert">{locale === 'pt-BR' ? 'Não foi possível identificar as medições desta comparação deste equipamento. Gere novamente esta vista na Engenharia.'
        : locale === 'es' ? 'No se pudieron identificar las mediciones de esta comparación de este equipo. Genere de nuevo esta vista en Ingeniería.'
          : 'The equipment comparison measurements could not be identified. Generate this view again in Engineering.'}</p>}
  </section>;
}
