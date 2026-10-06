import React, {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useMemo,
  useState
} from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import { applyModbusAddressBuild, metadataValue } from './TagAddressAssistant.logic';
import { buildModbusTagAddress } from './tagAddressApi';
import {
  resolveTagDataSource,
  updateManualTagAddress,
  type TagSourceAwareEngineering
} from './TagSourceSelector.logic';
import {
  MAX_TAG_SEQUENCE_COUNT,
  copyTagConfiguration,
  createDuplicateTagDrafts,
  createSequentialTagDrafts,
  sequentialModbusReference,
  validateGeneratedTagDrafts,
  type TagDuplicationDraft,
  type TagSequenceOptions
} from './tagDuplicationModel';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView, ImportPreviewView } from './types';

export type TagDuplicationPanelHandle = {
  copySelected: () => void;
  copyResources: (sources: readonly TagSourceAwareEngineering[]) => void;
  paste: () => void;
  pasteCopied: (sources: readonly TagSourceAwareEngineering[]) => void;
  duplicateSelected: () => void;
};

type Props = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
  primaryTag: TagSourceAwareEngineering | null;
  selectedTags: readonly TagSourceAwareEngineering[];
  selectionMode: boolean;
  onToggleSelectionMode: () => void;
  onClipboardChange?: (count: number) => void;
};

type GenerationKind = 'duplicate' | 'paste' | 'sequential';

const initialSequence: TagSequenceOptions = {
  count: 20,
  namePattern: '{name}_{n}',
  pathPattern: '{path}_{n}',
  suffixStart: 1,
  suffixStep: 1,
  addressStep: 1
};

