import React, { useEffect, useMemo, useState } from 'react';
import {
  applyEngineeringPackage,
  loadEngineeringWorkspace,
  previewEngineeringPackage
} from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView, ImportPreviewView, TagEngineering } from './types';
import './engineering-mutations.css';

const CLIENT_MEMORY_DRIVER = 'builtin.memory.client';
const SERVER_MEMORY_DRIVER = 'builtin.memory.server';

type Props = {
  model: EngineeringPackageView;
  locale: EngineeringLocale;
};

type MemoryTagView = {
  identity: string;
  tag: TagEngineering;
  sourceName: string;
  sourceDriver: string;
};

export function MemoryTagSettingsPanel({ model, locale }: Props) {
  const text = labels(locale);
  const memoryTags = useMemo(() => collectMemoryTags(model), [model]);
  const [selectedIdentity, setSelectedIdentity] = useState(memoryTags[0]?.identity ?? '');
  const selected = memoryTags.find(item => item.identity === selectedIdentity) ?? null;
  const [valueText, setValueText] = useState(() => selected ? formatInitialValue(selected.tag) : '');
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [candidate, setCandidate] = useState<EngineeringPackageView | null>(null);
  const [validatedChangeVersion, setValidatedChangeVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [applying, setApplying] = useState(false);

  useEffect(() => {
    if (memoryTags.some(item => item.identity === selectedIdentity)) return;
    setSelectedIdentity(memoryTags[0]?.identity ?? '');
  }, [memoryTags, selectedIdentity]);

  useEffect(() => {
    setValueText(selected ? formatInitialValue(selected.tag) : '');
    invalidate();
  }, [selectedIdentity, selected?.tag.initialValue?.value, selected?.tag.dataType]);

  if (memoryTags.length === 0) return null;

  const runPreview = async () => {
    if (!selected) return;
    setPreviewing(true);
    setError(null);
    setPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);

    try {
      const parsed = parseInitialValue(selected.tag.dataType, valueText, text);
      const before = await loadEngineeringWorkspace();
      const next = clone(model);
      next.tags = next.tags.map(tag =>
        tagIdentity(tag) === selected.identity
          ? { ...tag, initialValue: { dataType: tag.dataType, value: parsed } }
          : tag);
      const nextPreview = await previewEngineeringPackage(next);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion)
        throw new Error(text.workspaceChanged);

      setPreview(nextPreview);
      setCandidate(next);
      setValidatedChangeVersion(after.changeVersion);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setPreviewing(false);
    }
  };

  const runApply = async () => {
    if (!candidate || validatedChangeVersion === null || !preview?.canApply) return;
    setApplying(true);
    setError(null);
    try {
      await applyEngineeringPackage(candidate, validatedChangeVersion);
      window.location.reload();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      setPreview(null);
      setCandidate(null);
      setValidatedChangeVersion(null);
    } finally {
      setApplying(false);
    }
  };

  const invalidate = () => {
    setPreview(null);
    setCandidate(null);
    setValidatedChangeVersion(null);
    setError(null);
  };

  return (
    <section className="eng-mutation-panel" data-testid="memory-engineering-panel">
      <header className="eng-mutation-header">
        <div>
          <span>{text.eyebrow}</span>
          <h2>{text.title}</h2>
          <p>{text.description}</p>
        </div>
        <div className="eng-mutation-warning">{text.warning}</div>
      </header>

      <div className="eng-mutation-grid">
        <section className="eng-mutation-card">
          <header>
            <strong>{text.tag}</strong>
            <span>{text.tagHint}</span>
          </header>
          <label className="eng-mutation-field">
            <span>{text.tag}</span>
            <select
              value={selectedIdentity}
              onChange={event => {
                setSelectedIdentity(event.target.value);
                invalidate();
              }}
              disabled={previewing || applying}
            >
              {memoryTags.map(item => (
                <option value={item.identity} key={item.identity}>{item.tag.path}</option>
              ))}
            </select>
          </label>
          {selected && (
            <>
              <code className="eng-mutation-detail">{selected.sourceDriver} · {selected.sourceName}</code>
              <code className="eng-mutation-detail">{selected.tag.dataType} · {selected.tag.readOnly ? text.readOnly : text.writable}</code>
            </>
          )}
        </section>

        <section className="eng-mutation-card eng-bulk-card">
          <header>
            <strong>{text.initialValue}</strong>
            <span>{text.initialHint}</span>
          </header>
          <label className="eng-mutation-field">
            <span>{text.value}</span>
            {selected?.tag.dataType.toLowerCase() === 'boolean' ? (
              <select
                value={valueText}
                onChange={event => { setValueText(event.target.value); invalidate(); }}
                disabled={previewing || applying}
                data-testid="memory-initial-value"
              >
                <option value="false">false</option>
                <option value="true">true</option>
              </select>
            ) : (
              <input
                value={valueText}
                onChange={event => { setValueText(event.target.value); invalidate(); }}
                disabled={previewing || applying}
                data-testid="memory-initial-value"
              />
            )}
          </label>

          <div className="eng-mutation-actions">
            <button
              type="button"
              className="secondary"
              disabled={!selected || previewing || applying}
              onClick={() => void runPreview()}
              data-testid="memory-initial-preview"
            >
              {previewing ? text.previewing : text.preview}
            </button>
            <button
              type="button"
              className="primary"
              disabled={!preview?.canApply || applying || previewing}
              onClick={() => void runApply()}
              data-testid="memory-initial-apply"
            >
              {applying ? text.applying : text.apply}
            </button>
          </div>

          {preview && (
            <div className={preview.canApply ? 'eng-bulk-preview valid' : 'eng-bulk-preview invalid'}>
              <strong>{preview.canApply ? text.valid : text.invalid}</strong>
              <span>{text.updates}: <b>{preview.updateCount}</b></span>
              <span>{text.errors}: <b>{preview.errorCount}</b></span>
            </div>
          )}
        </section>
      </div>

      {error && <pre className="eng-mutation-error" aria-live="polite">{error}</pre>}
    </section>
  );
}

