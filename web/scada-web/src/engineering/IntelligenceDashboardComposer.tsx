import { useState } from 'react';
import type { EngineeringLocale } from './i18n';
import type { TagEngineering } from './types';
import type { IntelligenceDashboardAppearance, IntelligenceMeasurementDraft } from './intelligenceDashboardGenerator';

type Draft = { layout: IntelligenceMeasurementDraft['layout']; minimum: string; maximum: string; target: string; first: string; second: string };
export function IntelligenceDashboardComposer({ locale, tags, disabled, performanceAvailable, deviationAvailable, configurationHref, onGenerate }: {
  locale: EngineeringLocale; tags: readonly TagEngineering[]; disabled: boolean;
  performanceAvailable: boolean; deviationAvailable: boolean; configurationHref?: string;
  onGenerate: (appearance: IntelligenceDashboardAppearance, analyses: { performance: boolean; deviation: boolean }) => void;
}) {
  const copy = TEXT[locale];
  const [drafts, setDrafts] = useState<Record<string, Draft>>({});
  const [theme, setTheme] = useState<IntelligenceDashboardAppearance['theme']>('dark');
  const [performance, setPerformance] = useState(true);
  const [deviation, setDeviation] = useState(true);
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
    <h3>{copy.analyses}</h3><p>{copy.analysesHint}</p>
    <p>{copy.base}</p>
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={performance && performanceAvailable} disabled={disabled || !performanceAvailable} onChange={e => setPerformance(e.currentTarget.checked)}/>{copy.performance}</label>
    {!performanceAvailable && <p>{copy.performanceMissing}</p>}
    <label className="asset-process-intelligence__checkbox"><input type="checkbox" checked={deviation && deviationAvailable} disabled={disabled || !deviationAvailable} onChange={e => setDeviation(e.currentTarget.checked)}/>{copy.deviation}</label>
    {!deviationAvailable && <p>{copy.deviationMissing}</p>}
    {configurationHref && <a href={configurationHref}>{copy.configure}</a>}
    <p>{copy.otherAnalyses}</p>
    <button type="button" data-testid="intelligence-dashboard-generate" disabled={disabled || !rows.length || rows.some(row => !row.valid)}
      onClick={() => onGenerate({ theme, measurements: Object.fromEntries(rows.map(row => [row.tag.id!, row.measurement])) }, { performance: performance && performanceAvailable, deviation: deviation && deviationAvailable })}>{copy.generate}</button>
  </div>;
}

