import { useEffect, useMemo, useState } from 'react';
import type {
  AssetProcessIntelligenceProfileEngineering,
  TagEngineering
} from './types';
import type { EngineeringLocale } from './i18n';
import type { VersionedControlledUnitCatalog } from './semanticCatalogApi';
import { isNumericTagDataType, type IntelligenceContextualDeviationDashboardSelection } from './intelligenceDashboardGenerator';
import './AssetProcessIntelligenceContextualDeviationPanel.css';

type ResolvedSignal = Readonly<{
  id: string;
  signalRole: string;
  tag: TagEngineering & { id: string };
  canonicalUnit: string | null;
  inputUnit: string | null;
  scaleToCanonical: number | null;
}>;

export function AssetProcessIntelligenceContextualDeviationPanel({
  locale,
  scopeId,
  assignmentId,
  profile,
  tags,
  controlledUnits,
  displayName,
  canCreate,
  onCreateRuntimeScreenDraft,
  onGuidedSelectionChange
}: {
  locale: EngineeringLocale;
  scopeId: string;
  assignmentId: string;
  profile: AssetProcessIntelligenceProfileEngineering;
  tags: readonly TagEngineering[];
  controlledUnits: readonly VersionedControlledUnitCatalog[];
  displayName: string;
  canCreate: boolean;
  onCreateRuntimeScreenDraft?: (selection: IntelligenceContextualDeviationDashboardSelection) => void;
  onGuidedSelectionChange?: (assignmentId: string, selection: IntelligenceContextualDeviationDashboardSelection | null) => void;
}) {
  const copy = useMemo(() => panelCopy(locale), [locale]);
  const [targetIds, setTargetIds] = useState<string[]>([]);
  const [firstContextId, setFirstContextId] = useState('');
  const [secondContextId, setSecondContextId] = useState('');
  const [firstLower, setFirstLower] = useState('');
  const [firstUpper, setFirstUpper] = useState('');
  const [secondLower, setSecondLower] = useState('');
  const [secondUpper, setSecondUpper] = useState('');

  const tagById = useMemo(() => new Map(tags
    .filter((tag): tag is TagEngineering & { id: string } => Boolean(tag.id?.trim()))
    .map(tag => [tag.id.toLowerCase(), tag])), [tags]);
  const signalOptions = useMemo(() => profile.signals.flatMap(signal => {
    const tag = tagById.get(signal.tagId.toLowerCase());
    if (!tag || !isNumericTagDataType(tag.dataType)) return [];
    const unitRef = tag.controlledEngineeringUnit;
    const unit = unitRef ? controlledUnits.find(catalog => catalog.catalogVersion === unitRef.catalogVersion)
      ?.units.find(candidate => candidate.code === unitRef.code) : undefined;
    const supported = unit && Number.isFinite(unit.scaleToCanonical) && unit.scaleToCanonical > 0;
    const tagUnit = tag.engineeringUnit?.trim();
    return [{
      id: signal.id,
      signalRole: signal.signalRole,
      tag,
      canonicalUnit: supported ? unit.canonicalUnit : null,
      inputUnit: supported ? tagUnit || unit.canonicalUnit : null,
      scaleToCanonical: supported ? tagUnit ? unit.scaleToCanonical : 1 : null
    } satisfies ResolvedSignal];
  }), [controlledUnits, profile.signals, tagById]);
  const contextOptions = useMemo(() => signalOptions.filter(signal => Boolean(signal.canonicalUnit)), [signalOptions]);

  useEffect(() => {
    setTargetIds([]);
    setFirstContextId('');
    setSecondContextId('');
    setFirstLower('');
    setFirstUpper('');
    setSecondLower('');
    setSecondUpper('');
  }, [assignmentId, profile.id]);

  const firstScale = contextOptions.find(signal => signal.id === firstContextId)?.scaleToCanonical;
  const secondScale = contextOptions.find(signal => signal.id === secondContextId)?.scaleToCanonical;
  const firstLowerValue = toCanonicalLimit(firstLower, firstScale);
  const firstUpperValue = toCanonicalLimit(firstUpper, firstScale);
  const secondLowerValue = toCanonicalLimit(secondLower, secondScale);
  const secondUpperValue = toCanonicalLimit(secondUpper, secondScale);
  const selectedContextIds = [firstContextId, secondContextId].filter(Boolean);
  const selectedSignalIds = [...targetIds, ...selectedContextIds];
  const selectedTagIds = selectedSignalIds.map(id => signalOptions.find(signal => signal.id === id)?.tag.id.toLowerCase() ?? '');
  const selectionValid = targetIds.length >= 2 && targetIds.length <= 8 &&
    new Set(targetIds).size === targetIds.length &&
    selectedTagIds.every(Boolean) && new Set(selectedTagIds).size === selectedTagIds.length &&
    Boolean(firstContextId && secondContextId && firstContextId !== secondContextId) &&
    targetIds.every(id => !selectedContextIds.includes(id)) &&
    firstLowerValue !== null && firstUpperValue !== null && firstLowerValue < firstUpperValue &&
    secondLowerValue !== null && secondUpperValue !== null && secondLowerValue < secondUpperValue;

  useEffect(() => {
    if (!selectionValid || firstLowerValue === null || firstUpperValue === null ||
        secondLowerValue === null || secondUpperValue === null) {
      onGuidedSelectionChange?.(assignmentId, null);
      return;
    }
    onGuidedSelectionChange?.(assignmentId, {
      scopeId,
      assignmentId,
      profileId: profile.id,
      targetSignalIds: targetIds,
      firstContextSignalId: firstContextId,
      firstContextLowerInclusiveCanonical: firstLowerValue,
      firstContextUpperExclusiveCanonical: firstUpperValue,
      secondContextSignalId: secondContextId,
      secondContextLowerInclusiveCanonical: secondLowerValue,
      secondContextUpperExclusiveCanonical: secondUpperValue,
      displayName
    });
  }, [assignmentId, displayName, firstContextId, firstLowerValue, firstUpperValue, onGuidedSelectionChange,
    profile.id, scopeId, secondContextId, secondLowerValue, secondUpperValue, selectionValid, targetIds]);

  const submit = () => {
    if (!selectionValid || !onCreateRuntimeScreenDraft || firstLowerValue === null || firstUpperValue === null ||
        secondLowerValue === null || secondUpperValue === null) return;
    onCreateRuntimeScreenDraft({
      scopeId,
      assignmentId,
      profileId: profile.id,
      targetSignalIds: targetIds,
      firstContextSignalId: firstContextId,
      firstContextLowerInclusiveCanonical: firstLowerValue,
      firstContextUpperExclusiveCanonical: firstUpperValue,
      secondContextSignalId: secondContextId,
      secondContextLowerInclusiveCanonical: secondLowerValue,
      secondContextUpperExclusiveCanonical: secondUpperValue,
      displayName
    });
  };

  const toggleTarget = (signalId: string, checked: boolean) => {
    setTargetIds(current => {
      const next = checked
        ? current.length < 8 && !current.includes(signalId) ? [...current, signalId] : current
        : current.filter(id => id !== signalId);
      return next;
    });
    if (checked && firstContextId === signalId) setFirstContextId('');
    if (checked && secondContextId === signalId) setSecondContextId('');
  };

  const firstOptions = contextOptions.filter(signal => !targetIds.includes(signal.id) && signal.id !== secondContextId);
  const secondOptions = contextOptions.filter(signal => !targetIds.includes(signal.id) && signal.id !== firstContextId);

  return <details className="asset-process-intelligence__deviation-panel" data-testid={`intelligence-contextual-deviation-${assignmentId}`}>
    <summary>{copy.open}</summary>
    <div className="asset-process-intelligence__deviation-body">
      <p>{copy.hint}</p>
      {signalOptions.length < 4 ? <p role="note">{copy.notEnoughSignals}</p> : null}
      {contextOptions.length < 2 ? <p role="note">{copy.contextUnitsRequired}</p> : null}
      <fieldset>
        <legend>{copy.targets}</legend>
        <p>{copy.targetsHint}</p>
        <div className="asset-process-intelligence__deviation-targets">
          {signalOptions.map(signal => <label key={signal.id}>
            <input type="checkbox" checked={targetIds.includes(signal.id)} disabled={!canCreate || (!targetIds.includes(signal.id) && targetIds.length >= 8)}
              onChange={event => toggleTarget(signal.id, event.currentTarget.checked)}/>
            <span><strong>{signal.signalRole || signal.tag.path}</strong><small>{signal.tag.path}{signal.tag.engineeringUnit ? ` · ${signal.tag.engineeringUnit}` : ''}</small></span>
          </label>)}
          {signalOptions.length === 0 ? <span>{copy.noNumericSignals}</span> : null}
        </div>
      </fieldset>
      <div className="asset-process-intelligence__deviation-contexts">
        <ContextRangeEditor
          label={copy.firstContext}
          signalId={firstContextId}
          options={firstOptions}
          lower={firstLower}
          upper={firstUpper}
          lowerLabel={copy.lowerInclusive}
          upperLabel={copy.upperExclusive}
          chooseLabel={copy.chooseSignal}
          onSignalChange={id => { setFirstContextId(id); setFirstLower(''); setFirstUpper(''); }}
          onLowerChange={setFirstLower}
          onUpperChange={setFirstUpper}
          disabled={!canCreate}
        />
        <ContextRangeEditor
          label={copy.secondContext}
          signalId={secondContextId}
          options={secondOptions}
          lower={secondLower}
          upper={secondUpper}
          lowerLabel={copy.lowerInclusive}
          upperLabel={copy.upperExclusive}
          chooseLabel={copy.chooseSignal}
          onSignalChange={id => { setSecondContextId(id); setSecondLower(''); setSecondUpper(''); }}
          onLowerChange={setSecondLower}
          onUpperChange={setSecondUpper}
          disabled={!canCreate}
        />
      </div>
      <p className="asset-process-intelligence__deviation-note">{copy.canonicalHint}</p>
      <button type="button" className="secondary" disabled={!canCreate || !selectionValid || !onCreateRuntimeScreenDraft} onClick={submit}>
        {copy.createScreen}
      </button>
    </div>
  </details>;
}

