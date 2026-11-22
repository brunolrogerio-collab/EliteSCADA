import { useMemo, type CSSProperties } from 'react';
import { useAuth } from '../../auth/AuthGate';
import { useIntelligenceAnalyticsRuntimeContext } from '../../visual-runtime';
import type { EngineeringLocale } from '../i18n';
import { readIntelligenceContextReferences } from '../intelligenceContextReferences';
import { IntelligenceContextEligibilityPanel } from '../IntelligenceContextEligibilityPanel';
import './IntelligenceReferenceEvidenceVisualElement.css';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function IntelligenceContextEligibilityVisualElement({ scopeId, assignmentId, profileId, contextsJson, style, runtimeObjectId, locale, title }: {
  scopeId: string; assignmentId: string; profileId: string; contextsJson: string;
  style: CSSProperties; runtimeObjectId?: string; locale: EngineeringLocale; title?: string;
}) {
  const runtime = useIntelligenceAnalyticsRuntimeContext();
  const { profile } = useAuth();
  const references = useMemo(() => readIntelligenceContextReferences(contextsJson), [contextsJson]);
  const contexts = useMemo(() => references?.map(context => ({
    id: context.id, displayLabel: context.displayLabel, version: context.version, assignmentId,
    predicates: context.predicateTagIds.map(tagId => ({ tagId }))
  })) ?? [], [references, assignmentId]);
  const sessionKey = profile ? [profile.subjectId, profile.identityProvider ?? '', profile.expiresAtUtc ?? '', [...profile.roles].sort().join(',')].join('|') : 'anonymous';
  const key = JSON.stringify([scopeId, assignmentId, profileId, contextsJson, runtime?.runtimeContextId,
    runtime?.projectKey, runtime?.activeRevision, runtime?.enabled, sessionKey]);
  const valid = [scopeId, assignmentId, profileId].every(id => UUID.test(id)) && references !== null;
  const canQuery = Boolean(valid && runtime?.enabled && runtime.projectKey && runtime.activeRevision !== null);
  return <section className="visual-editor-object runtime-intelligence-reference-evidence runtime-intelligence-context-eligibility"
    data-testid="runtime-intelligence-context-eligibility" data-object-id={runtimeObjectId} data-runtime-object-id={runtimeObjectId} title={title}
    style={{ ...style, flexDirection: 'column', alignItems: 'stretch', whiteSpace: 'normal',
      boxSizing: 'border-box', overflow: 'auto', padding: 12, fontSize: 16,
      background: 'var(--app-surface, #111827)', color: 'var(--app-text-primary, #E2E8F0)' }}>
    {valid ? <IntelligenceContextEligibilityPanel key={key} runtimeView locale={locale} scopeId={scopeId}
      assignmentId={assignmentId} profileId={profileId} contexts={contexts} projectKey={runtime?.projectKey ?? null}
      activeRevision={runtime?.activeRevision ?? null} canQuery={canQuery}/>
      : <p role="alert">{locale === 'pt-BR' ? 'Não foi possível identificar os contextos deste equipamento. Gere novamente esta vista na Engenharia.'
        : locale === 'es' ? 'No se pudieron identificar los contextos de este equipo. Genere de nuevo esta vista en Ingeniería.'
          : 'The equipment contexts could not be identified. Generate this view again in Engineering.'}</p>}
  </section>;
}
