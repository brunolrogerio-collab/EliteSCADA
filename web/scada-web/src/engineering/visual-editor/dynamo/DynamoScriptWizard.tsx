import React, { useEffect, useMemo, useState } from 'react';
import { useAppShellLocale } from '../../../appShellI18n';
import { useDynamoAuthoringCatalog } from '../DynamoAuthoringCatalogContext';
import { loadScriptEngineeringContext, previewScriptMutation, applyScriptMutation, validateServerScriptPython } from '../../scripts/scriptEngineeringApi';
import { createNewScriptDefinition } from '../../scripts/ScriptEngineeringWorkspace.logic';
import type { ScriptEngineeringContext, ScriptEngineeringDefinition, ScriptMutationPreviewToken, ScriptVisualEventReference } from '../../scripts/scriptEngineeringTypes';
import type { DynamoParameterDefinitionEngineering, DynamoParameterValueEngineering } from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';
import type { VisualEditorBindingSourceCatalogItem } from '../visualEditorContracts';
import { resolveDynamoValueSourceType } from './dynamoPublicInterfaceModel';

export function DynamoScriptWizard({ screenId, objectId, parameter, sources, disabled, onSet }: {
  screenId?: string | null; objectId: string; parameter: DynamoParameterDefinitionEngineering;
  sources: readonly VisualEditorBindingSourceCatalogItem[]; disabled: boolean;
  onSet: (value: DynamoParameterValueEngineering) => void;
}) {
  const locale = useAppShellLocale();
  const label = (pt: string, en: string, es: string) => locale === 'pt-BR' ? pt : locale === 'es' ? es : en;
  const catalog = useDynamoAuthoringCatalog();
  const [context, setContext] = useState<ScriptEngineeringContext | null>(null);
  const [expanded, setExpanded] = useState(false);
  const [scriptId, setScriptId] = useState('');
  const [outputId, setOutputId] = useState('');
  const [tagId, setTagId] = useState('');
  const [expression, setExpression] = useState('bool(source)');
  const [event, setEvent] = useState<'timer' | 'objectInteraction'>('timer');
  const [interval, setInterval] = useState(1000);
  const [handler, setHandler] = useState('');
  const [newDefinition] = useState(createNewScriptDefinition);
  const [token, setToken] = useState<ScriptMutationPreviewToken | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const outputs = sources.filter(source => source.family === 'clientMemory' && source.tagReference?.tagId && resolveDynamoValueSourceType(source.dataType) &&
    (!parameter.valueSourceType || resolveDynamoValueSourceType(source.dataType) === parameter.valueSourceType));
  const output = outputs.find(source => source.tagReference?.tagId === outputId);
  const valueType = resolveDynamoValueSourceType(output?.dataType);
  const selected = context?.scripts.find(script => script.id === scriptId && script.scope === 'clientVisual');
  const sourceCode = useMemo(() => `from elite_scada import tag_read, client_memory_write\n\nasync def dynamo_signal(context):\n    sample = await tag_read(${JSON.stringify(tagId)})\n    if sample["quality"] not in (0, "Good", "good") or sample["value"] is None:\n        return\n    source = sample["value"]\n    result = ${expression.trim() || 'bool(source)'}\n    await client_memory_write(${JSON.stringify(outputId)}, ${valueType === 'Number' ? 'float(result)' : 'bool(result)'})\n`, [tagId, outputId, expression, valueType]);
  const signature = JSON.stringify({ scriptId, outputId, tagId, expression, event, interval, handler });
  useEffect(() => { setToken(null); }, [signature]);
  useEffect(() => {
    if (!expanded || context) return;
    let alive = true;
    void loadScriptEngineeringContext().then(value => { if (alive) setContext(value); }).catch(error => { if (alive) setMessage(String(error)); });
    return () => { alive = false; };
  }, [expanded, context]);

  const preview = async () => {
    if (!context || !screenId || !output || !valueType || disabled) return;
    setBusy(true); setMessage('');
    try {
      const entryPoint = selected ? handler : 'dynamo_signal';
      const script: ScriptEngineeringDefinition = selected ? { ...selected, metadata: { ...selected.metadata, [`dynamo.output.${parameter.key}`]: outputId } } : {
        ...newDefinition, path: `scripts/dynamo-${newDefinition.id}.py`, name: `Dynamo ${parameter.key}`, source: sourceCode,
        entryPoints: [{ eventKind: event, handlerName: entryPoint, timerIntervalMs: event === 'timer' ? interval : null }],
        dependencies: [{ kind: 'tag', stableReference: tagId }, { kind: 'clientMemoryTag', stableReference: outputId }],
        metadata: { [`dynamo.output.${parameter.key}`]: outputId }
      };
      const reference: ScriptVisualEventReference = { visualDefinitionId: screenId, visualObjectId: objectId,
        eventKind: event, eventKey: event === 'objectInteraction' ? 'click' : null, scriptId: script.id,
        entryPoint, timerIntervalMs: event === 'timer' ? interval : null };
      const references = [...context.visualEventReferences.filter(item => !(item.visualDefinitionId === screenId && item.visualObjectId === objectId && item.scriptId === script.id && item.eventKind === event)), reference];
      const syntax = await validateServerScriptPython(script.source);
      const errors = syntax.diagnostics.filter(item => item.severity === 'error');
      if (errors.length) throw new Error(errors.map(item => `${item.line}: ${item.message}`).join('\n'));
      const next = await previewScriptMutation(script, references, selected ? 'UpdateExisting' : 'CreateOnly');
      setToken(next); setMessage(next.preview.canApply ? label('Preview válido. Aplicar grava apenas o script e sua associação no Working.', 'Valid preview. Apply saves only the script and its Working association.', 'Preview válido. Aplicar guarda solo el script y su asociación en Working.') : JSON.stringify(next.preview.items.flatMap(item => item.issues).filter(issue => issue.isError)));
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };
  const apply = async () => {
    if (!token?.preview.canApply) return;
    setBusy(true);
    try {
      const result = await applyScriptMutation(token);
      if (result.issues?.some(issue => issue.isError)) throw new Error(JSON.stringify(result.issues));
      setToken(null); setContext(await loadScriptEngineeringContext());
      await catalog.onApplied?.();
      setMessage(label('Script aplicado. Vincule a saída e salve a tela; depois publique/ative.', 'Script applied. Bind the output and save the screen, then publish/activate.', 'Script aplicado. Vincule la salida y guarde la pantalla; publique/active.'));
    } catch (error) { setToken(null); setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };
  return <details data-testid="dynamo-script-wizard" onToggle={event => setExpanded(event.currentTarget.open)}><summary>{label('Script e saída de animação', 'Script and animation output', 'Script y salida de animación')}</summary>
    <p>{label('Saída explícita em Client Memory. Scripts não escrevem diretamente em parâmetros privados do dínamo.', 'Explicit Client Memory output. Scripts do not write private Dynamo parameters.', 'Salida explícita en Client Memory; no modifica parámetros privados del dínamo.')}</p>
    <label>{label('Script', 'Script', 'Script')}<select value={scriptId} disabled={disabled || busy} onChange={e => { setScriptId(e.target.value); setHandler(''); const script = context?.scripts.find(item => item.id === e.target.value); setOutputId(script?.metadata[`dynamo.output.${parameter.key}`] ?? ''); }}>
      <option value="">{label('Criar com wizard', 'Create with wizard', 'Crear con asistente')}</option>{context?.scripts.filter(script => script.scope === 'clientVisual').map(script => <option key={script.id} value={script.id}>{script.name}</option>)}
    </select></label>
    <label>{label('Saída', 'Output', 'Salida')}<select value={outputId} disabled={disabled || busy} onChange={e => setOutputId(e.target.value)}><option value="">—</option>{outputs.map(source => <option key={source.tagReference!.tagId} value={source.tagReference!.tagId}>{source.label} · {source.dataType}</option>)}</select></label>
    <label>{label('Gatilho', 'Trigger', 'Disparador')}<select value={event} disabled={disabled || busy} onChange={e => { setEvent(e.target.value as typeof event); setHandler(''); }}><option value="timer">Timer</option><option value="objectInteraction">Click</option></select></label>
    {event === 'timer' && <input aria-label="Timer ms" type="number" min={50} value={interval} onChange={e => setInterval(Number(e.target.value))}/>}
    {selected ? <select aria-label="Handler" value={handler} onChange={e => setHandler(e.target.value)}><option value="">—</option>{selected.entryPoints.filter(entry => entry.eventKind === event).map(entry => <option key={entry.handlerName} value={entry.handlerName}>{entry.handlerName}</option>)}</select> : <>
      <select aria-label="Source TAG" value={tagId} onChange={e => setTagId(e.target.value)}><option value="">TAG</option>{catalog.tags.filter(tag => tag.id).map(tag => <option key={tag.id!} value={tag.id!}>{tag.name}</option>)}</select>
      <input aria-label="Python expression" value={expression} onChange={e => setExpression(e.target.value)}/><pre>{sourceCode}</pre>
    </>}
    {selected && <p>{label('O handler escolhido deve escrever na saída Client Memory indicada. Esta associação não altera o código de um script existente.', 'The selected handler must write the indicated Client Memory output. This association does not rewrite existing script code.', 'El handler debe escribir la salida Client Memory indicada; no se modifica el código existente.')}</p>}
    {!screenId && <p>{label('Salve a definição visual antes de associar scripts.', 'Save the visual definition before associating scripts.', 'Guarde la definición antes de asociar scripts.')}</p>}
    <button type="button" disabled={disabled || busy || !screenId || !output || (selected ? !handler : !tagId)} onClick={() => void preview()}>{label('Validar script', 'Validate script', 'Validar script')}</button>
    <button type="button" disabled={disabled || busy || !token?.preview.canApply} onClick={() => void apply()}>{label('Aplicar script', 'Apply script', 'Aplicar script')}</button>
    <button type="button" disabled={disabled || !output || !valueType} onClick={() => onSet({ key: parameter.key, kind: 'ValueSource', version: parameter.version, valueSource: { kind: 'ClientMemory', valueType: valueType!, tagReference: output!.tagReference, target: output!.target } })}>{label('Vincular saída ao parâmetro', 'Bind output to parameter', 'Vincular salida al parámetro')}</button>
    {message && <p role="status">{message}</p>}
  </details>;
}