export const TagDuplicationPanel = forwardRef<TagDuplicationPanelHandle, Props>(function TagDuplicationPanel({
  model,
  locale,
  primaryTag,
  selectedTags,
  selectionMode,
  onToggleSelectionMode,
  onClipboardChange
}, ref) {
  const text = useMemo(() => tagDuplicationText(locale), [locale]);
  const [clipboard, setClipboard] = useState<TagDuplicationDraft[]>([]);
  const [generated, setGenerated] = useState<TagDuplicationDraft[]>([]);
  const [generationKind, setGenerationKind] = useState<GenerationKind>('duplicate');
  const [sequence, setSequence] = useState<TagSequenceOptions>(initialSequence);
  const [sequenceOpen, setSequenceOpen] = useState(false);
  const [bulkFind, setBulkFind] = useState('');
  const [bulkReplace, setBulkReplace] = useState('');
  const [bulkTarget, setBulkTarget] = useState<'name' | 'path' | 'both'>('both');
  const [serverPreview, setServerPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedChangeVersion, setValidatedChangeVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [building, setBuilding] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);

  const effectiveSelection = selectionMode
    ? selectedTags
    : primaryTag
      ? [primaryTag]
      : [];

  const localValidation = useMemo(
    () => validateGeneratedTagDrafts(
      model.tags as TagSourceAwareEngineering[],
      generated,
      { checkAddressCollisions: generationKind === 'sequential' }
    ),
    [generated, generationKind, model.tags]
  );

  const invalidatePreview = () => {
    setServerPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);
  };

  useEffect(() => {
    invalidatePreview();
  }, [generated]);

  useEffect(() => {
    // Any official model refresh makes the generated candidate stale. Keep the
    // session clipboard, but require the engineer to regenerate/re-preview.
    setGenerated([]);
    invalidatePreview();
  }, [model]);

  const copyResources = (sources: readonly TagSourceAwareEngineering[]) => {
    setError(null);
    if (sources.length === 0) {
      setError(text.selectRequired);
      return;
    }

    // Clipboard is intentionally session-local configuration, not a portable wire.
    setClipboard(sources.map((tag, index) =>
      copyTagConfiguration(tag, `clipboard-${index + 1}`)));
    onClipboardChange?.(sources.length);
  };
  const copySelected = () => copyResources(effectiveSelection);

  const duplicateSelected = () => {
    setError(null);
    if (effectiveSelection.length === 0) {
      setError(text.selectRequired);
      return;
    }
    try {
      setGenerated(createDuplicateTagDrafts(effectiveSelection, model.tags, newStableTagId));
      setGenerationKind('duplicate');
      setSequenceOpen(false);
    } catch (reason) {
      setError(errorMessage(reason));
    }
  };

  const paste = () => {
    setError(null);
    if (clipboard.length === 0) {
      setError(text.clipboardEmpty);
      return;
    }
    try {
      setGenerated(createDuplicateTagDrafts(clipboard, model.tags, newStableTagId));
      setGenerationKind('paste');
      setSequenceOpen(false);
    } catch (reason) {
      setError(errorMessage(reason));
    }
  };

  const pasteCopied = (sources: readonly TagSourceAwareEngineering[]) => {
    setError(null);
    if (sources.length === 0) return;
    try {
      setGenerated(createDuplicateTagDrafts(sources, model.tags, newStableTagId));
      setGenerationKind('paste');
      setSequenceOpen(false);
    } catch (reason) {
      setError(errorMessage(reason));
    }
  };

  useImperativeHandle(ref, () => ({
    copySelected,
    copyResources,
    paste,
    pasteCopied,
    duplicateSelected
  }));

  const generateSequential = async () => {
    setError(null);
    const source = effectiveSelection.length === 1 ? effectiveSelection[0] : null;
    if (!source) {
      setError(text.sequenceSingleRequired);
      return;
    }

    const resolvedSource = resolveTagDataSource(source, model.dataSources ?? []).source;
    if (resolvedSource?.driver.trim().toLowerCase() !== 'modbus.tcp') {
      setError(text.modbusOnly);
      return;
    }

    setBuilding(true);
    try {
      const skeletons = createSequentialTagDrafts(source, model.tags, sequence, newStableTagId);
      const withAddresses: TagDuplicationDraft[] = [];

      for (let index = 0; index < skeletons.length; index += 1) {
        const step = sequentialModbusReference(source.address, sequence.addressStep, index);
        const result = await buildModbusTagAddress({
          area: step.area,
          reference: step.reference,
          referenceBase: 'zeroBased',
          unitId: optionalInteger(metadataValue(source, 'modbus.unitId')),
          valueType: optionalString(metadataValue(source, 'modbus.valueType')),
          wordOrder: optionalString(metadataValue(source, 'modbus.wordOrder')),
          scale: optionalNumber(metadataValue(source, 'modbus.scale')),
          offset: optionalNumber(metadataValue(source, 'modbus.offset')),
          bitIndex: source.addressSelector?.kind === 'bit' ? source.addressSelector.index : null
        });
        withAddresses.push(applyModbusAddressBuild(skeletons[index], result));
      }

      setGenerated(withAddresses);
      setGenerationKind('sequential');
    } catch (reason) {
      setGenerated([]);
      setError(errorMessage(reason));
    } finally {
      setBuilding(false);
    }
  };

  const updateDraft = (index: number, change: Partial<Pick<TagDuplicationDraft, 'name' | 'path' | 'address'>>) => {
    setGenerated(current => current.map((draft, currentIndex) => {
      if (currentIndex !== index) return draft;
      let next = { ...draft, ...change };
      if (Object.prototype.hasOwnProperty.call(change, 'address')) {
        next = updateManualTagAddress(next, change.address?.trim() ? change.address : null);
      }
      return next;
    }));
  };

  const applyBulkDraftEdit = () => {
    if (!bulkFind) return;
    setGenerated(current => current.map(draft => ({
      ...draft,
      name: bulkTarget === 'path' ? draft.name : draft.name.replaceAll(bulkFind, bulkReplace),
      path: bulkTarget === 'name' ? draft.path : draft.path.replaceAll(bulkFind, bulkReplace)
    })));
  };

  const runPreview = async () => {
    setError(null);
    if (generated.length === 0) {
      setError(text.generateFirst);
      return;
    }
    if (!localValidation.canPreview) {
      setError(text.fixCollisions);
      return;
    }

    setPreviewing(true);
    invalidatePreview();
    try {
      const before = await loadEngineeringWorkspace();
      const nextCandidate: EngineeringPackageView = {
        ...clone(model),
        tags: [...clone(model.tags), ...clone(generated)]
      };
      const preview = await previewEngineeringPackage(nextCandidate);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion) {
        throw new Error(text.workspaceChanged);
      }
      setServerPreview(preview);
      setCandidate(nextCandidate);
      setValidatedChangeVersion(after.changeVersion);
    } catch (reason) {
      setError(errorMessage(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const runApply = async () => {
    if (!candidate || !serverPreview?.canApply || validatedChangeVersion === null || !localValidation.canPreview) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate, validatedChangeVersion);
      window.location.reload();
    } catch (reason) {
      invalidatePreview();
      setError(errorMessage(reason));
    } finally {
      setApplying(false);
    }
  };

  const busy = building || previewing || applying;
  const selectedCount = effectiveSelection.length;

  return (
    <section className="tag-duplication" data-testid="tag-duplication">
      <header className="tag-duplication__header">
        <div>
          <span>{text.eyebrow}</span>
          <strong>{text.title}</strong>
          <small>{text.description}</small>
        </div>
        <div className="tag-duplication__selection">
          <span>{text.selected}: <b data-testid="tag-duplication-selected-count">{selectedCount}</b></span>
          <button type="button" className="secondary" onClick={onToggleSelectionMode} disabled={busy}>
            {selectionMode ? text.finishSelection : text.selectMultiple}
          </button>
        </div>
      </header>

      <div className="tag-duplication__toolbar" role="toolbar" aria-label={text.toolbarLabel}>
        <button type="button" onClick={copySelected} disabled={selectedCount === 0 || busy} data-testid="tag-copy-selected">{text.copy}</button>
        <button type="button" onClick={paste} disabled={clipboard.length === 0 || busy} data-testid="tag-paste">{text.paste}</button>
        <button type="button" onClick={duplicateSelected} disabled={selectedCount === 0 || busy} data-testid="tag-duplicate-selected">{text.duplicate}</button>
        <button
          type="button"
          onClick={() => setSequenceOpen(current => !current)}
          disabled={selectedCount !== 1 || busy}
          data-testid="tag-sequence-toggle"
        >
          {text.sequential}
        </button>
        <span>{text.clipboard}: <b data-testid="tag-clipboard-count">{clipboard.length}</b></span>
      </div>

      {sequenceOpen && (
        <section className="tag-duplication__sequence" data-testid="tag-sequence-panel">
          <header><strong>{text.sequenceTitle}</strong><span>{text.sequenceHint}</span></header>
          <div className="tag-duplication__sequence-grid">
            <NumberInput label={text.count} value={sequence.count} min={1} max={MAX_TAG_SEQUENCE_COUNT} onChange={value => setSequence(current => ({ ...current, count: value }))} />
            <label><span>{text.namePattern}</span><input value={sequence.namePattern} onChange={event => setSequence(current => ({ ...current, namePattern: event.target.value }))} /></label>
            <label><span>{text.pathPattern}</span><input value={sequence.pathPattern} onChange={event => setSequence(current => ({ ...current, pathPattern: event.target.value }))} /></label>
            <NumberInput label={text.suffixStart} value={sequence.suffixStart} onChange={value => setSequence(current => ({ ...current, suffixStart: value }))} />
            <NumberInput label={text.suffixStep} value={sequence.suffixStep} onChange={value => setSequence(current => ({ ...current, suffixStep: value }))} />
            <NumberInput label={text.addressStep} value={sequence.addressStep} onChange={value => setSequence(current => ({ ...current, addressStep: value }))} />
          </div>
          <button type="button" className="primary" onClick={() => void generateSequential()} disabled={building} data-testid="tag-sequence-generate">
            {building ? text.generating : text.generate}
          </button>
        </section>
      )}

      {generated.length > 0 && (
        <section className="tag-duplication__drafts" data-testid="tag-generated-preview">
          <header>
            <div><strong>{text.generatedPreview}</strong><span>{text.noMutationHint}</span></div>
            <b>{generated.length}</b>
          </header>

          <div className="tag-duplication__bulk-edit">
            <label><span>{text.bulkFind}</span><input value={bulkFind} onChange={event => setBulkFind(event.target.value)} /></label>
            <label><span>{text.bulkReplace}</span><input value={bulkReplace} onChange={event => setBulkReplace(event.target.value)} /></label>
            <label><span>{text.bulkTarget}</span>
              <select value={bulkTarget} onChange={event => setBulkTarget(event.target.value as typeof bulkTarget)}>
                <option value="both">{text.nameAndPath}</option>
                <option value="name">{text.nameOnly}</option>
                <option value="path">{text.pathOnly}</option>
              </select>
            </label>
            <button type="button" onClick={applyBulkDraftEdit} disabled={!bulkFind || busy}>{text.applyBulkDraft}</button>
          </div>

          <div className="tag-duplication__table-wrap">
            <table>
              <thead><tr><th>#</th><th>{text.name}</th><th>{text.path}</th><th>{text.address}</th><th>{text.stableId}</th></tr></thead>
              <tbody>
                {generated.map((draft, index) => (
                  <tr key={draft.id ?? index} data-testid="tag-generated-row">
                    <td>{index + 1}</td>
                    <td><input aria-label={`${text.name} ${index + 1}`} value={draft.name} onChange={event => updateDraft(index, { name: event.target.value })} /></td>
                    <td><input aria-label={`${text.path} ${index + 1}`} value={draft.path} onChange={event => updateDraft(index, { path: event.target.value })} /></td>
                    <td><input aria-label={`${text.address} ${index + 1}`} value={draft.address ?? ''} onChange={event => updateDraft(index, { address: event.target.value })} /></td>
                    <td><code title={draft.id}>{draft.id}</code></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {localValidation.collisions.length > 0 && (
            <div className="tag-duplication__collisions" role="alert" data-testid="tag-duplication-collisions">
              <strong>{text.collisions}</strong>
              {localValidation.collisions.map((collision, index) => (
                <span key={`${collision.code}-${collision.index}-${index}`}>{collision.message}</span>
              ))}
            </div>
          )}

          <div className="tag-duplication__preview-actions">
            <button type="button" className="secondary" onClick={() => void runPreview()} disabled={!localValidation.canPreview || busy} data-testid="tag-duplication-preview">
              {previewing ? text.previewing : text.preview}
            </button>
            <button type="button" className="primary" onClick={() => void runApply()} disabled={!serverPreview?.canApply || !localValidation.canPreview || busy} data-testid="tag-duplication-apply">
              {applying ? text.applying : text.apply}
            </button>
          </div>

          {serverPreview && (
            <div className={serverPreview.canApply ? 'tag-duplication__server-preview valid' : 'tag-duplication__server-preview invalid'} data-testid="tag-duplication-server-preview">
              <strong>{serverPreview.canApply ? text.validPreview : text.invalidPreview}</strong>
              <span>{text.creates}: {serverPreview.createCount}</span>
              <span>{text.updates}: {serverPreview.updateCount}</span>
              <span>{text.errors}: {serverPreview.errorCount}</span>
              <small>Workspace v{validatedChangeVersion}</small>
            </div>
          )}
        </section>
      )}

      <footer>
        <span>{text.shortcutHint}</span>
        <span>{text.portabilityHint}</span>
      </footer>

      {error && <pre className="eng-preview-error" role="alert" data-testid="tag-duplication-error">{error}</pre>}
    </section>
  );
});

function NumberInput({
  label,
  value,
  onChange,
  min,
  max
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
}) {
  return (
    <label>
      <span>{label}</span>
      <input
        type="number"
        step="1"
        min={min}
        max={max}
        value={value}
        onChange={event => onChange(Number(event.target.value))}
      />
    </label>
  );
}

function newStableTagId(): string {
  if (!globalThis.crypto?.randomUUID) {
    throw new Error('Secure stable TAG ID generation is unavailable in this browser session.');
  }
  return globalThis.crypto.randomUUID();
}

function optionalString(value: string): string | null {
  return value.trim() ? value.trim() : null;
}

function optionalInteger(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed)) throw new Error(`Invalid integer value '${value}'.`);
  return parsed;
}

function optionalNumber(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new Error(`Invalid numeric value '${value}'.`);
  return parsed;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function errorMessage(reason: unknown): string {
  return reason instanceof Error ? reason.message : String(reason);
}

export function tagDuplicationText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'TAG productivity', title: 'Copy, duplicate and generate', description: 'Prepare new TAG drafts, inspect every generated identity/address, then Preview and Apply through the canonical Workspace flow.',
    selected: 'Selected', selectMultiple: 'Select multiple', finishSelection: 'Finish selection', toolbarLabel: 'TAG copy and duplicate actions',
    copy: 'Copy selected', paste: 'Paste', duplicate: 'Duplicate selected', sequential: 'Sequential…', clipboard: 'Internal clipboard',
    sequenceTitle: 'Bounded Modbus sequence', sequenceHint: 'Canonical Modbus coil/discrete/holding/input addresses only. The Driver-owned builder validates every generated address.',
    count: 'Count', namePattern: 'Display name pattern', pathPattern: 'TAG path pattern', suffixStart: 'Suffix start', suffixStep: 'Suffix step', addressStep: 'Address step',
    generate: 'Generate drafts', generating: 'Generating…', generatedPreview: 'Generated TAG Preview', noMutationHint: 'This table is session-only. Working is untouched until Apply.',
    bulkFind: 'Find', bulkReplace: 'Replace with', bulkTarget: 'Bulk target', nameAndPath: 'Display name + TAG path', nameOnly: 'Display name', pathOnly: 'TAG path', applyBulkDraft: 'Edit generated drafts',
    name: 'Display name', path: 'TAG path', address: 'Address', stableId: 'New stable identifier', collisions: 'Resolve collisions before Preview.',
    preview: 'Validate Preview', previewing: 'Validating…', apply: 'Apply to Working', applying: 'Applying…', validPreview: 'Valid candidate', invalidPreview: 'Invalid candidate',
    creates: 'Creates', updates: 'Updates', errors: 'Errors', shortcutHint: 'On a focused TAG: Ctrl/Cmd+C copies, Ctrl/Cmd+V pastes, Ctrl/Cmd+D duplicates.',
    portabilityHint: 'Internal clipboard is same-session only. Cross-project transfer remains Engineering Fragment (.escadafrag).',
    selectRequired: 'Select at least one TAG first.', clipboardEmpty: 'The internal TAG clipboard is empty.', sequenceSingleRequired: 'Sequential generation requires exactly one source TAG.',
    modbusOnly: 'Automatic address sequencing is supported only for a TAG bound to a Modbus TCP Data Source.', generateFirst: 'Generate TAG drafts before Preview.',
    fixCollisions: 'Resolve generated TAG collisions before Preview.', workspaceChanged: 'Engineering Workspace changed while this candidate was being validated. Reload and generate again.'
  };
  if (locale === 'es') return {
    eyebrow: 'Productividad TAG', title: 'Copiar, duplicar y generar', description: 'Prepare borradores de TAG, revise cada identidad/dirección generada y luego use Preview y Apply por el flujo canónico del Workspace.',
    selected: 'Seleccionados', selectMultiple: 'Seleccionar varios', finishSelection: 'Finalizar selección', toolbarLabel: 'Acciones de copia y duplicación de TAG',
    copy: 'Copiar seleccionados', paste: 'Pegar', duplicate: 'Duplicar seleccionados', sequential: 'Secuencial…', clipboard: 'Portapapeles interno',
    sequenceTitle: 'Secuencia Modbus limitada', sequenceHint: 'Solo direcciones canónicas Modbus coil/discrete/holding/input. El builder del Driver valida cada dirección.',
    count: 'Cantidad', namePattern: 'Patrón de nombre visible', pathPattern: 'Patrón de ruta de TAG', suffixStart: 'Inicio del sufijo', suffixStep: 'Paso del sufijo', addressStep: 'Paso de dirección',
    generate: 'Generar borradores', generating: 'Generando…', generatedPreview: 'Preview de TAGs generados', noMutationHint: 'Esta tabla es solo de sesión. Working no cambia hasta Apply.',
    bulkFind: 'Buscar', bulkReplace: 'Reemplazar por', bulkTarget: 'Destino de edición', nameAndPath: 'Nombre visible + ruta de TAG', nameOnly: 'Nombre visible', pathOnly: 'Ruta de TAG', applyBulkDraft: 'Editar borradores generados',
    name: 'Nombre visible', path: 'Ruta de TAG', address: 'Dirección', stableId: 'Nuevo identificador estable', collisions: 'Resuelva las colisiones antes del Preview.',
    preview: 'Validar Preview', previewing: 'Validando…', apply: 'Aplicar a Working', applying: 'Aplicando…', validPreview: 'Candidato válido', invalidPreview: 'Candidato inválido',
    creates: 'Creaciones', updates: 'Actualizaciones', errors: 'Errores', shortcutHint: 'Con un TAG enfocado: Ctrl/Cmd+C copia, Ctrl/Cmd+V pega, Ctrl/Cmd+D duplica.',
    portabilityHint: 'El portapapeles interno es solo de la misma sesión. Entre proyectos se mantiene Engineering Fragment (.escadafrag).',
    selectRequired: 'Seleccione al menos un TAG.', clipboardEmpty: 'El portapapeles interno de TAG está vacío.', sequenceSingleRequired: 'La generación secuencial requiere exactamente un TAG de origen.',
    modbusOnly: 'La secuencia automática de direcciones solo se admite para un TAG vinculado a un Data Source Modbus TCP.', generateFirst: 'Genere borradores antes del Preview.',
    fixCollisions: 'Resuelva las colisiones antes del Preview.', workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue y genere nuevamente.'
  };
  return {
    eyebrow: 'Produtividade de TAG', title: 'Copiar, duplicar e gerar', description: 'Prepare novos rascunhos, confira identidade e endereço de cada TAG e só então use Preview e Apply pelo fluxo canônico do Workspace.',
    selected: 'Selecionados', selectMultiple: 'Selecionar vários', finishSelection: 'Concluir seleção', toolbarLabel: 'Ações de cópia e duplicação de TAG',
    copy: 'Copiar selecionados', paste: 'Colar', duplicate: 'Duplicar selecionados', sequential: 'Sequencial…', clipboard: 'Clipboard interno',
    sequenceTitle: 'Sequência Modbus limitada', sequenceHint: 'Somente endereços canônicos Modbus coil/discrete/holding/input. O builder do Driver valida cada endereço gerado.',
    count: 'Quantidade', namePattern: 'Padrão do nome de exibição', pathPattern: 'Padrão do caminho da TAG', suffixStart: 'Início do sufixo', suffixStep: 'Passo do sufixo', addressStep: 'Passo do endereço',
    generate: 'Gerar rascunhos', generating: 'Gerando…', generatedPreview: 'Preview dos TAGs gerados', noMutationHint: 'Esta tabela existe só na sessão. Working permanece intocado até o Apply.',
    bulkFind: 'Localizar', bulkReplace: 'Substituir por', bulkTarget: 'Aplicar em', nameAndPath: 'Nome de exibição + caminho da TAG', nameOnly: 'Nome de exibição', pathOnly: 'Caminho da TAG', applyBulkDraft: 'Editar rascunhos gerados',
    name: 'Nome de exibição', path: 'Caminho da TAG', address: 'Endereço', stableId: 'Novo identificador estável', collisions: 'Resolva as colisões antes do Preview.',
    preview: 'Validar Preview', previewing: 'Validando…', apply: 'Aplicar ao Working', applying: 'Aplicando…', validPreview: 'Candidato válido', invalidPreview: 'Candidato inválido',
    creates: 'Criações', updates: 'Atualizações', errors: 'Erros', shortcutHint: 'Com um TAG focado: Ctrl/Cmd+C copia, Ctrl/Cmd+V cola, Ctrl/Cmd+D duplica.',
    portabilityHint: 'O clipboard interno vale só nesta sessão. Entre projetos, a direção permanece Engineering Fragment (.escadafrag).',
    selectRequired: 'Selecione pelo menos um TAG.', clipboardEmpty: 'O clipboard interno de TAG está vazio.', sequenceSingleRequired: 'A geração sequencial exige exatamente um TAG de origem.',
    modbusOnly: 'A sequência automática de endereço é suportada somente para TAG ligado a Data Source Modbus TCP.', generateFirst: 'Gere rascunhos antes do Preview.',
    fixCollisions: 'Resolva as colisões dos TAGs gerados antes do Preview.', workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e gere novamente.'
  };
}
