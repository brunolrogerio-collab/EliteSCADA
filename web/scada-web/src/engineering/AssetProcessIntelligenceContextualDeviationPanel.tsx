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
    return [{
      id: signal.id,
      signalRole: signal.signalRole,
      tag,
      canonicalUnit: unit && Number.isFinite(unit.scaleToCanonical) && unit.scaleToCanonical > 0
        ? unit.canonicalUnit : null
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

  const firstLowerValue = parseCanonicalNumber(firstLower);
  const firstUpperValue = parseCanonicalNumber(firstUpper);
  const secondLowerValue = parseCanonicalNumber(secondLower);
  const secondUpperValue = parseCanonicalNumber(secondUpper);
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
          onSignalChange={setFirstContextId}
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
          onSignalChange={setSecondContextId}
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
      {options.map(signal => <option key={signal.id} value={signal.id}>{signal.signalRole || signal.tag.path} · {signal.canonicalUnit}</option>)}
    </select></label>
    <label>{lowerLabel} ({selected?.canonicalUnit ?? '—'})<input type="text" inputMode="decimal" value={lower} disabled={disabled || !selected} onChange={event => onLowerChange(event.currentTarget.value)}/></label>
    <label>{upperLabel} ({selected?.canonicalUnit ?? '—'})<input type="text" inputMode="decimal" value={upper} disabled={disabled || !selected} onChange={event => onUpperChange(event.currentTarget.value)}/></label>
  </fieldset>;
}

function parseCanonicalNumber(value: string): number | null {
  const normalized = value.trim();
  if (!/^-?(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:[eE][+-]?\d+)?$/.test(normalized)) return null;
  const parsed = Number(normalized.replace(',', '.'));
  return Number.isFinite(parsed) ? parsed : null;
}

function panelCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    open: 'Create contextual deviation Runtime screen',
    hint: 'Select at least two target signals and two distinct numeric context signals. This only creates a Screen draft; it does not query Historian.',
    notEnoughSignals: 'This profile has fewer than four usable numeric signals.', contextUnitsRequired: 'Context signals require a controlled engineering unit so canonical thresholds can be declared without guessing.',
    targets: 'Target signals (2–8)', targetsHint: 'Targets must be distinct from each other and from both context signals.', noNumericSignals: 'No numeric profile signals resolve to current TAGs.',
    firstContext: 'First context signal', secondContext: 'Second context signal', chooseSignal: 'Numeric context TAG',
    lowerInclusive: 'Canonical lower bound (inclusive)', upperExclusive: 'Canonical upper bound (exclusive)',
    canonicalHint: 'Ranges use the controlled canonical unit shown here: lower ≤ value < upper. They are not copied from Engineering context profiles, whose endpoint semantics differ.',
    createScreen: 'Create Runtime screen draft'
  };
  if (locale === 'es') return {
    open: 'Crear pantalla Runtime de desviación contextual',
    hint: 'Seleccione al menos dos señales objetivo y dos señales numéricas de contexto distintas. Solo crea un borrador de Screen; no consulta Historian.',
    notEnoughSignals: 'El perfil tiene menos de cuatro señales numéricas utilizables.', contextUnitsRequired: 'Las señales de contexto requieren una unidad controlada para declarar límites canónicos sin suposiciones.',
    targets: 'Señales objetivo (2–8)', targetsHint: 'Los objetivos deben diferir entre sí y de ambas señales de contexto.', noNumericSignals: 'Ninguna señal numérica del perfil resuelve a TAGs actuales.',
    firstContext: 'Primera señal de contexto', secondContext: 'Segunda señal de contexto', chooseSignal: 'TAG numérico de contexto',
    lowerInclusive: 'Límite canónico inferior (inclusivo)', upperExclusive: 'Límite canónico superior (exclusivo)',
    canonicalHint: 'Los rangos usan la unidad canónica controlada mostrada: límite inferior ≤ valor < límite superior. No se copian de perfiles Engineering, cuyas semánticas de extremos son distintas.',
    createScreen: 'Crear borrador de pantalla Runtime'
  };
  return {
    open: 'Criar tela Runtime de desvio contextual',
    hint: 'Selecione pelo menos dois sinais-alvo e dois sinais numéricos de contexto distintos. Isso apenas cria um rascunho de Screen; não consulta o Historian.',
    notEnoughSignals: 'Este perfil tem menos de quatro sinais numéricos utilizáveis.', contextUnitsRequired: 'Os sinais de contexto precisam de unidade de engenharia controlada para declarar faixas canônicas sem adivinhação.',
    targets: 'Sinais-alvo (2–8)', targetsHint: 'Os alvos devem ser distintos entre si e dos dois sinais de contexto.', noNumericSignals: 'Nenhum sinal numérico do perfil resolve para TAGs atuais.',
    firstContext: 'Primeiro sinal de contexto', secondContext: 'Segundo sinal de contexto', chooseSignal: 'TAG numérica de contexto',
    lowerInclusive: 'Limite canônico inferior (inclusivo)', upperExclusive: 'Limite canônico superior (exclusivo)',
    canonicalHint: 'As faixas usam a unidade canônica controlada exibida: limite inferior ≤ valor < limite superior. Elas não são copiadas dos perfis de contexto do Engineering, cujas semânticas de extremidade são diferentes.',
    createScreen: 'Criar rascunho de tela Runtime'
  };
}
