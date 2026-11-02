import { useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { IntelligenceProcessStageReportMember as Member } from '../intelligenceDashboardGenerator';
import { IntelligenceContextualDeviationVisualElement } from './IntelligenceContextualDeviationVisualElement';
import { IntelligencePerformanceVisualElement } from './IntelligencePerformanceVisualElement';
import './IntelligenceProcessStageInsightsVisualElement.css';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type ReportKind = 'performance' | 'contextual-deviation' | null;

type Props = Readonly<{
  stageId: string;
  stageKey: string;
  stageName: string;
  membersJson: string;
  style: CSSProperties;
  runtimeObjectId?: string;
  locale: EngineeringLocale;
  title?: string;
}>;

export function IntelligenceProcessStageInsightsVisualElement({
  stageId, stageKey, stageName, membersJson, style, runtimeObjectId, locale, title
}: Props) {
  const members = useMemo(() => parseMembers(membersJson), [membersJson]);
  const [selectedAssignmentId, setSelectedAssignmentId] = useState('');
  const [report, setReport] = useState<ReportKind>(null);
  const selected = members.find(member => member.assignmentId === selectedAssignmentId) ?? members[0] ?? null;
  const copy = text(locale);
  const activeReport = report === 'performance' && selected?.performance ? report
    : report === 'contextual-deviation' && selected?.contextualDeviation ? report : null;
  const configValid = UUID_PATTERN.test(stageId) && stageKey.length > 0 && stageKey === stageKey.trim() &&
    stageKey.length <= 64 && stageName.length > 0 && stageName === stageName.trim() && stageName.length <= 128 &&
    members.length > 0 && members.length <= 32;

  useEffect(() => {
    if (!members.some(member => member.assignmentId === selectedAssignmentId)) {
      setSelectedAssignmentId(members[0]?.assignmentId ?? '');
      setReport(null);
    }
  }, [membersJson, selectedAssignmentId, members]);

  const rootStyle: CSSProperties = {
    ...style, flexDirection: 'column', alignItems: 'stretch', whiteSpace: 'normal',
    boxSizing: 'border-box',
    overflow: 'auto',
    padding: 12,
    border: '1px solid var(--app-border, #475569)',
    borderRadius: 8,
    background: 'var(--app-surface, #111827)',
    color: 'var(--app-text-primary, #E2E8F0)'
  };
  const reportStyle: CSSProperties = {
    position: 'relative', left: 0, top: 0, width: '100%', height: '100%', minHeight: 560,
    transform: 'none', boxSizing: 'border-box', overflow: 'auto'
  };

  return <section
    className="visual-editor-object runtime-intelligence-process-stage-insights"
    style={rootStyle}
    data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId}
    data-testid="runtime-intelligence-process-stage-insights"
    data-stage-id={stageId}
    title={title}
  >
    <header className="runtime-intelligence-process-stage-insights__header">
      <strong>{title || `${copy.header} · ${stageName}`}</strong>
      <span>{copy.description}</span>
    </header>
    {!configValid ? <p role="alert">{copy.invalid}</p> : <>
      <label className="runtime-intelligence-process-stage-insights__member">
        {copy.member}
        <select
          data-testid="runtime-intelligence-process-stage-insights-member"
          value={selected?.assignmentId ?? ''}
          onChange={event => {
            setSelectedAssignmentId(event.currentTarget.value);
            setReport(null);
          }}
        >
          {members.map(member => <option key={member.assignmentId} value={member.assignmentId}>
            {member.order + 1}. {member.displayLabel}
          </option>)}
        </select>
      </label>
      <div className="runtime-intelligence-process-stage-insights__actions" role="group" aria-label={copy.reports}>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-performance"
          disabled={!selected?.performance} aria-pressed={activeReport === 'performance'} onClick={() => setReport('performance')}>{copy.performance}</button>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-deviation"
          disabled={!selected?.contextualDeviation} aria-pressed={activeReport === 'contextual-deviation'} onClick={() => setReport('contextual-deviation')}>{copy.deviation}</button>
      </div>
      {!selected?.performance && <p>{copy.performanceMissing}</p>}
      {!selected?.contextualDeviation && <p>{copy.deviationMissing}</p>}
      {!activeReport || !selected ? <p className="runtime-intelligence-process-stage-insights__hint">{copy.chooseReport}</p>
        : <div className="runtime-intelligence-process-stage-insights__report">{activeReport === 'performance' && selected.performance ? <IntelligencePerformanceVisualElement
          key={`performance:${selected.assignmentId}`}
          scopeId={selected.scopeId}
          assignmentId={selected.assignmentId}
          profileId={selected.profileId}
          ruleId={selected.performance.ruleId}
          ruleVersion={selected.performance.ruleVersion}
          signalIdsJson={JSON.stringify(selected.performance.signalIds)}
          style={reportStyle}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:performance` : undefined}
          locale={locale}
          title={selected.displayLabel}
        /> : selected.contextualDeviation ? <IntelligenceContextualDeviationVisualElement
          key={`deviation:${selected.assignmentId}`}
          scopeId={selected.scopeId}
          assignmentId={selected.assignmentId}
          profileId={selected.profileId}
          targetSignalIdsJson={JSON.stringify(selected.contextualDeviation.targetSignalIds)}
          firstContextSignalId={selected.contextualDeviation.firstContextSignalId}
          firstContextLowerInclusiveCanonical={selected.contextualDeviation.firstContextLowerInclusiveCanonical}
          firstContextUpperExclusiveCanonical={selected.contextualDeviation.firstContextUpperExclusiveCanonical}
          secondContextSignalId={selected.contextualDeviation.secondContextSignalId}
          secondContextLowerInclusiveCanonical={selected.contextualDeviation.secondContextLowerInclusiveCanonical}
          secondContextUpperExclusiveCanonical={selected.contextualDeviation.secondContextUpperExclusiveCanonical}
          style={reportStyle}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:deviation` : undefined}
          locale={locale}
          title={selected.displayLabel}
        /> : null}</div>}
      <p className="runtime-intelligence-process-stage-insights__limit">{copy.noAggregate}</p>
    </>}
  </section>;
}

