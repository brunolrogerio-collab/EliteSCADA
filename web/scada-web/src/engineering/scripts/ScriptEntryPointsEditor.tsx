import React, { useEffect, useMemo, useState } from 'react';
import { initializeClientMemory } from '../../runtime/clientMemory';
import { loadEngineeringSnapshot } from '../api';
import type { EngineeringLocale } from '../i18n';
import {
  buildProjectReferenceCatalog,
  type ClientMemoryDefinitionView,
  type ProjectReferenceDescriptor
} from '../project-reference/projectReferenceModel';
import { eventKindLabel } from './ScriptEngineeringWorkspace.copy';
import {
  DEFAULT_SCRIPT_TIMER_INTERVAL_MS,
  isScriptEventAllowedForScope,
  MINIMUM_SCRIPT_TIMER_INTERVAL_MS,
  retargetScriptEntryPoint,
  SCRIPT_EVENT_KINDS
} from './ScriptEngineeringWorkspace.logic';
import type {
  ScriptEngineeringEntryPoint,
  ScriptEngineeringEventKind,
  ScriptEngineeringScope
} from './scriptEngineeringTypes';
import './script-entry-points-editor.css';

type ScriptEntryPointsEditorProps = {
  locale: EngineeringLocale;
  scope: ScriptEngineeringScope;
  entries: readonly ScriptEngineeringEntryPoint[];
  disabled?: boolean;
  onChange(entries: ScriptEngineeringEntryPoint[]): void;
};