function collectMemoryTags(model: EngineeringPackageView): MemoryTagView[] {
  const sources = new Map(
    (model.dataSources ?? []).map(source => [source.key.toLowerCase(), source]));

  return model.tags
    .map(tag => {
      const source = tag.source ? sources.get(tag.source.toLowerCase()) : undefined;
      if (!source) return null;
      const driver = source.driver.toLowerCase();
      if (driver !== CLIENT_MEMORY_DRIVER && driver !== SERVER_MEMORY_DRIVER) return null;
      return {
        identity: tagIdentity(tag),
        tag,
        sourceName: source.name,
        sourceDriver: source.driver
      } satisfies MemoryTagView;
    })
    .filter((item): item is MemoryTagView => item !== null)
    .sort((left, right) => left.tag.path.localeCompare(right.tag.path));
}

function tagIdentity(tag: TagEngineering): string {
  return tag.id ? `id:${tag.id}` : `path:${tag.path}`;
}

function formatInitialValue(tag: TagEngineering): string {
  const value = tag.initialValue?.value;
  if (value === undefined || value === null) return defaultInitialText(tag.dataType);
  if (typeof value === 'string') return value;
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  return String(value);
}

function defaultInitialText(dataType: string): string {
  switch (dataType.toLowerCase()) {
    case 'boolean': return 'false';
    case 'string': return '';
    case 'datetime': return '1970-01-01T00:00:00Z';
    default: return '0';
  }
}