function parseMembers(value: string): Member[] {
  if (typeof value !== 'string' || value.length > 65536) return [];
  try {
    const raw: unknown = JSON.parse(value);
    if (!Array.isArray(raw) || raw.length < 1 || raw.length > 32) return [];
    const members = raw.map(item => {
      if (!item || typeof item !== 'object' || Array.isArray(item)) return null;
      const member = item as Partial<Member>;
      const partial = member.availabilityVersion === 2;
      if (typeof member.assignmentId !== 'string' || !UUID_PATTERN.test(member.assignmentId) ||
          typeof member.scopeId !== 'string' || !UUID_PATTERN.test(member.scopeId) ||
          typeof member.profileId !== 'string' || !UUID_PATTERN.test(member.profileId) ||
          typeof member.displayLabel !== 'string' || !member.displayLabel.trim() ||
          member.displayLabel !== member.displayLabel.trim() || member.displayLabel.length > 320 ||
          typeof member.order !== 'number' || !Number.isInteger(member.order) || member.order < 0 ||
          (member.availabilityVersion !== undefined && !partial) ||
          ((!partial || member.performance !== undefined) && !validPerformance(member.performance)) ||
          ((!partial || member.contextualDeviation !== undefined) && !validDeviation(member.contextualDeviation))) return null;
      return member as Member;
    });
    if (members.some(member => member === null)) return [];
    const valid = members as Member[];
    if (new Set(valid.map(member => member.assignmentId.toLowerCase())).size !== valid.length ||
        new Set(valid.map(member => `${member.scopeId.toLowerCase()}:${member.profileId.toLowerCase()}`)).size !== valid.length ||
        new Set(valid.map(member => member.order)).size !== valid.length) return [];
    return [...valid].sort((left, right) => left.order - right.order || left.assignmentId.localeCompare(right.assignmentId));
  } catch { return []; }
}

