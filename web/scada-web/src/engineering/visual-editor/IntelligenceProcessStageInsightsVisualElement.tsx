import { readSingleTargetSignals } from '../intelligenceSingleTargetReferences';
import { IntelligenceSingleTargetVisualElement } from './IntelligenceSingleTargetVisualElement';
import { IntelligenceCurrentFrequencyVisualElement } from './IntelligenceCurrentFrequencyVisualElement';
import { readCurrentFrequencySignals } from '../intelligenceCurrentFrequencySignals';
import { useEffect, useMemo, useState } from 'react';
import type { CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import type { IntelligenceProcessStageReportMember as Member } from '../intelligenceDashboardGenerator';
import { IntelligenceContextualDeviationVisualElement } from './IntelligenceContextualDeviationVisualElement';
import { IntelligencePerformanceVisualElement } from './IntelligencePerformanceVisualElement';
import { IntelligenceReferenceEvidenceVisualElement } from './IntelligenceReferenceEvidenceVisualElement';
import { IntelligenceContextEligibilityVisualElement } from './IntelligenceContextEligibilityVisualElement';
import { readIntelligenceContextReferences } from '../intelligenceContextReferences';
import { IntelligenceExperimentalJointVisualElement } from './IntelligenceExperimentalJointVisualElement';
import { readIntelligenceJointPresentation } from '../intelligenceJointReferences';
import './IntelligenceProcessStageInsightsVisualElement.css';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type ReportKind = 'single-target' | 'current-frequency' | 'performance' | 'contextual-deviation' | 'reference-evidence' | 'context-eligibility' | 'experimental-joint' | null;

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
  const activeReport = report === 'single-target' && selected?.singleTarget ? report : report === 'current-frequency' && selected?.currentFrequency ? report : report === 'performance' && selected?.performance ? report
    : report === 'contextual-deviation' && selected?.contextualDeviation ? report
      : report === 'reference-evidence' && selected?.referenceEvidence ? report
        : report === 'context-eligibility' && selected?.operatingContexts ? report
          : report === 'experimental-joint' && selected?.experimentalJoint ? report : null;
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
    position: 'relative', left: 0, top: 0, width: '100%', height: 'auto', minHeight: 560,
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
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-current-frequency"
          disabled={!selected?.currentFrequency} aria-pressed={activeReport === 'current-frequency'} onClick={()=>setReport('current-frequency')}>{copy.currentFrequency}</button>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-single-target"
          disabled={!selected?.singleTarget} aria-pressed={activeReport === 'single-target'} onClick={()=>setReport('single-target')}>{copy.singleTarget}</button>
        {selected?.referenceEvidence && <button type="button" data-testid="runtime-intelligence-process-stage-insights-reference"
          aria-pressed={activeReport === 'reference-evidence'} onClick={() => setReport('reference-evidence')}>{copy.reference}</button>}
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-joint"
          disabled={!selected?.experimentalJoint} aria-pressed={activeReport === 'experimental-joint'} onClick={()=>setReport('experimental-joint')}>{copy.joint}</button>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-context"
          disabled={!selected?.operatingContexts} aria-pressed={activeReport === 'context-eligibility'} onClick={() => setReport('context-eligibility')}>{copy.conditions}</button>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-performance"
          disabled={!selected?.performance} aria-pressed={activeReport === 'performance'} onClick={() => setReport('performance')}>{copy.performance}</button>
        <button type="button" data-testid="runtime-intelligence-process-stage-insights-deviation"
          disabled={!selected?.contextualDeviation} aria-pressed={activeReport === 'contextual-deviation'} onClick={() => setReport('contextual-deviation')}>{copy.deviation}</button>
      </div>
      {!selected?.currentFrequency && <p>{copy.currentFrequencyMissing}</p>}
      {!selected?.singleTarget && <p>{copy.singleTargetMissing}</p>}
      {!selected?.experimentalJoint && <p>{copy.jointMissing}</p>}
      {!selected?.operatingContexts && <p>{copy.conditionsMissing}</p>}
      {!selected?.performance && <p>{copy.performanceMissing}</p>}
      {!selected?.contextualDeviation && <p>{copy.deviationMissing}</p>}
      {!activeReport || !selected ? <p className="runtime-intelligence-process-stage-insights__hint">{copy.chooseReport}</p>
        : <div className="runtime-intelligence-process-stage-insights__report">{activeReport === 'single-target' && selected.singleTarget ? <IntelligenceSingleTargetVisualElement
          key={`single-target:${selected.assignmentId}`} scopeId={selected.scopeId} assignmentId={selected.assignmentId} profileId={selected.profileId}
          signalsJson={JSON.stringify(selected.singleTarget)} style={reportStyle} locale={locale}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:single-target` : undefined} title={selected.displayLabel}/>
        : activeReport === 'current-frequency' && selected.currentFrequency ? <IntelligenceCurrentFrequencyVisualElement
          key={`current-frequency:${selected.assignmentId}`} scopeId={selected.scopeId} assignmentId={selected.assignmentId} profileId={selected.profileId}
          signalsJson={JSON.stringify(selected.currentFrequency)} style={reportStyle}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:current-frequency` : undefined} locale={locale} title={selected.displayLabel}/>
        : activeReport === 'experimental-joint' && selected.experimentalJoint ? <IntelligenceExperimentalJointVisualElement
          key={`joint:${selected.assignmentId}`} scopeId={selected.scopeId} assignmentId={selected.assignmentId} profileId={selected.profileId}
          contextsJson={JSON.stringify(selected.experimentalJoint.contexts)} signalsJson={JSON.stringify(selected.experimentalJoint.signals)} style={reportStyle}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:joint` : undefined} locale={locale} title={selected.displayLabel}/>
        : activeReport === 'context-eligibility' && selected.operatingContexts ? <IntelligenceContextEligibilityVisualElement
          key={`conditions:${selected.assignmentId}`} scopeId={selected.scopeId} assignmentId={selected.assignmentId} profileId={selected.profileId}
          contextsJson={JSON.stringify(selected.operatingContexts)} style={reportStyle}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:conditions` : undefined}
          locale={locale} title={selected.displayLabel}/>
        : activeReport === 'reference-evidence' ? <IntelligenceReferenceEvidenceVisualElement
          key={`reference:${selected.assignmentId}`} scopeId={selected.scopeId} profileId={selected.profileId}
          style={reportStyle} runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}:reference` : undefined}
          locale={locale} title={selected.displayLabel}/>
        : activeReport === 'performance' && selected.performance ? <IntelligencePerformanceVisualElement
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
          (member.referenceEvidence !== undefined && (!partial || member.referenceEvidence !== true)) ||
          (member.operatingContexts !== undefined && (!partial || !readIntelligenceContextReferences(JSON.stringify(member.operatingContexts)))) ||
          (member.singleTarget !== undefined && (!partial || !readSingleTargetSignals(JSON.stringify(member.singleTarget)))) ||
          (member.currentFrequency !== undefined && (!partial || !readCurrentFrequencySignals(JSON.stringify(member.currentFrequency)))) ||
          (member.experimentalJoint !== undefined && (!partial || !readIntelligenceJointPresentation(member.experimentalJoint))) ||
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
    currentFrequency: 'Current at similar frequency', currentFrequencyMissing: 'Current/frequency comparison is not included for this equipment. It needs controlled current in A and measured frequency in Hz.',
    singleTarget: 'Compare one measurement under a condition', singleTargetMissing: 'This comparison is not included for this equipment. It needs two distinct numeric measurements and a controlled condition unit.',
    joint: 'Joint deviation (experimental)', jointMissing: 'Joint deviation is not included for this equipment. It needs a saved context and two distinct numeric measurements outside its conditions.', reference: 'Reference data', conditions: 'Operating conditions', conditionsMissing: 'This equipment has no saved context in this view. Generate and publish it again after saving a context in Engineering.',
    performanceMissing: 'Performance is not configured for this equipment. Other available analyses remain usable.',
    deviationMissing: 'Contextual deviation is not configured for this equipment. Other available analyses remain usable.',
    header: 'Process-stage member insights', description: 'Select one configured member, then explicitly open one of its independent read-only analyses.',
    member: 'Equipment assignment', reports: 'Available reports', performance: 'Performance', deviation: 'Contextual deviation',
    chooseReport: 'Choose a report to set its time window and explicitly request the analysis.',
    noAggregate: 'Each report belongs only to the selected Equipment assignment. No process-wide score, timestamp join, diagnosis or efficiency claim is produced.',
    invalid: 'This process-stage analysis has invalid or incomplete stable references.'
  };
  if (locale === 'es') return {
    currentFrequency: 'Corriente a frecuencia similar', currentFrequencyMissing: 'No se incluyó comparación de corriente/frecuencia para este equipo. Necesita corriente controlada en A y frecuencia medida en Hz.',
    singleTarget: 'Comparar una medición bajo una condición', singleTargetMissing: 'Esta comparación no está incluida para este equipo. Requiere dos mediciones numéricas distintas y unidad controlada en la condición.',
    joint: 'Desviación conjunta (experimental)', jointMissing: 'Desviación conjunta no incluida para este equipo. Necesita contexto guardado y dos mediciones numéricas distintas fuera de sus condiciones.', reference: 'Datos de referencia', conditions: 'Condiciones de operación', conditionsMissing: 'Este equipo no tiene un contexto guardado en esta vista. Genere y publique de nuevo después de guardar un contexto en Ingeniería.',
    performanceMissing: 'Rendimiento no configurado para este equipo. Puede usar los otros análisis disponibles.',
    deviationMissing: 'Desviación contextual no configurada para este equipo. Puede usar los otros análisis disponibles.',
    header: 'Análisis de miembros de etapa', description: 'Seleccione un miembro configurado y abra explícitamente uno de sus análisis independientes y de solo lectura.',
    member: 'Asignación de equipo', reports: 'Informes disponibles', performance: 'Rendimiento', deviation: 'Desviación contextual',
    chooseReport: 'Elija un informe para definir su período y solicitar explícitamente el análisis.',
    noAggregate: 'Cada informe pertenece solo a la asignación de equipo seleccionada. No se produce una puntuación de proceso, unión de marcas de tiempo, diagnóstico ni afirmación de eficiencia.',
    invalid: 'Este análisis de etapa contiene referencias estables no válidas o incompletas.'
  };
  return {
    currentFrequency: 'Corrente em frequência semelhante', currentFrequencyMissing: 'Comparação corrente/frequência não incluída para este equipamento. Precisa de corrente controlada em A e frequência medida em Hz.',
    singleTarget: 'Comparar uma medição sob uma condição', singleTargetMissing: 'Esta comparação não foi incluída para este equipamento. Precisa de duas medições numéricas distintas e unidade controlada na condição.',
    joint: 'Desvio conjunto (experimental)', jointMissing: 'Desvio conjunto não incluído para este equipamento. Precisa de contexto salvo e duas medições numéricas distintas fora de suas condições.', reference: 'Dados da referência', conditions: 'Condições de operação', conditionsMissing: 'Este equipamento não tem um contexto salvo nesta vista. Gere e publique novamente depois de salvar um contexto na Engenharia.',
    performanceMissing: 'Desempenho não configurado para este equipamento. As outras análises disponíveis continuam acessíveis.',
    deviationMissing: 'Desvio contextual não configurado para este equipamento. As outras análises disponíveis continuam acessíveis.',
    header: 'Análises dos membros da etapa', description: 'Selecione um membro configurado e abra explicitamente uma análise independente e somente leitura.',
    member: 'Equipamento associado', reports: 'Análises disponíveis', performance: 'Desempenho', deviation: 'Desvio contextual',
    chooseReport: 'Escolha uma análise para definir o período e solicitar explicitamente a consulta.',
    noAggregate: 'Cada análise pertence apenas ao Equipamento selecionado. Não há score do processo, cruzamento de timestamps, diagnóstico ou afirmação de eficiência.',
    invalid: 'Esta análise da etapa contém referências estáveis inválidas ou incompletas.'
  };
}