function ContextRangeEditor({
  label, signalId, options, lower, upper, lowerLabel, upperLabel, chooseLabel,
  onSignalChange, onLowerChange, onUpperChange, disabled
}: {
  label: string;
  signalId: string;
  options: readonly ResolvedSignal[];
  lower: string;
  upper: string;
  lowerLabel: string;
  upperLabel: string;
  chooseLabel: string;
  onSignalChange: (value: string) => void;
  onLowerChange: (value: string) => void;
  onUpperChange: (value: string) => void;
  disabled: boolean;
}) {
  const selected = options.find(signal => signal.id === signalId);
  return <fieldset>
    <legend>{label}</legend>
    <label>{chooseLabel}<select value={signalId} disabled={disabled} onChange={event => onSignalChange(event.currentTarget.value)}>
      <option value="">—</option>
      {options.map(signal => <option key={signal.id} value={signal.id}>{signal.signalRole || signal.tag.path} · {signal.inputUnit}</option>)}
    </select></label>
    <label>{lowerLabel} ({selected?.inputUnit ?? '—'})<input type="text" inputMode="decimal" value={lower} disabled={disabled || !selected} onChange={event => onLowerChange(event.currentTarget.value)}/></label>
    <label>{upperLabel} ({selected?.inputUnit ?? '—'})<input type="text" inputMode="decimal" value={upper} disabled={disabled || !selected} onChange={event => onUpperChange(event.currentTarget.value)}/></label>
  </fieldset>;
}