function parseInitialValue(dataType: string, raw: string, text: ReturnType<typeof labels>): unknown {
  const normalized = dataType.toLowerCase();
  if (normalized === 'string') return raw;
  if (normalized === 'boolean') {
    if (raw === 'true') return true;
    if (raw === 'false') return false;
    throw new Error(text.invalidBoolean);
  }
  if (normalized === 'datetime') {
    if (Number.isNaN(Date.parse(raw))) throw new Error(text.invalidDateTime);
    return raw;
  }

  const numeric = Number(raw);
  if (!Number.isFinite(numeric)) throw new Error(text.invalidNumber);

  if (normalized === 'float' || normalized === 'double') return numeric;
  if (!Number.isInteger(numeric)) throw new Error(text.invalidInteger);

  if (normalized === 'int16' && (numeric < -32768 || numeric > 32767))
    throw new Error(text.outOfRange);
  if ((normalized === 'int32' || normalized === 'enum') && (numeric < -2147483648 || numeric > 2147483647))
    throw new Error(text.outOfRange);
  if (normalized === 'int64' && !Number.isSafeInteger(numeric))
    throw new Error(text.int64SafeRange);

  return numeric;
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function labels(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Internal Memory Engineering', title: 'Typed startup value',
    description: 'Configure the public Engineering initial/default value used by Client Memory on each new client and by Server Memory when no compatible retained value exists.',
    warning: 'No network address is used. Server Memory retained runtime values are separate from Engineering.',
    tag: 'Memory TAG', tagHint: 'Only TAGs owned by builtin.memory.client or builtin.memory.server are listed.',
    readOnly: 'read-only', writable: 'writable', initialValue: 'Initial/default value',
    initialHint: 'Preview validates the value through the canonical Engineering schema before Apply.', value: 'Value',
    preview: 'Preview value', previewing: 'Previewing...', apply: 'Apply to Workspace', applying: 'Applying...',
    valid: 'Valid Engineering candidate', invalid: 'Invalid Engineering candidate', updates: 'Updates', errors: 'Errors',
    workspaceChanged: 'The Engineering Workspace changed while validating this value. Reload and validate again.',
    invalidBoolean: 'Boolean initial value must be true or false.', invalidDateTime: 'DateTime initial value is invalid.',
    invalidNumber: 'Numeric initial value is invalid.', invalidInteger: 'Integer initial value must not contain decimals.',
    outOfRange: 'Initial value is outside the selected TAG data type range.',
    int64SafeRange: 'The browser editor only accepts Int64 values inside JavaScript safe-integer range. Use canonical JSON/CSV for larger exact Int64 values.'
  };
  if (locale === 'es') return {
    eyebrow: 'Ingeniería de Memoria Interna', title: 'Valor inicial tipado',
    description: 'Configure el valor inicial por defecto de Engineering usado por Client Memory en cada nuevo cliente y por Server Memory cuando no existe un valor retenido compatible.',
    warning: 'No se usa dirección de red. Los valores retenidos de Server Memory permanecen separados de Engineering.',
    tag: 'TAG de memoria', tagHint: 'Solo se muestran TAGs de builtin.memory.client o builtin.memory.server.',
    readOnly: 'solo lectura', writable: 'escribible', initialValue: 'Valor inicial/por defecto',
    initialHint: 'Preview valida el valor mediante el esquema canónico antes de Aplicar.', value: 'Valor',
    preview: 'Preview del valor', previewing: 'Validando...', apply: 'Aplicar al Workspace', applying: 'Aplicando...',
    valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', updates: 'Actualizaciones', errors: 'Errores',
    workspaceChanged: 'El Engineering Workspace cambió durante la validación. Recargue y valide nuevamente.',
    invalidBoolean: 'El valor booleano debe ser true o false.', invalidDateTime: 'El valor DateTime no es válido.',
    invalidNumber: 'El valor numérico no es válido.', invalidInteger: 'El valor entero no puede contener decimales.',
    outOfRange: 'El valor inicial está fuera del rango del tipo de TAG seleccionado.',
    int64SafeRange: 'El editor web solo acepta Int64 dentro del rango entero seguro de JavaScript. Use JSON/CSV canónico para valores Int64 exactos mayores.'
  };
  return {
    eyebrow: 'Engineering de Memória Interna', title: 'Valor inicial tipado',
    description: 'Configure o valor inicial/padrão público de Engineering usado pelo Client Memory em cada novo cliente e pelo Server Memory quando não existe valor retido compatível.',
    warning: 'Não há endereço de rede. Valores retidos do Server Memory permanecem separados do Engineering.',
    tag: 'TAG de memória', tagHint: 'Somente TAGs de builtin.memory.client ou builtin.memory.server são exibidas.',
    readOnly: 'somente leitura', writable: 'gravável', initialValue: 'Valor inicial/padrão',
    initialHint: 'O Preview valida o valor pelo schema canônico de Engineering antes do Apply.', value: 'Valor',
    preview: 'Preview do valor', previewing: 'Validando...', apply: 'Aplicar ao Workspace', applying: 'Aplicando...',
    valid: 'Candidato Engineering válido', invalid: 'Candidato Engineering inválido', updates: 'Atualizações', errors: 'Erros',
    workspaceChanged: 'O Engineering Workspace mudou durante a validação. Recarregue e valide novamente.',
    invalidBoolean: 'O valor booleano deve ser true ou false.', invalidDateTime: 'O valor DateTime é inválido.',
    invalidNumber: 'O valor numérico é inválido.', invalidInteger: 'O valor inteiro não pode conter casas decimais.',
    outOfRange: 'O valor inicial está fora da faixa do tipo de TAG selecionado.',
    int64SafeRange: 'O editor web aceita Int64 apenas dentro da faixa inteira segura do JavaScript. Use JSON/CSV canônico para valores Int64 exatos maiores.'
  };
}