const TEXT = {
  'pt-BR': {
    otherAnalyses: 'As consultas de referência, condições do contexto e desvio conjunto experimental continuam na associação, em Engenharia. Elas ainda não geram uma vista própria no Runtime.',
    measurements: 'Objetos de medição', measurementsHint: 'Escolha a apresentação de cada medição. Escalas usam a unidade do TAG e podem vir do cadastro. Informe os limites reais; faixas e metas não são inferidas. Sem escala, use Valor numérico.',
    theme: 'Tema dos objetos', dark: 'Escuro', light: 'Claro', contrast: 'Alto contraste', layout: 'Objeto',
    layouts: { value: 'Valor numérico', measurementDial: 'Gauge semicircular', measurementRing: 'Anel circular', measurementBar: 'Barra horizontal', measurementThermometer: 'Régua vertical', measurementBands: 'Escala por faixas', measurementTarget: 'Valor e meta' },
    fields: { minimum: 'Mínimo', maximum: 'Máximo', target: 'Meta', first: 'Fim da primeira faixa', second: 'Fim da segunda faixa' }, invalid: 'Confira os limites: mínimo < máximo; faixas em ordem dentro da escala; meta dentro dos limites.',
    analyses: 'Conteúdo analítico do painel', analysesHint: 'Cada análise configurada pode gerar sua própria Tela, com navegação entre as vistas. Selecione as que deseja incluir. As consultas continuam sob demanda no Runtime.', base: 'Incluído: medições e tendência. A cobertura do perfil é incluída quando suas referências estão completas.',
    performance: 'Desempenho', performanceMissing: 'Desempenho ainda não configurado para esta origem. Escolha uma regra elegível na associação do equipamento.', deviation: 'Desvio contextual', deviationMissing: 'Desvio contextual ainda não configurado para esta origem. Escolha os sinais e as duas faixas de contexto na associação do equipamento.', configure: 'Configurar análises da associação', generate: 'Gerar painel e vistas selecionadas'
  },
  en: {
    otherAnalyses: 'Reference, context-condition and experimental joint-deviation queries remain in the Engineering assignment. They do not yet generate their own Runtime view.',
    measurements: 'Measurement objects', measurementsHint: 'Choose how to display each measurement. Scales use the TAG unit and may come from its settings. Enter actual limits; bands and targets are not inferred. Use Numeric value without a scale.',
    theme: 'Object theme', dark: 'Dark', light: 'Light', contrast: 'High contrast', layout: 'Object',
    layouts: { value: 'Numeric value', measurementDial: 'Semicircular dial', measurementRing: 'Circular ring', measurementBar: 'Horizontal bar', measurementThermometer: 'Vertical ruler', measurementBands: 'Segmented bands', measurementTarget: 'Value and target' },
    fields: { minimum: 'Minimum', maximum: 'Maximum', target: 'Target', first: 'First band end', second: 'Second band end' }, invalid: 'Check limits: minimum < maximum; ordered bands inside the scale; target within limits.',
    analyses: 'Dashboard analyses', analysesHint: 'Each configured analysis can generate its own linked Screen. Select what to include. Runtime queries remain explicit.', base: 'Included: measurements and trend. Profile coverage is included when its references are complete.',
    performance: 'Performance', performanceMissing: 'Performance is not configured for this source yet. Choose an eligible rule in the equipment assignment.', deviation: 'Contextual deviation', deviationMissing: 'Contextual deviation is not configured for this source yet. Choose signals and both context ranges in the equipment assignment.', configure: 'Configure assignment analyses', generate: 'Generate dashboard and selected views'
  },
  es: {
    otherAnalyses: 'Las consultas de referencia, condiciones del contexto y desviación conjunta experimental siguen en la asignación de Ingeniería. Todavía no generan una vista propia en Runtime.',
    measurements: 'Objetos de medición', measurementsHint: 'Elija cómo mostrar cada medición. Las escalas usan la unidad del TAG y pueden venir de su configuración. Indique límites reales; no se infieren intervalos ni metas. Sin escala use Valor numérico.',
    theme: 'Tema de los objetos', dark: 'Oscuro', light: 'Claro', contrast: 'Alto contraste', layout: 'Objeto',
    layouts: { value: 'Valor numérico', measurementDial: 'Dial semicircular', measurementRing: 'Anillo circular', measurementBar: 'Barra horizontal', measurementThermometer: 'Regla vertical', measurementBands: 'Escala por intervalos', measurementTarget: 'Valor y meta' },
    fields: { minimum: 'Mínimo', maximum: 'Máximo', target: 'Meta', first: 'Fin del primer intervalo', second: 'Fin del segundo intervalo' }, invalid: 'Revise los límites: mínimo < máximo; intervalos ordenados dentro de la escala; meta dentro de los límites.',
    analyses: 'Análisis del panel', analysesHint: 'Cada análisis configurado puede generar su propia Pantalla vinculada. Elija qué incluir. Las consultas de Runtime siguen siendo explícitas.', base: 'Incluido: mediciones y tendencia. Se incluye cobertura del perfil cuando sus referencias están completas.',
    performance: 'Rendimiento', performanceMissing: 'Rendimiento aún no configurado para esta fuente. Elija una regla elegible en la asignación del equipo.', deviation: 'Desviación contextual', deviationMissing: 'Desviación contextual aún no configurada. Elija señales y ambos intervalos de contexto en la asignación del equipo.', configure: 'Configurar análisis de la asignación', generate: 'Generar panel y vistas seleccionadas'
  }
} as const;
