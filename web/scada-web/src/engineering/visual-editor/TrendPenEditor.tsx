import React from 'react';
import type { TagEngineering, VisualElementEngineering } from '../types';
import {
  createTrendPen,
  readTrendPens,
  TREND_PENS_PROPERTY,
  trendPensEngineeringValue,
  type TrendVisualPen
} from '../../visual-runtime';
import { useDynamoAuthoringCatalog } from './DynamoAuthoringCatalogContext';
import { c07VisualEditorText, useC07VisualEditorText } from './c07VisualEditorI18n';
import { rebindTrendPenToTag } from './trendAuthoringModel';
import type { VisualEditorMutationIntent } from './visualEditorContracts';

const COPY = {
  'pt-BR': { title: 'Pens do Trend', hint: 'Configure TAG, apresentação, eixo e escala de cada Pen.', add: 'Adicionar Pen', empty: 'Nenhuma Pen configurada.', tag: 'TAG', visible: 'Visível', remove: 'Remover', label: 'Rótulo', unit: 'Unidade', color: 'Cor', width: 'Espessura', style: 'Estilo', axis: 'Eixo', scale: 'Escala', solid: 'Sólida', dashed: 'Tracejada', dotted: 'Pontilhada', left: 'Esquerdo', right: 'Direito', auto: 'Automática', fixed: 'Fixa', minimum: 'Mínimo', maximum: 'Máximo', identityRequired: 'Salve o objeto Trend antes de editar suas Pens.', noTags: 'Não há TAGs canônicas disponíveis neste projeto.' },
  en: { title: 'Trend Pens', hint: 'Configure each Pen TAG, presentation, axis and scale.', add: 'Add Pen', empty: 'No Pens configured.', tag: 'TAG', visible: 'Visible', remove: 'Remove', label: 'Label', unit: 'Unit', color: 'Color', width: 'Width', style: 'Style', axis: 'Axis', scale: 'Scale', solid: 'Solid', dashed: 'Dashed', dotted: 'Dotted', left: 'Left', right: 'Right', auto: 'Auto', fixed: 'Fixed', minimum: 'Minimum', maximum: 'Maximum', identityRequired: 'Save the Trend object before editing its Pens.', noTags: 'No canonical TAGs are available in this project.' },
  es: { title: 'Pens del Trend', hint: 'Configure TAG, presentación, eje y escala de cada Pen.', add: 'Agregar Pen', empty: 'No hay Pens configuradas.', tag: 'TAG', visible: 'Visible', remove: 'Eliminar', label: 'Etiqueta', unit: 'Unidad', color: 'Color', width: 'Espesor', style: 'Estilo', axis: 'Eje', scale: 'Escala', solid: 'Sólida', dashed: 'Discontinua', dotted: 'Punteada', left: 'Izquierdo', right: 'Derecho', auto: 'Automática', fixed: 'Fija', minimum: 'Mínimo', maximum: 'Máximo', identityRequired: 'Guarde el objeto Trend antes de editar sus Pens.', noTags: 'No hay TAGs canónicas disponibles en este proyecto.' }
} as const;

