import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import type { EngineeringLocale } from '../i18n';
import { IntelligenceProfileCoverageVisualElement } from './IntelligenceProfileCoverageVisualElement';
import './IntelligenceProcessStageCoverageVisualElement.css';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type IntelligenceProcessStageCoverageMember = Readonly<{
  assignmentId: string;
  scopeId: string;
  profileId: string;
  displayLabel: string;
  order: number;
}>;

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

export function IntelligenceProcessStageCoverageVisualElement({
  stageId,
  stageKey,
  stageName,
  membersJson,
  style,
  runtimeObjectId,
  locale,
  title
}: Props) {
  const members = useMemo(() => parseMembers(membersJson), [membersJson]);
  const [selectedAssignmentId, setSelectedAssignmentId] = useState('');
  const selected = members.find(member => member.assignmentId === selectedAssignmentId) ?? members[0] ?? null;
  const copy = text(locale);
  const configValid = UUID_PATTERN.test(stageId) &&
    stageKey.length > 0 && stageKey === stageKey.trim() && stageKey.length <= 64 &&
    stageName.length > 0 && stageName === stageName.trim() && stageName.length <= 128 &&
    members.length > 0 && members.length <= 32;

  useEffect(() => {
    if (!members.some(member => member.assignmentId === selectedAssignmentId))
      setSelectedAssignmentId(members[0]?.assignmentId ?? '');
  }, [membersJson, selectedAssignmentId, members]);

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

  return <section
    className="visual-editor-object runtime-intelligence-process-stage-coverage"
    style={rootStyle}
    data-object-id={runtimeObjectId}
    data-runtime-object-id={runtimeObjectId}
    data-testid="runtime-intelligence-process-stage-coverage"
    data-stage-id={stageId}
    title={title}
  >
    <header className="runtime-intelligence-coverage__header">
      <strong>{title || `${copy.header} · ${stageName}`}</strong>
      <span>{copy.description}</span>
    </header>
    {!configValid ? <p className="runtime-intelligence-coverage__notice" role="alert">{copy.invalid}</p>
      : <>
        <label className="runtime-intelligence-process-stage-coverage__member">
          {copy.member}
          <select
            data-testid="runtime-intelligence-process-stage-member"
            value={selected?.assignmentId ?? ''}
            onChange={event => setSelectedAssignmentId(event.currentTarget.value)}
          >
            {members.map(member => <option key={member.assignmentId} value={member.assignmentId}>
              {member.order + 1}. {member.displayLabel}
            </option>)}
          </select>
        </label>
        {selected ? <IntelligenceProfileCoverageVisualElement
          key={selected.assignmentId}
          scopeId={selected.scopeId}
          profileId={selected.profileId}
          requiredAssignmentId={selected.assignmentId}
          style={{ width: '100%', minHeight: 300 }}
          runtimeObjectId={runtimeObjectId ? `${runtimeObjectId}:${selected.assignmentId}` : undefined}
          locale={locale}
          title={selected.displayLabel}
        /> : null}
        <p className="runtime-intelligence-coverage__notice">{copy.noAggregate}</p>
      </>}
  </section>;
}

function parseMembers(value: string): IntelligenceProcessStageCoverageMember[] {
  if (typeof value !== 'string' || value.length > 32768) return [];
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed) || parsed.length < 1 || parsed.length > 32) return [];
    const members = parsed.map((item): IntelligenceProcessStageCoverageMember | null => {
      if (!item || typeof item !== 'object') return null;
      const member = item as Partial<IntelligenceProcessStageCoverageMember>;
      if (typeof member.assignmentId !== 'string' || !UUID_PATTERN.test(member.assignmentId) ||
          typeof member.scopeId !== 'string' || !UUID_PATTERN.test(member.scopeId) ||
          typeof member.profileId !== 'string' || !UUID_PATTERN.test(member.profileId) ||
          typeof member.displayLabel !== 'string' || !member.displayLabel.trim() ||
          member.displayLabel !== member.displayLabel.trim() || member.displayLabel.length > 320 ||
          typeof member.order !== 'number' || !Number.isInteger(member.order) || member.order < 0) return null;
      return member as IntelligenceProcessStageCoverageMember;
    });
    if (members.some(member => member === null)) return [];
    const valid = members as IntelligenceProcessStageCoverageMember[];
    if (new Set(valid.map(member => member.assignmentId.toLowerCase())).size !== valid.length ||
        new Set(valid.map(member => `${member.scopeId.toLowerCase()}:${member.profileId.toLowerCase()}`)).size !== valid.length ||
        new Set(valid.map(member => member.order)).size !== valid.length) return [];
    return [...valid].sort((left, right) => left.order - right.order || left.assignmentId.localeCompare(right.assignmentId));
  } catch {
    return [];
  }
}

function text(locale: EngineeringLocale) {
  if (locale === 'en') return {
    header: 'Process-stage coverage', member: 'Equipment assignment',
    description: 'Select one configured member and explicitly request its read-only Historian coverage report.',
    noAggregate: 'Each result belongs only to the selected Equipment profile. No process KPI, cross-equipment score or combined timestamp overlap is calculated.',
    invalid: 'This process-stage report has invalid or incomplete stable references.'
  };
  if (locale === 'es') return {
    header: 'Cobertura de etapa del proceso', member: 'Asignación de equipo',
    description: 'Seleccione un miembro configurado y solicite explícitamente su informe de cobertura de Historian, de solo lectura.',
    noAggregate: 'Cada resultado pertenece solo al perfil de equipo seleccionado. No se calcula un KPI de proceso, una puntuación entre equipos ni una superposición combinada de marcas de tiempo.',
    invalid: 'Este informe de etapa del proceso tiene referencias estables no válidas o incompletas.'
  };
  return {
    header: 'Cobertura da etapa do processo', member: 'Equipamento associado',
    description: 'Selecione um membro configurado e solicite explicitamente seu relatório de cobertura do Historian, somente leitura.',
    noAggregate: 'Cada resultado pertence apenas ao perfil do Equipamento selecionado. Nenhum KPI de processo, score entre equipamentos ou cruzamento combinado de timestamps é calculado.',
    invalid: 'Este relatório da etapa contém referências estáveis inválidas ou incompletas.'
  };
}