export function ScriptEntryPointsEditor({
  locale,
  scope,
  entries,
  disabled = false,
  onChange
}: ScriptEntryPointsEditorProps) {
  const copy = useMemo(() => entryPointCopy(locale), [locale]);
  const [catalog, setCatalog] = useState<readonly ProjectReferenceDescriptor[]>(Object.freeze([]));
  const [catalogError, setCatalogError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void Promise.all([loadEngineeringSnapshot(), initializeClientMemory()])
      .then(([snapshot, memoryDefinitions]) => {
        if (cancelled) return;
        const memoryViews: readonly ClientMemoryDefinitionView[] = memoryDefinitions.map(definition => ({
          id: definition.id,
          name: definition.name,
          path: definition.path,
          dataType: definition.dataType,
          initialValue: definition.initialValue,
          readOnly: definition.readOnly
        }));
        setCatalog(buildProjectReferenceCatalog(snapshot.package, memoryViews));
        setCatalogError(false);
      })
      .catch(() => {
        if (!cancelled) setCatalogError(true);
      });
    return () => { cancelled = true; };
  }, []);

  const tagTargets = useMemo(
    () => catalog.filter(item => item.bindingKind === 'Tag' && item.tagReference?.tagId),
    [catalog]
  );
  const memoryTargets = useMemo(
    () => catalog.filter(item => item.bindingKind === 'ClientMemory' && item.tagReference?.tagId),
    [catalog]
  );

  function replaceEntry(index: number, next: ScriptEngineeringEntryPoint) {
    onChange(entries.map((entry, entryIndex) => entryIndex === index ? next : cloneEntry(entry)));
  }

  function patchEntry(index: number, patch: Partial<ScriptEngineeringEntryPoint>) {
    const current = entries[index];
    if (!current) return;
    replaceEntry(index, { ...cloneEntry(current), ...patch });
  }

  function removeEntry(index: number) {
    onChange(entries.filter((_, entryIndex) => entryIndex !== index).map(cloneEntry));
  }

  return (
    <div className="script-rows" data-testid="script-entry-points-editor">
      <datalist id="script-entry-point-tag-targets">
        {tagTargets.map(target => (
          <option
            key={projectTargetKey(target)}
            value={target.tagReference!.tagId}
            label={target.label + ' · ' + target.reference}
          />
        ))}
      </datalist>
      <datalist id="script-entry-point-memory-targets">
        {memoryTargets.map(target => (
          <option
            key={projectTargetKey(target)}
            value={target.tagReference!.tagId}
            label={target.label + ' · ' + target.reference}
          />
        ))}
      </datalist>

      {entries.map((entry, index) => {
        const selectedTag = tagTargets.find(target => target.tagReference?.tagId === entry.tagReference?.tagId) ?? null;
        const selectedMemory = memoryTargets.find(target => target.tagReference?.tagId === entry.targetReference) ?? null;
        const selector = entry.tagReference?.selector;
        const supportsBitSelector = selectedTag?.selectorCapability?.kind === 'bit' || selector?.kind === 'bit';
        const isAllowed = isScriptEventAllowedForScope(scope, entry.eventKind);

        return (
          <div className="script-row script-entry-point-editor" key={'entry-' + index}>
            <label>{copy.event}
              <select
                value={entry.eventKind}
                disabled={disabled}
                data-testid={'script-entry-event-' + index}
                onChange={event => replaceEntry(
                  index,
                  retargetScriptEntryPoint(entry, event.currentTarget.value as ScriptEngineeringEventKind)
                )}
              >
                {SCRIPT_EVENT_KINDS.map(kind => (
                  <option key={kind} value={kind} disabled={!isScriptEventAllowedForScope(scope, kind)}>
                    {eventKindLabel(kind, locale)}{isScriptEventAllowedForScope(scope, kind) ? '' : ' · ' + copy.unsupported}
                  </option>
                ))}
              </select>
            </label>

            <label>{copy.handler}
              <input
                value={entry.handlerName}
                disabled={disabled}
                data-testid={'script-entry-handler-' + index}
                onChange={event => patchEntry(index, { handlerName: event.currentTarget.value })}
              />
            </label>

            <div className="script-entry-point-editor__target">
              {!isAllowed && <p className="script-entry-point-editor__warning">{copy.scopeMismatch}</p>}
              {entry.eventKind === 'timer' ? (
                <label>{copy.timerInterval}
                  <input
                    type="number"
                    min={MINIMUM_SCRIPT_TIMER_INTERVAL_MS}
                    step="1"
                    value={entry.timerIntervalMs ?? DEFAULT_SCRIPT_TIMER_INTERVAL_MS}
                    disabled={disabled}
                    data-testid={'script-entry-timer-' + index}
                    onChange={event => patchEntry(index, {
                      timerIntervalMs: event.currentTarget.value === '' ? null : Number(event.currentTarget.value),
                      targetReference: null,
                      tagReference: null
                    })}
                  />
                  <small>{copy.timerHint}</small>
                </label>
              ) : entry.eventKind === 'tagChanged' ? (
                <>
                  <label>{copy.tagTarget}
                    <input
                      list="script-entry-point-tag-targets"
                      value={entry.tagReference?.tagId ?? ''}
                      disabled={disabled}
                      placeholder={copy.tagTargetPlaceholder}
                      data-testid={'script-entry-tag-' + index}
                      onChange={event => {
                        const tagId = event.currentTarget.value.trim();
                        const sameTag = tagId && tagId === entry.tagReference?.tagId;
                        patchEntry(index, {
                          targetReference: null,
                          timerIntervalMs: null,
                          tagReference: tagId ? {
                            tagId,
                            selector: sameTag && entry.tagReference?.selector ? { ...entry.tagReference.selector } : null
                          } : null
                        });
                      }}
                    />
                    <small>{selectedTag ? selectedTag.label + ' · ' + selectedTag.reference : copy.tagTargetHint}</small>
                  </label>
                  {supportsBitSelector && (
                    <label>{copy.bitSelector}
                      <input
                        type="number"
                        min={selectedTag?.selectorCapability?.minIndex ?? 0}
                        max={selectedTag?.selectorCapability?.maxIndex}
                        step="1"
                        value={selector?.kind === 'bit' ? selector.index : ''}
                        disabled={disabled}
                        placeholder={copy.wholeTag}
                        data-testid={'script-entry-tag-bit-' + index}
                        onChange={event => {
                          const raw = event.currentTarget.value;
                          patchEntry(index, {
                            tagReference: entry.tagReference ? {
                              ...entry.tagReference,
                              selector: raw === '' ? null : { kind: 'bit', index: Number(raw) }
                            } : null
                          });
                        }}
                      />
                      <small>{copy.bitSelectorHint}</small>
                    </label>
                  )}
                </>
              ) : entry.eventKind === 'clientMemoryChanged' ? (
                <label>{copy.clientMemoryTarget}
                  <input
                    list="script-entry-point-memory-targets"
                    value={entry.targetReference ?? ''}
                    disabled={disabled}
                    placeholder={copy.clientMemoryPlaceholder}
                    data-testid={'script-entry-memory-' + index}
                    onChange={event => patchEntry(index, {
                      targetReference: event.currentTarget.value.trim() || null,
                      tagReference: null,
                      timerIntervalMs: null
                    })}
                  />
                  <small>{selectedMemory ? selectedMemory.label + ' · ' + selectedMemory.reference : copy.clientMemoryHint}</small>
                </label>
              ) : usesStableTarget(entry.eventKind) ? (
                <label>{copy.stableTarget}
                  <input
                    value={entry.targetReference ?? ''}
                    disabled={disabled}
                    placeholder={copy.stableTargetPlaceholder}
                    data-testid={'script-entry-target-' + index}
                    onChange={event => patchEntry(index, {
                      targetReference: event.currentTarget.value || null,
                      tagReference: null,
                      timerIntervalMs: null
                    })}
                  />
                  <small>{copy.stableTargetHint}</small>
                </label>
              ) : (
                <span className="script-entry-point-editor__none">{copy.noTarget}</span>
              )}
              {catalogError && (entry.eventKind === 'tagChanged' || entry.eventKind === 'clientMemoryChanged') && (
                <small className="script-entry-point-editor__warning">{copy.catalogUnavailable}</small>
              )}
            </div>

            <button
              type="button"
              className="danger ghost"
              disabled={disabled}
              onClick={() => removeEntry(index)}
            >
              {copy.remove}
            </button>
          </div>
        );
      })}
    </div>
  );
}