export function TrendPenEditor({ element, onMutationIntent }: Readonly<{
  element: VisualElementEngineering;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}>) {
  const { tags } = useDynamoAuthoringCatalog();
  const currentText = useC07VisualEditorText();
  const locale = currentText === c07VisualEditorText('en') ? 'en' : currentText === c07VisualEditorText('es') ? 'es' : 'pt-BR';
  const text = COPY[locale];
  const [actionError, setActionError] = React.useState<string | null>(null);
  let pens: readonly TrendVisualPen[] = Object.freeze([]);
  let parseError: string | null = null;
  try { pens = readTrendPens(element); } catch (reason) { parseError = reason instanceof Error ? reason.message : String(reason); }
  const objectId = element.id?.trim() ?? '';
  const selectableTags = React.useMemo(
    () => tags.filter((tag): tag is TagEngineering & { id: string } => Boolean(tag.id?.trim())),
    [tags]
  );

  const commit = (next: readonly TrendVisualPen[]) => {
    if (!objectId) { setActionError(text.identityRequired); return; }
    setActionError(null);
    onMutationIntent(Object.freeze({
      kind: 'property.set',
      objectIds: Object.freeze([objectId]),
      propertyKey: TREND_PENS_PROPERTY,
      value: trendPensEngineeringValue(next)
    }));
  };

  const addPen = () => {
    const used = new Set(pens.map(pen => pen.tagId));
    const tag = selectableTags.find(candidate => !used.has(candidate.id)) ?? selectableTags[0];
    if (!tag) { setActionError(text.noTags); return; }
    commit([...pens, createTrendPen({ id: tag.id, path: tag.path, label: tag.name, unit: tag.engineeringUnit }, pens.length)]);
  };

  const updatePen = (index: number, patch: Partial<TrendVisualPen>) => {
    commit(pens.map((pen, candidate) => candidate === index ? Object.freeze({ ...pen, ...patch }) : pen));
  };

  const selectTag = (index: number, tagId: string) => {
    const tag = selectableTags.find(candidate => candidate.id === tagId);
    if (!tag) return;
    const previous = pens[index];
    const previousTag = selectableTags.find(candidate => candidate.id === previous.tagId);
    updatePen(index, rebindTrendPenToTag(previous, previousTag, tag));
  };

  return <section data-testid="trend-pen-editor" style={sectionStyle}>
    <div style={headingStyle}>
      <div><strong>{text.title}</strong><div style={hintStyle}>{text.hint}</div></div>
      <button type="button" onClick={addPen} disabled={!objectId || pens.length >= 16 || Boolean(parseError)}>{text.add}</button>
    </div>
    {pens.length === 0 && !parseError ? <p style={hintStyle}>{text.empty}</p> : pens.map((pen, index) => <div key={pen.id} data-pen-id={pen.id} style={cardStyle}>
      <div style={rowStyle}>
        <label style={wideStyle}>{text.tag}<select value={pen.tagId} onChange={event => selectTag(index, event.target.value)} style={inputStyle}>
          {!selectableTags.some(tag => tag.id === pen.tagId) ? <option value={pen.tagId}>{pen.tagPath}</option> : null}
          {selectableTags.map(tag => <option key={tag.id} value={tag.id}>{tag.path}</option>)}
        </select></label>
        <label>{text.visible}<input type="checkbox" checked={pen.visible} onChange={event => updatePen(index, { visible: event.target.checked })} /></label>
        <button type="button" onClick={() => commit(pens.filter((_, candidate) => candidate !== index))}>{text.remove}</button>
      </div>
      <div style={rowStyle}>
        <label style={wideStyle}>{text.label}<input value={pen.label} onChange={event => updatePen(index, { label: event.target.value })} style={inputStyle} /></label>
        <label>{text.unit}<input value={pen.unit} onChange={event => updatePen(index, { unit: event.target.value })} style={smallInputStyle} /></label>
        <label>{text.color}<input type="color" value={pen.color.slice(0, 7)} onChange={event => updatePen(index, { color: event.target.value.toUpperCase() })} /></label>
      </div>
      <div style={rowStyle}>
        <label>{text.width}<input type="number" min="1" max="12" step="0.5" value={pen.lineWidth} onChange={event => updatePen(index, { lineWidth: Number(event.target.value) })} style={smallInputStyle} /></label>
        <label>{text.style}<select value={pen.lineStyle} onChange={event => updatePen(index, { lineStyle: event.target.value as TrendVisualPen['lineStyle'] })} style={smallInputStyle}><option value="solid">{text.solid}</option><option value="dashed">{text.dashed}</option><option value="dotted">{text.dotted}</option></select></label>
        <label>{text.axis}<select value={pen.axis} onChange={event => updatePen(index, { axis: event.target.value as TrendVisualPen['axis'] })} style={smallInputStyle}><option value="left">{text.left}</option><option value="right">{text.right}</option></select></label>
        <label>{text.scale}<select value={pen.scale.mode} onChange={event => updatePen(index, { scale: event.target.value === 'fixed' ? Object.freeze({ mode: 'fixed', minimum: 0, maximum: 100 }) : Object.freeze({ mode: 'auto' }) })} style={smallInputStyle}><option value="auto">{text.auto}</option><option value="fixed">{text.fixed}</option></select></label>
      </div>
      {pen.scale.mode === 'fixed' ? <div style={rowStyle}>
        <label>{text.minimum}<input type="number" value={pen.scale.minimum} onChange={event => updatePen(index, { scale: Object.freeze({ ...pen.scale, minimum: Number(event.target.value) }) })} style={smallInputStyle} /></label>
        <label>{text.maximum}<input type="number" value={pen.scale.maximum} onChange={event => updatePen(index, { scale: Object.freeze({ ...pen.scale, maximum: Number(event.target.value) }) })} style={smallInputStyle} /></label>
      </div> : null}
    </div>)}
    {parseError || actionError ? <p role="alert" style={errorStyle}>{parseError ?? actionError}</p> : null}
  </section>;
}

const sectionStyle: React.CSSProperties = { borderTop: '1px solid #334155', paddingTop: 10, marginTop: 10 };
const headingStyle: React.CSSProperties = { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 8 };
const hintStyle: React.CSSProperties = { fontSize: 11, opacity: 0.75, margin: '2px 0 6px' };
const cardStyle: React.CSSProperties = { border: '1px solid #334155', borderRadius: 4, padding: 8, marginTop: 8 };
const rowStyle: React.CSSProperties = { display: 'flex', alignItems: 'end', gap: 8, flexWrap: 'wrap', marginTop: 6 };
const wideStyle: React.CSSProperties = { flex: '1 1 220px' };
const inputStyle: React.CSSProperties = { display: 'block', width: '100%' };
const smallInputStyle: React.CSSProperties = { display: 'block', width: 90 };
const errorStyle: React.CSSProperties = { color: '#FCA5A5', fontSize: 12 };