function toCanonicalLimit(value: string, scale: number | null | undefined): number | null {
  if (scale === null || scale === undefined || !Number.isFinite(scale) || scale <= 0) return null;
  const normalized = value.trim();
  if (!/^-?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?$/.test(normalized)) return null;
  const parsed = Number(normalized.replace(',', '.')) * scale;
  return Number.isFinite(parsed) ? parsed : null;
}

function panelCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    open: 'Create contextual deviation Runtime screen',
    hint: 'Select at least two target signals and two distinct numeric context signals. This only creates a Screen draft; it does not query Historian.',
    notEnoughSignals: 'This profile has fewer than four usable numeric signals.', contextUnitsRequired: 'Context signals need a controlled engineering unit for automatic limit conversion.',
    targets: 'Target signals (2–8)', targetsHint: 'Targets must be distinct from each other and from both context signals.', noNumericSignals: 'No numeric profile signals resolve to current TAGs.',
    firstContext: 'First context signal', secondContext: 'Second context signal', chooseSignal: 'Numeric context TAG',
    lowerInclusive: 'Minimum (included)', upperExclusive: 'Maximum (not included)',
    canonicalHint: 'Enter limits in the unit shown beside each field; SCADIVO converts them internally. The minimum is included and the maximum is not: minimum ≤ reading < maximum. These ranges are separate from the saved operating-context profile.',
    createScreen: 'Create Runtime screen draft'
  };
  if (locale === 'es') return {
    open: 'Crear pantalla Runtime de desviación contextual',
    hint: 'Seleccione al menos dos señales objetivo y dos señales numéricas de contexto distintas. Solo crea un borrador de Screen; no consulta Historian.',
    notEnoughSignals: 'El perfil tiene menos de cuatro señales numéricas utilizables.', contextUnitsRequired: 'Las señales de contexto necesitan una unidad controlada para convertir los límites automáticamente.',
    targets: 'Señales objetivo (2–8)', targetsHint: 'Los objetivos deben diferir entre sí y de ambas señales de contexto.', noNumericSignals: 'Ninguna señal numérica del perfil resuelve a TAGs actuales.',
    firstContext: 'Primera señal de contexto', secondContext: 'Segunda señal de contexto', chooseSignal: 'TAG numérico de contexto',
    lowerInclusive: 'Mínimo (incluido)', upperExclusive: 'Máximo (no incluido)',
    canonicalHint: 'Introduzca los límites en la unidad indicada junto al campo; SCADIVO los convierte internamente. El mínimo se incluye y el máximo no: mínimo ≤ lectura < máximo. Estos rangos son independientes del perfil operativo guardado.',
    createScreen: 'Crear borrador de pantalla Runtime'
  };
  return {
    open: 'Criar tela Runtime de desvio contextual',
    hint: 'Selecione pelo menos dois sinais-alvo e dois sinais numéricos de contexto distintos. Isso apenas cria um rascunho de Screen; não consulta o Historian.',
    notEnoughSignals: 'Este perfil tem menos de quatro sinais numéricos utilizáveis.', contextUnitsRequired: 'Os sinais de contexto precisam de uma unidade controlada para converter os limites automaticamente.',
    targets: 'Sinais-alvo (2–8)', targetsHint: 'Os alvos devem ser distintos entre si e dos dois sinais de contexto.', noNumericSignals: 'Nenhum sinal numérico do perfil resolve para TAGs atuais.',
    firstContext: 'Primeiro sinal de contexto', secondContext: 'Segundo sinal de contexto', chooseSignal: 'TAG numérica de contexto',
    lowerInclusive: 'Mínimo (incluído)', upperExclusive: 'Máximo (não incluído)',
    canonicalHint: 'Digite os limites na unidade indicada junto ao campo; o SCADIVO faz a conversão internamente. O mínimo entra na comparação e o máximo não: mínimo ≤ leitura < máximo. Estas faixas são independentes do perfil operacional salvo.',
    createScreen: 'Criar rascunho de tela Runtime'
  };
}
