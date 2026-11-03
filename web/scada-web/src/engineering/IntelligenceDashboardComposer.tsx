import { useState } from 'react';
import type { EngineeringLocale } from './i18n';
import type { TagEngineering } from './types';
import type { IntelligenceDashboardAppearance, IntelligenceMeasurementDraft } from './intelligenceDashboardGenerator';

type Draft = { layout: IntelligenceMeasurementDraft['layout']; minimum: string; maximum: string; target: string; first: string; second: string };
export function IntelligenceDashboardComposer({ locale, tags, disabled, performanceAvailable, deviationAvailable, deviationSignalCount, referenceAvailable = false, contextAvailable = false, jointAvailable = false, currentFrequencyAvailable = false, singleTargetAvailable = false, stage, configurationHref, onGenerate }: {
  locale: EngineeringLocale; tags: readonly TagEngineering[]; disabled: boolean;
  performanceAvailable: boolean; deviationAvailable: boolean; configurationHref?: string;
  deviationSignalCount?: number;
  referenceAvailable?: boolean; contextAvailable?: boolean; jointAvailable?: boolean; currentFrequencyAvailable?: boolean; singleTargetAvailable?: boolean;
  stage?: Readonly<{ coverageAvailable: boolean; members: readonly Readonly<{
    assignmentId: string; label: string; performance: boolean; deviation: boolean; contexts: boolean; joint: boolean; currentFrequency: boolean; singleTarget?: boolean; onConfigure: () => void;
  }>[] }>;

  onGenerate: (appearance: IntelligenceDashboardAppearance, analyses: { performance: boolean; deviation: boolean; stageCoverage: boolean; stageReports: boolean; referenceEvidence: boolean; stageReferenceEvidence: boolean; stageContextEligibility: boolean; stageExperimentalJoint: boolean; stageCurrentFrequency: boolean; currentFrequency: boolean; singleTarget: boolean; stageSingleTarget: boolean; contextEligibility: boolean; experimentalJoint: boolean }) => void;
}) {
  const copy = TEXT[locale];
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [theme, setTheme] = useState<IntelligenceDashboardAppearance['theme']>('dark');
  const [performance, setPerformance] = useState(true);
  const [deviation, setDeviation] = useState(true);
  const [stageCoverage, setStageCoverage] = useState(true);
  const [stageReports, setStageReports] = useState(true);
  const [singleTarget,setSingleTarget]=useState(false);
  const [stageSingleTarget,setStageSingleTarget]=useState(false);
  const stageSingleTargetAvailable=stage?.members.some(member=>member.singleTarget) ?? false;
  const [currentFrequency, setCurrentFrequency] = useState(false);
  const [stageCurrentFrequency, setStageCurrentFrequency] = useState(false);
  const stageCurrentFrequencyAvailable = stage?.members.some(member => member.currentFrequency) ?? false;
  const [experimentalJoint, setExperimentalJoint] = useState(false);
  const [contextEligibility, setContextEligibility] = useState(false);
  const [stageExperimentalJoint, setStageExperimentalJoint] = useState(false);
  const stageJointAvailable = stage?.members.some(member=>member.joint) ?? false;
  const [stageContextEligibility, setStageContextEligibility] = useState(false);
  const stageContextsAvailable = stage?.members.some(member => member.contexts) ?? false;
  const [stageReferenceEvidence, setStageReferenceEvidence] = useState(false);
  const [referenceEvidence, setReferenceEvidence] = useState(false);
  const stageReportsAvailable = stage?.members.some(member => member.performance || member.deviation) ?? false;
  const number = (value: string) => value.trim() && Number.isFinite(Number(value)) ? Number(value) : undefined;
  const rows = tags.filter(tag => tag.id).map(tag => {
    const validScale = Number.isFinite(tag.scaleMinimum) && Number.isFinite(tag.scaleMaximum) && tag.scaleMinimum! < tag.scaleMaximum!;
    const draft = drafts[tag.id!] ?? {
      layout: validScale ? 'measurementBar' : 'value', minimum: validScale ? String(tag.scaleMinimum) : '',
      maximum: validScale ? String(tag.scaleMaximum) : '', target: '', first: '', second: ''
    } as Draft;
    const min = number(draft.minimum), max = number(draft.maximum), target = number(draft.target), first = number(draft.first), second = number(draft.second);
    const valid = draft.layout === 'value' || min !== undefined && max !== undefined && min < max &&
      (draft.layout !== 'measurementTarget' || target !== undefined && target >= min && target <= max) &&
      (draft.layout !== 'measurementBands' || first !== undefined && second !== undefined && min < first && first < second && second < max);
    return { tag, draft, valid, measurement: { layout: draft.layout, minimum: min, maximum: max, target, bandOneEnd: first, bandTwoEnd: second } };
  });
  const change = (id: string, draft: Draft, patch: Partial<Draft>) => setDrafts(current => ({ ...current, [id]: { ...draft, ...patch } }));
  return <div className="asset-process-intelligence__dashboard-composer">
    <h3>{copy.measurements}</h3><p>{copy.measurementsHint}</p>
    <label>{copy.theme}<select value={theme} disabled={disabled} onChange={event => setTheme(event.currentTarget.value as typeof theme)}>
      <option value="dark">{copy.dark}</option><option value="light">{copy.light}</option><option value="high-contrast">{copy.contrast}</option>
    </select></label>
    {rows.map(({ tag, draft, valid }) => <details key={tag.id} className="asset-process-intelligence__analysis" open={draft.layout !== 'value'}>
      <summary>{tag.name}{tag.engineeringUnit ? ` (${tag.engineeringUnit})` : ''} · {copy.layouts[draft.layout]}</summary>
      <div className="asset-process-intelligence__field-grid">
        <label>{copy.layout}<select aria-label={`${copy.layout} · ${tag.name}`} value={draft.layout} disabled={disabled}
          onChange={event => change(tag.id!, draft, { layout: event.currentTarget.value as Draft['layout'] })}>
          {Object.entries(copy.layouts).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></label>
        {draft.layout !== 'value' && <>
          {(['minimum', 'maximum', ...(draft.layout === 'measurementTarget' ? ['target'] : []), ...(draft.layout === 'measurementBands' ? ['first', 'second'] : [])] as const).map(field =>
            <label key={field}>{copy.fields[field as keyof typeof copy.fields]}<input type="number" step="any" value={draft[field as keyof Draft]}
              aria-label={`${copy.fields[field as keyof typeof copy.fields]} · ${tag.name}`} disabled={disabled}
              onChange={event => change(tag.id!, draft, { [field]: event.currentTarget.value })}/></label>)}
        </>}
      </div>
      {!valid && <p role="status">{copy.invalid}</p>}
    </details>)}
    <h3>{copy.analyses}</h3><p>{stage ? copy.stageAnalysesHint : copy.analysesHint}</p>
    <p>{stage ? copy.stageBase : copy.base}</p>
    {stage ? <>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageCoverage && stage.coverageAvailable} disabled={disabled || !stage.coverageAvailable} onChange={e => setStageCoverage(e.currentTarget.checked)}/>{copy.stageCoverage}</label>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageReports && stageReportsAvailable} disabled={disabled || !stageReportsAvailable} onChange={e => setStageReports(e.currentTarget.checked)}/>{copy.stageReports}</label>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageReferenceEvidence && stage.coverageAvailable} disabled={disabled || !stage.coverageAvailable} onChange={e => setStageReferenceEvidence(e.currentTarget.checked)}/>{copy.stageReference}</label>
      <p>{copy.stageReferenceHint}</p>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageContextEligibility && stageContextsAvailable} disabled={disabled || !stageContextsAvailable} onChange={e => setStageContextEligibility(e.currentTarget.checked)}/>{copy.stageContexts}</label>
      <p>{stageContextsAvailable ? copy.stageContextsHint : copy.stageContextsMissing}</p>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageCurrentFrequency && stageCurrentFrequencyAvailable} disabled={disabled || !stageCurrentFrequencyAvailable} onChange={event=>setStageCurrentFrequency(event.currentTarget.checked)}/>{copy.stageCurrentFrequency}</label>
      <p>{stageCurrentFrequencyAvailable ? copy.currentFrequencyHint : copy.currentFrequencyMissing}</p>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageSingleTarget && stageSingleTargetAvailable} disabled={disabled || !stageSingleTargetAvailable} onChange={event=>setStageSingleTarget(event.currentTarget.checked)}/>{copy.stageSingleTarget}</label>
      <p>{stageSingleTargetAvailable ? copy.singleTargetHint : copy.singleTargetMissing}</p>
      <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={stageExperimentalJoint && stageJointAvailable} disabled={disabled || !stageJointAvailable} onChange={e=>setStageExperimentalJoint(e.currentTarget.checked)}/>{copy.stageJoint}</label>
      <p>{stageJointAvailable ? copy.stageJointHint : copy.stageJointMissing}</p>
      <p>{copy.stageHint}</p>
      {!stage.coverageAvailable && <p>{copy.stageCoverageMissing}</p>}
      {stage.members.map(member => <article key={member.assignmentId} className="asset-process-intelligence__row">
        <strong>{member.label}</strong>
        <span>{copy.performance}: {member.performance ? copy.ready : copy.notConfigured} · {copy.deviation}: {member.deviation ? copy.ready : copy.notConfigured} · {copy.contextEligibility}: {member.contexts ? copy.ready : copy.notConfigured} · {copy.joint}: {member.joint ? copy.ready : copy.notConfigured} · {copy.currentFrequency}: {member.currentFrequency ? copy.ready : copy.notConfigured} · {copy.singleTarget}: {member.singleTarget ? copy.ready : copy.notConfigured}</span>
        <button type="button" className="secondary" disabled={disabled} onClick={member.onConfigure}>{copy.configureMember}</button>
      </article>)}
    </> : <>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={referenceEvidence && referenceAvailable} disabled={disabled || !referenceAvailable} onChange={e => setReferenceEvidence(e.currentTarget.checked)}/>{copy.referenceEvidence}</label>
    <p>{copy.referenceHint}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={contextEligibility && contextAvailable} disabled={disabled || !contextAvailable} onChange={e => setContextEligibility(e.currentTarget.checked)}/>{copy.contextEligibility}</label>
    <p>{contextAvailable ? copy.contextHint : copy.contextMissing}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={currentFrequency && currentFrequencyAvailable} disabled={disabled || !currentFrequencyAvailable} onChange={event=>setCurrentFrequency(event.currentTarget.checked)}/>{copy.currentFrequency}</label>
    <p>{currentFrequencyAvailable ? copy.currentFrequencyHint : copy.currentFrequencyMissing}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={singleTarget && singleTargetAvailable} disabled={disabled || !singleTargetAvailable} onChange={event=>setSingleTarget(event.currentTarget.checked)}/>{copy.singleTarget}</label>
    <p>{singleTargetAvailable ? copy.singleTargetHint : copy.singleTargetMissing}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={experimentalJoint && jointAvailable} disabled={disabled || !jointAvailable} onChange={e => setExperimentalJoint(e.currentTarget.checked)}/>{copy.joint}</label>
    <p>{jointAvailable ? copy.jointHint : copy.jointMissing}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={performance && performanceAvailable} disabled={disabled || !performanceAvailable} onChange={e => setPerformance(e.currentTarget.checked)}/>{copy.performance}</label>
    {!performanceAvailable && <p>{copy.performanceMissing}</p>}
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={deviation && deviationAvailable} disabled={disabled || !deviationAvailable} onChange={e => setDeviation(e.currentTarget.checked)}/>{copy.deviation}</label>
    {!deviationAvailable && <p>{deviationSignalCount !== undefined && deviationSignalCount < 4 ? copy.deviationNeedsSignals : copy.deviationMissing}</p>}
    {configurationHref && <a href={configurationHref}>{copy.configure}</a>}
    </>}
    <button type="button" data-testid="intelligence-dashboard-generate" disabled={disabled || !rows.length || rows.some(row => !row.valid)}
      onClick={() => onGenerate({ theme, measurements: Object.fromEntries(rows.map(row => [row.tag.id!, row.measurement])) }, { performance: !stage && performance && performanceAvailable, deviation: !stage && deviation && deviationAvailable, stageCoverage: Boolean(stage && stageCoverage && stage.coverageAvailable), stageReports: Boolean(stage && stageReports && stageReportsAvailable), referenceEvidence: !stage && referenceEvidence && referenceAvailable, contextEligibility: !stage && contextEligibility && contextAvailable, experimentalJoint: !stage && experimentalJoint && jointAvailable, stageReferenceEvidence: Boolean(stage && stageReferenceEvidence && stage.coverageAvailable), stageContextEligibility: Boolean(stage && stageContextEligibility && stageContextsAvailable), stageExperimentalJoint: Boolean(stage && stageExperimentalJoint && stageJointAvailable), singleTarget: !stage && singleTarget && singleTargetAvailable, stageSingleTarget: Boolean(stage && stageSingleTarget && stageSingleTargetAvailable), currentFrequency: !stage && currentFrequency && currentFrequencyAvailable, stageCurrentFrequency: Boolean(stage && stageCurrentFrequency && stageCurrentFrequencyAvailable) })}>{copy.generate}</button>
  </div>;
}