function validPerformance(performance: Member['performance']): boolean {
  return Boolean(performance && typeof performance === 'object' &&
    typeof performance.ruleId === 'string' && performance.ruleId.trim() &&
    performance.ruleId === performance.ruleId.trim() && performance.ruleId.length <= 128 &&
    typeof performance.ruleVersion === 'string' && performance.ruleVersion.trim() &&
    performance.ruleVersion === performance.ruleVersion.trim() && performance.ruleVersion.length <= 32 &&
    Array.isArray(performance.signalIds) && performance.signalIds.length >= 2 && performance.signalIds.length <= 3 &&
    performance.signalIds.every(id => typeof id === 'string' && UUID_PATTERN.test(id)) &&
    new Set(performance.signalIds.map(id => id.toLowerCase())).size === performance.signalIds.length);
}

function validDeviation(deviation: Member['contextualDeviation']): boolean {
  if (!deviation || typeof deviation !== 'object' || !Array.isArray(deviation.targetSignalIds) ||
      deviation.targetSignalIds.length < 2 || deviation.targetSignalIds.length > 8) return false;
  const ids = [...deviation.targetSignalIds, deviation.firstContextSignalId, deviation.secondContextSignalId];
  return ids.every(id => typeof id === 'string' && UUID_PATTERN.test(id)) &&
    new Set(ids.map(id => id.toLowerCase())).size === ids.length &&
    validRange(deviation.firstContextLowerInclusiveCanonical, deviation.firstContextUpperExclusiveCanonical) &&
    validRange(deviation.secondContextLowerInclusiveCanonical, deviation.secondContextUpperExclusiveCanonical);
}

function validRange(lower: number, upper: number): boolean {
  return Number.isFinite(lower) && Number.isFinite(upper) && lower < upper;
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    performanceMissing: 'Performance is not configured for this equipment. Other available analyses remain usable.',
    deviationMissing: 'Contextual deviation is not configured for this equipment. Other available analyses remain usable.',
    header: 'Process-stage member insights', description: 'Select one configured member, then explicitly open one of its independent read-only analyses.',
    member: 'Equipment assignment', reports: 'Available reports', performance: 'Performance', deviation: 'Contextual deviation',
    chooseReport: 'Choose a report to set its time window and explicitly request the analysis.',
    noAggregate: 'Each report belongs only to the selected Equipment assignment. No process-wide score, timestamp join, diagnosis or efficiency claim is produced.',
    invalid: 'This process-stage analysis has invalid or incomplete stable references.'
  };
  if (locale === 'es') return {
    performanceMissing: 'Rendimiento no configurado para este equipo. Puede usar los otros análisis disponibles.',
    deviationMissing: 'Desviación contextual no configurada para este equipo. Puede usar los otros análisis disponibles.',
    header: 'Análisis de miembros de etapa', description: 'Seleccione un miembro configurado y abra explícitamente uno de sus análisis independientes y de solo lectura.',
    member: 'Asignación de equipo', reports: 'Informes disponibles', performance: 'Rendimiento', deviation: 'Desviación contextual',
    chooseReport: 'Elija un informe para definir su período y solicitar explícitamente el análisis.',
    noAggregate: 'Cada informe pertenece solo a la asignación de equipo seleccionada. No se produce una puntuación de proceso, unión de marcas de tiempo, diagnóstico ni afirmación de eficiencia.',
    invalid: 'Este análisis de etapa contiene referencias estables no válidas o incompletas.'
  };
  return {
    performanceMissing: 'Desempenho não configurado para este equipamento. As outras análises disponíveis continuam acessíveis.',
    deviationMissing: 'Desvio contextual não configurado para este equipamento. As outras análises disponíveis continuam acessíveis.',
    header: 'Análises dos membros da etapa', description: 'Selecione um membro configurado e abra explicitamente uma análise independente e somente leitura.',
    member: 'Equipamento associado', reports: 'Análises disponíveis', performance: 'Desempenho', deviation: 'Desvio contextual',
    chooseReport: 'Escolha uma análise para definir o período e solicitar explicitamente a consulta.',
    noAggregate: 'Cada análise pertence apenas ao Equipamento selecionado. Não há score do processo, cruzamento de timestamps, diagnóstico ou afirmação de eficiência.',
    invalid: 'Esta análise da etapa contém referências estáveis inválidas ou incompletas.'
  };
}