function cloneEntry(entry: ScriptEngineeringEntryPoint): ScriptEngineeringEntryPoint {
  return {
    ...entry,
    tagReference: entry.tagReference
      ? {
          ...entry.tagReference,
          selector: entry.tagReference.selector ? { ...entry.tagReference.selector } : null
        }
      : null
  };
}

function usesStableTarget(eventKind: ScriptEngineeringEventKind): boolean {
  return eventKind === 'objectInteraction' || eventKind === 'propertyChanged' || eventKind === 'serverRuntimeEvent';
}

function projectTargetKey(target: ProjectReferenceDescriptor): string {
  return (target.tagReference?.tagId ?? target.reference) + ':' + target.reference;
}

function entryPointCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      event: 'Evento', handler: 'Handler', remove: 'Remover', unsupported: 'indisponível neste escopo',
      scopeMismatch: 'Este evento não pertence ao escopo atual. Selecione um evento compatível antes do Preview.',
      timerInterval: 'Intervalo (ms)', timerHint: 'Intervalo canônico do Timer; mínimo de 50 ms.',
      tagTarget: 'TAG canônica', tagTargetPlaceholder: 'Selecione ou informe o TagId',
      tagTargetHint: 'Use a descoberta para escolher a TAG; o valor persistido é o TagId estável.',
      bitSelector: 'Bit (opcional)', bitSelectorHint: 'Use um índice válido quando a TAG inteira não for o alvo.', wholeTag: 'TAG inteira',
      clientMemoryTarget: 'Client Memory', clientMemoryPlaceholder: 'Selecione ou informe o ID estável',
      clientMemoryHint: 'A associação persiste o ID estável da definição de Client Memory.',
      stableTarget: 'Referência alvo estável (opcional)', stableTargetPlaceholder: 'ID/referência opaca estável',
      stableTargetHint: 'Use somente uma referência estável pertencente ao evento; não codifique duração ou path de TAG aqui.',
      noTarget: 'Este evento não exige configuração de alvo.',
      catalogUnavailable: 'A descoberta está indisponível; IDs estáveis ainda podem ser informados manualmente.'
    },
    en: {
      event: 'Event', handler: 'Handler', remove: 'Remove', unsupported: 'unavailable for this scope',
      scopeMismatch: 'This event does not belong to the current scope. Select a compatible event before Preview.',
      timerInterval: 'Interval (ms)', timerHint: 'Canonical Timer interval; minimum 50 ms.',
      tagTarget: 'Canonical TAG', tagTargetPlaceholder: 'Select or enter the TagId',
      tagTargetHint: 'Use discovery to choose the TAG; the persisted value is the stable TagId.',
      bitSelector: 'Bit (optional)', bitSelectorHint: 'Use a valid index when the whole TAG is not the target.', wholeTag: 'Whole TAG',
      clientMemoryTarget: 'Client Memory', clientMemoryPlaceholder: 'Select or enter the stable ID',
      clientMemoryHint: 'The association persists the stable Client Memory definition ID.',
      stableTarget: 'Stable target reference (optional)', stableTargetPlaceholder: 'Stable opaque ID/reference',
      stableTargetHint: 'Use only a stable reference owned by the event; do not encode Timer duration or TAG paths here.',
      noTarget: 'This event requires no target configuration.',
      catalogUnavailable: 'Discovery is unavailable; stable IDs can still be entered manually.'
    },
    es: {
      event: 'Evento', handler: 'Handler', remove: 'Eliminar', unsupported: 'no disponible para este ámbito',
      scopeMismatch: 'Este evento no pertenece al ámbito actual. Seleccione un evento compatible antes del Preview.',
      timerInterval: 'Intervalo (ms)', timerHint: 'Intervalo canónico del Timer; mínimo de 50 ms.',
      tagTarget: 'TAG canónica', tagTargetPlaceholder: 'Seleccione o informe el TagId',
      tagTargetHint: 'Use el descubrimiento para elegir la TAG; el valor persistido es el TagId estable.',
      bitSelector: 'Bit (opcional)', bitSelectorHint: 'Use un índice válido cuando la TAG completa no sea el objetivo.', wholeTag: 'TAG completa',
      clientMemoryTarget: 'Client Memory', clientMemoryPlaceholder: 'Seleccione o informe el ID estable',
      clientMemoryHint: 'La asociación persiste el ID estable de la definición de Client Memory.',
      stableTarget: 'Referencia de destino estable (opcional)', stableTargetPlaceholder: 'ID/referencia opaca estable',
      stableTargetHint: 'Use solo una referencia estable propia del evento; no codifique duración de Timer ni paths de TAG aquí.',
      noTarget: 'Este evento no requiere configuración de destino.',
      catalogUnavailable: 'El descubrimiento no está disponible; los IDs estables aún pueden informarse manualmente.'
    }
  } as const;
  return copies[locale];
}
