import type { CSSProperties } from 'react';
import { useAuth } from '../../auth/AuthGate';
import { useIntelligenceAnalyticsRuntimeContext } from '../../visual-runtime';
import type { EngineeringLocale } from '../i18n';
import { IntelligenceReferenceEvidencePanel } from '../IntelligenceReferenceEvidencePanel';
import './IntelligenceReferenceEvidenceVisualElement.css';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function IntelligenceReferenceEvidenceVisualElement({ scopeId, profileId, style, runtimeObjectId, locale, title }: {
  scopeId: string; profileId: string; style: CSSProperties; runtimeObjectId?: string;
  locale: EngineeringLocale; title?: string;
}) {
  const runtime = useIntelligenceAnalyticsRuntimeContext();
  const { profile } = useAuth();
  const sessionKey = profile
    ? [profile.subjectId, profile.identityProvider ?? '', profile.expiresAtUtc ?? '', [...profile.roles].sort().join(',')].join('|')
    : 'anonymous';
  // Remount the shared transient form when any Runtime/session fence changes.
  const key = JSON.stringify([scopeId, profileId, runtime?.runtimeContextId, runtime?.projectKey,
    runtime?.activeRevision, runtime?.enabled, sessionKey]);
  const valid = UUID.test(scopeId) && UUID.test(profileId);
  const canQuery = Boolean(valid && runtime?.enabled && runtime.projectKey && runtime.activeRevision !== null);
  return <section className="visual-editor-object runtime-intelligence-reference-evidence"
    data-testid="runtime-intelligence-reference-evidence" data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId} title={title}
    style={{ ...style, flexDirection: 'column', alignItems: 'stretch', whiteSpace: 'normal',
      boxSizing: 'border-box', overflow: 'auto', padding: 12, fontSize: 16,
      background: 'var(--app-surface, #111827)', color: 'var(--app-text-primary, #E2E8F0)' }}>
    {valid ? <IntelligenceReferenceEvidencePanel key={key} runtimeView locale={locale} scopeId={scopeId} profileId={profileId}
      projectKey={runtime?.projectKey ?? null} activeRevision={runtime?.activeRevision ?? null} canQuery={canQuery}/>
      : <p role="alert">{locale === 'pt-BR' ? 'Não foi possível identificar o equipamento e seu perfil. Gere novamente esta vista na Engenharia.'
        : locale === 'es' ? 'No se pudo identificar el equipo y su perfil. Genere de nuevo esta vista en Ingeniería.'
          : 'The equipment and profile could not be identified. Generate this view again in Engineering.'}</p>}
  </section>;
}