const TEXT = {
  'pt-BR': {
    stageJoint: 'Desvio conjunto experimental por equipamento', stageJointHint: 'Use o seletor da mesma tela de análises para calcular o desvio conjunto de um equipamento por vez. A consulta é experimental, sem diagnóstico ou limiar de alarme.', stageJointMissing: 'Ao menos um equipamento precisa de contexto salvo que deixe duas medições numéricas distintas fora de suas condições. As outras vistas continuam disponíveis.',
    currentFrequency: 'Corrente em frequência semelhante', stageCurrentFrequency: 'Corrente e frequência por equipamento', currentFrequencyHint: 'Acrescente a comparação de corrente usando medições controladas em A e Hz. O operador escolhe períodos e faixa de frequência no Runtime, sem diagnóstico ou afirmação de eficiência.', currentFrequencyMissing: 'O perfil precisa de corrente controlada em ampere e frequência medida em hertz, com significados explícitos. As outras vistas continuam disponíveis.',
    singleTarget: 'Medições sob uma condição', stageSingleTarget: 'Medição sob condição por equipamento', singleTargetHint: 'Adiciona a comparação de uma medição por uma condição com unidade controlada. O operador escolhe faixa e períodos no Runtime.', singleTargetMissing: 'Precisa de 2 a 16 medições numéricas distintas e unidade controlada na condição. As demais vistas continuam disponíveis.',
    joint: 'Desvio conjunto experimental', jointHint: 'Acrescente uma tela para comparar de duas a oito medições sob um contexto salvo. Cadência, períodos e consulta são definidos no Runtime. Validado por simulação, sem diagnóstico ou limiar de alarme.', jointMissing: 'Salve um contexto que deixe pelo menos duas medições numéricas distintas disponíveis para comparação. As outras vistas continuam disponíveis.',
    contextEligibility: 'Condições de operação', contextHint: 'Acrescente uma tela para conferir quais leituras atendem aos contextos salvos. No Runtime, escolha o contexto pelo nome e consulte um período.', contextMissing: 'Salve um contexto operacional desta associação para acrescentar a vista de condições. As outras vistas continuam disponíveis.',
    stageContexts: 'Condições de operação por equipamento', stageContextsHint: 'Use a mesma tela de análises para escolher um equipamento e consultar seus contextos salvos. Equipamentos sem contexto mantêm as outras análises disponíveis.', stageContextsMissing: 'Salve um contexto na associação de pelo menos um equipamento desta etapa para incluir as condições. As outras vistas continuam disponíveis.',
    stageReference: 'Dados da referência por equipamento', stageReferenceHint: 'Acrescente a conferência da referência à tela de análises da etapa. Você escolhe um equipamento de cada vez e consulta os períodos desejados. Não exige configurar desempenho ou desvio.',
    referenceEvidence: 'Conferir dados da referência', referenceHint: 'Acrescente uma tela para escolher um período conhecido e conferir as leituras disponíveis. Isso ajuda a avaliar os dados antes de uma comparação; não confirma que o equipamento estava saudável.',
    stageBase: 'Sempre incluído: medições e tendência. Abaixo, escolha se quer acrescentar cobertura e análises dos equipamentos.',
    stageAnalysesHint: 'O painel pode ter até três telas ligadas por botões: medições, cobertura e análises. Na tela de análises, escolha o equipamento e o relatório que quer consultar.',
    stageCoverageMissing: 'As vistas por equipamento precisam de uma etapa com 1 a 32 associações completas e distintas. A visão básica de medições continua disponível.',
    stageCoverage: 'Cobertura por equipamento', stageReports: 'Análises por equipamento', ready: 'Disponível', notConfigured: 'Não configurado', configureMember: 'Configurar este equipamento',
    stageHint: 'Cada equipamento mantém suas próprias análises. A vista reúne as opções configuradas; uma opção ausente não bloqueia as outras nem a cobertura. Use Configurar este equipamento para escolher suas regras e sinais antes de gerar.',
    deviationNeedsSignals: 'Este desvio precisa de pelo menos quatro medições distintas: duas para acompanhar e duas para definir as condições de comparação. As outras vistas disponíveis podem ser geradas sem ele.',
    measurements: 'Objetos de medição', measurementsHint: 'Escolha a apresentação de cada medição. Escalas usam a unidade do TAG e podem vir do cadastro. Informe os limites reais; faixas e metas não são inferidas. Sem escala, use Valor numérico.',
    theme: 'Tema dos objetos', dark: 'Escuro', light: 'Claro', contrast: 'Alto contraste', layout: 'Objeto',
    layouts: { value: 'Valor numérico', measurementDial: 'Gauge semicircular', measurementRing: 'Anel circular', measurementBar: 'Barra horizontal', measurementThermometer: 'Régua vertical', measurementBands: 'Escala por faixas', measurementTarget: 'Valor e meta' },
    fields: { minimum: 'Mínimo', maximum: 'Máximo', target: 'Meta', first: 'Fim da primeira faixa', second: 'Fim da segunda faixa' }, invalid: 'Confira os limites: mínimo < máximo; faixas em ordem dentro da escala; meta dentro dos limites.',
    analyses: 'Conteúdo analítico do painel', analysesHint: 'Cada análise configurada pode gerar sua própria Tela, com navegação entre as vistas. Selecione as que deseja incluir. As consultas continuam sob demanda no Runtime.', base: 'Incluído: medições e tendência. A cobertura do perfil é incluída quando suas referências estão completas.',
    performance: 'Desempenho', performanceMissing: 'Desempenho ainda não configurado para esta origem. Escolha uma regra elegível na associação do equipamento.', deviation: 'Desvio contextual', deviationMissing: 'Desvio contextual ainda não configurado para esta origem. Escolha os sinais e as duas faixas de contexto na associação do equipamento.', configure: 'Configurar análises da associação', generate: 'Gerar painel e vistas selecionadas'
  },
  en: {
    stageJoint: 'Experimental joint deviation per equipment', stageJointHint: 'Use the same analyses screen selector to calculate joint deviation for one equipment at a time. This is experimental, without diagnosis or an alarm threshold.', stageJointMissing: 'At least one equipment needs a saved context leaving two distinct numeric measurements outside its conditions. Other views remain available.',
    currentFrequency: 'Current at similar frequency', stageCurrentFrequency: 'Current and frequency per equipment', currentFrequencyHint: 'Add current comparison using controlled measurements in A and Hz. The operator chooses periods and frequency range in Runtime, without diagnosis or an efficiency claim.', currentFrequencyMissing: 'The profile needs controlled current in amperes and measured frequency in hertz, with explicit meanings. Other views remain available.',
    singleTarget: 'Measurements under one condition', stageSingleTarget: 'Measurement under a condition per equipment', singleTargetHint: 'Adds comparison of one measurement under a controlled-unit condition. The operator chooses range and periods in Runtime.', singleTargetMissing: 'Needs 2 to 16 distinct numeric measurements and a controlled condition unit. Other views remain available.',
    joint: 'Experimental joint deviation', jointHint: 'Add a screen to compare two to eight measurements under a saved context. Cadence, periods and query are set in Runtime. Validated by simulation, without diagnosis or an alarm threshold.', jointMissing: 'Save a context that leaves at least two distinct numeric measurements available for comparison. Other views remain available.',
    contextEligibility: 'Operating conditions', contextHint: 'Add a screen to check which readings meet saved context conditions. In Runtime, choose a context by name and query a period.', contextMissing: 'Save an operating context for this assignment to add the conditions view. Other views remain available.',
    stageContexts: 'Operating conditions per equipment', stageContextsHint: 'Use the same analyses screen to choose equipment and query its saved contexts. Equipment without a context retains its other available analyses.', stageContextsMissing: 'Save a context in at least one equipment assignment in this stage to include conditions. The other views remain available.',
    stageReference: 'Reference data per equipment', stageReferenceHint: 'Add reference-data checks to the stage analyses screen. Choose one equipment at a time and query the desired periods. Performance or deviation configuration is not required.',
    referenceEvidence: 'Check reference data', referenceHint: 'Add a screen to choose a known period and check the available readings. This helps review the data before a comparison; it does not confirm the equipment was healthy.',
    stageBase: 'Always included: measurements and trend. Below, choose whether to add equipment coverage and analyses.',
    stageAnalysesHint: 'The dashboard can contain up to three screens linked by buttons: measurements, coverage and analyses. On the analyses screen, choose the equipment and report to query.',
    stageCoverageMissing: 'Member views need a stage with 1 to 32 complete, distinct assignments. The basic measurement view remains available.',
    stageCoverage: 'Member coverage', stageReports: 'Member analyses', ready: 'Available', notConfigured: 'Not configured', configureMember: 'Configure this equipment',
    stageHint: 'Each equipment keeps its own analyses. The view lists configured options; a missing option does not block other analyses or coverage. Use Configure this equipment to choose its rules and signals before generating.',
    deviationNeedsSignals: 'This deviation needs at least four distinct measurements: two to monitor and two to define comparison conditions. Other available views can be generated without it.',
    measurements: 'Measurement objects', measurementsHint: 'Choose how to display each measurement. Scales use the TAG unit and may come from its settings. Enter actual limits; bands and targets are not inferred. Use Numeric value without a scale.',
    theme: 'Object theme', dark: 'Dark', light: 'Light', contrast: 'High contrast', layout: 'Object',
    layouts: { value: 'Numeric value', measurementDial: 'Semicircular dial', measurementRing: 'Circular ring', measurementBar: 'Horizontal bar', measurementThermometer: 'Vertical ruler', measurementBands: 'Segmented bands', measurementTarget: 'Value and target' },
    fields: { minimum: 'Minimum', maximum: 'Maximum', target: 'Target', first: 'First band end', second: 'Second band end' }, invalid: 'Check limits: minimum < maximum; ordered bands inside the scale; target within limits.',
    analyses: 'Dashboard analyses', analysesHint: 'Each configured analysis can generate its own linked Screen. Select what to include. Runtime queries remain explicit.', base: 'Included: measurements and trend. Profile coverage is included when its references are complete.',
    performance: 'Performance', performanceMissing: 'Performance is not configured for this source yet. Choose an eligible rule in the equipment assignment.', deviation: 'Contextual deviation', deviationMissing: 'Contextual deviation is not configured for this source yet. Choose signals and both context ranges in the equipment assignment.', configure: 'Configure assignment analyses', generate: 'Generate dashboard and selected views'
  },
  es: {
    stageJoint: 'Desviación conjunta experimental por equipo', stageJointHint: 'Use el selector de la misma pantalla de análisis para calcular desviación conjunta de un equipo a la vez. Es experimental, sin diagnóstico ni umbral de alarma.', stageJointMissing: 'Al menos un equipo necesita contexto guardado que deje dos mediciones numéricas distintas fuera de sus condiciones. Las otras vistas siguen disponibles.',
    currentFrequency: 'Corriente a frecuencia similar', stageCurrentFrequency: 'Corriente y frecuencia por equipo', currentFrequencyHint: 'Agregue la comparación de corriente con mediciones controladas en A y Hz. El operador elige períodos y rango en Runtime, sin diagnóstico ni afirmación de eficiencia.', currentFrequencyMissing: 'El perfil necesita corriente controlada en amperios y frecuencia medida en hercios, con significados explícitos. Las otras vistas siguen disponibles.',
    singleTarget: 'Mediciones bajo una condición', stageSingleTarget: 'Medición bajo condición por equipo', singleTargetHint: 'Agrega comparación de una medición bajo una condición de unidad controlada. El operador elige rango y períodos en Runtime.', singleTargetMissing: 'Requiere de 2 a 16 mediciones numéricas distintas y unidad controlada en la condición. Las demás vistas siguen disponibles.',
    joint: 'Desviación conjunta experimental', jointHint: 'Agregue una pantalla para comparar de dos a ocho mediciones bajo un contexto guardado. Cadencia, períodos y consulta se definen en Runtime. Validado por simulación, sin diagnóstico ni umbral de alarma.', jointMissing: 'Guarde un contexto que deje al menos dos mediciones numéricas distintas para comparar. Las otras vistas siguen disponibles.',
    contextEligibility: 'Condiciones de operación', contextHint: 'Agregue una pantalla para revisar qué lecturas cumplen los contextos guardados. En Runtime, elija el contexto por nombre y consulte un período.', contextMissing: 'Guarde un contexto operativo de esta asignación para agregar la vista de condiciones. Las otras vistas siguen disponibles.',
    stageContexts: 'Condiciones de operación por equipo', stageContextsHint: 'Use la misma pantalla de análisis para elegir un equipo y consultar sus contextos guardados. Los equipos sin contexto conservan sus otros análisis disponibles.', stageContextsMissing: 'Guarde un contexto en al menos una asignación de equipo de esta etapa para incluir condiciones. Las otras vistas siguen disponibles.',
    stageReference: 'Datos de referencia por equipo', stageReferenceHint: 'Agregue la revisión de referencia a la pantalla de análisis de etapa. Elija un equipo cada vez y consulte los períodos deseados. No requiere configurar rendimiento o desviación.',
    referenceEvidence: 'Revisar datos de referencia', referenceHint: 'Agregue una pantalla para elegir un período conocido y revisar las lecturas disponibles. Ayuda a evaluar los datos antes de comparar; no confirma que el equipo estuviera sano.',
    stageBase: 'Siempre incluido: mediciones y tendencia. Abajo, elija si desea agregar cobertura y análisis de los equipos.',
    stageAnalysesHint: 'El panel puede tener hasta tres pantallas enlazadas por botones: mediciones, cobertura y análisis. En la pantalla de análisis, elija el equipo y el informe que desea consultar.',
    stageCoverageMissing: 'Las vistas por equipo necesitan una etapa con 1 a 32 asignaciones completas y distintas. La vista básica de mediciones sigue disponible.',
    stageCoverage: 'Cobertura por equipo', stageReports: 'Análisis por equipo', ready: 'Disponible', notConfigured: 'No configurado', configureMember: 'Configurar este equipo',
    stageHint: 'Cada equipo conserva sus propios análisis. La vista reúne las opciones configuradas; una opción ausente no bloquea las otras ni la cobertura. Use Configurar este equipo para elegir sus reglas y señales antes de generar.',
    deviationNeedsSignals: 'Esta desviación necesita al menos cuatro mediciones distintas: dos para observar y dos para definir condiciones de comparación. Las otras vistas disponibles se pueden generar sin ella.',
    measurements: 'Objetos de medición', measurementsHint: 'Elija cómo mostrar cada medición. Las escalas usan la unidad del TAG y pueden venir de su configuración. Indique límites reales; no se infieren intervalos ni metas. Sin escala use Valor numérico.',
    theme: 'Tema de los objetos', dark: 'Oscuro', light: 'Claro', contrast: 'Alto contraste', layout: 'Objeto',
    layouts: { value: 'Valor numérico', measurementDial: 'Dial semicircular', measurementRing: 'Anillo circular', measurementBar: 'Barra horizontal', measurementThermometer: 'Regla vertical', measurementBands: 'Escala por intervalos', measurementTarget: 'Valor y meta' },
    fields: { minimum: 'Mínimo', maximum: 'Máximo', target: 'Meta', first: 'Fin del primer intervalo', second: 'Fin del segundo intervalo' }, invalid: 'Revise los límites: mínimo < máximo; intervalos ordenados dentro de la escala; meta dentro de los límites.',
    analyses: 'Análisis del panel', analysesHint: 'Cada análisis configurado puede generar su propia Pantalla vinculada. Elija qué incluir. Las consultas de Runtime siguen siendo explícitas.', base: 'Incluido: mediciones y tendencia. Se incluye cobertura del perfil cuando sus referencias están completas.',
    performance: 'Rendimiento', performanceMissing: 'Rendimiento aún no configurado para esta fuente. Elija una regla elegible en la asignación del equipo.', deviation: 'Desviación contextual', deviationMissing: 'Desviación contextual aún no configurada. Elija señales y ambos intervalos de contexto en la asignación del equipo.', configure: 'Configurar análisis de la asignación', generate: 'Generar panel y vistas seleccionadas'
  }
} as const;
