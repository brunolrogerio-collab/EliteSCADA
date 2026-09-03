import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import {
  loadTagBindingDefinition,
  requireAllowedTagBindingValue,
  requireTagBindingField
} from './TagBindingSchema';
import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';

type Props = {
  tag: TagSourceAwareEngineering;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
};

const pointKinds = [
  'binaryInput',
  'doubleBitBinaryInput',
  'analogInput',
  'counter',
  'frozenCounter',
  'binaryOutputStatus',
  'analogOutputStatus'
] as const;

const DNP3_DRIVER_TYPE = 'dnp3.master';

type PointKind = typeof pointKinds[number];

export function Dnp3TagAddressAssistant({ tag, locale, onChange }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const parsed = parseDnp3Address(tag.address);
  const [pointKind, setPointKind] = useState<PointKind>(parsed?.pointKind ?? 'analogInput');
  const [index, setIndex] = useState(parsed?.index ?? '0');
  const [writable, setWritable] = useState(!tag.readOnly && isOutputKind(parsed?.pointKind ?? 'analogInput'));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const apply = async () => {
    setError(null);
    if (!/^\d+$/.test(index.trim())) {
      setError(text.indexInvalid);
      return;
    }
    const numericIndex = Number(index);
    if (!Number.isInteger(numericIndex) || numericIndex < 0 || numericIndex > 65535) {
      setError(text.indexInvalid);
      return;
    }

    setBusy(true);
    try {
      const definition = await loadTagBindingDefinition(DNP3_DRIVER_TYPE);
      requireAllowedTagBindingValue(definition, 'pointKind', pointKind);
      const indexField = requireTagBindingField(definition, 'index');
      if ((indexField.minimum != null && numericIndex < indexField.minimum) ||
          (indexField.maximum != null && numericIndex > indexField.maximum)) {
        throw new Error(text.indexInvalid);
      }

      const canWrite = writable && isOutputKind(pointKind);
      const address = `dnp3:${pointKind}:${numericIndex}`;
      const settings: Record<string, string> = {
        pointKind,
        index: String(numericIndex),
        writable: String(canWrite).toLowerCase()
      };
      requireTagBindingField(definition, 'writable');
      if (canWrite) {
        requireAllowedTagBindingValue(definition, 'commandMode', 'selectBeforeOperate');
        settings.commandMode = 'selectBeforeOperate';
      }

      onChange({
        ...tag,
        address,
        dataType: compatibleDataType(pointKind, tag.dataType),
        readOnly: !canWrite,
        addressSelector: null,
        communicationBinding: {
          contractVersion: 1,
          schemaId: definition.identity.schemaId,
          schemaVersion: definition.identity.schemaVersion,
          portableAddress: address,
          settings
        }
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="dnp3-address-assistant">
      <header><strong>{text.title}</strong><span>{text.help}</span></header>
      <div className="eng-editor-form-grid">
        <label className="eng-editor-field">
          <span>{text.kind}</span>
          <select value={pointKind} onChange={event => {
            const next = event.target.value as PointKind;
            setPointKind(next);
            if (!isOutputKind(next)) setWritable(false);
          }} data-testid="dnp3-point-kind">
            {pointKinds.map(kind => <option key={kind} value={kind}>{kind}</option>)}
          </select>
        </label>
        <label className="eng-editor-field">
          <span>{text.index}</span>
          <input type="number" min="0" max="65535" step="1" value={index} onChange={event => setIndex(event.target.value)} data-testid="dnp3-index" />
        </label>
        {isOutputKind(pointKind) && <label className="eng-editor-field">
          <span>{text.writable}</span>
          <input type="checkbox" checked={writable} onChange={event => setWritable(event.target.checked)} data-testid="dnp3-writable" />
        </label>}
      </div>
      <div className="eng-editor-actions">
        <button type="button" className="secondary" onClick={() => void apply()} disabled={busy} data-testid="dnp3-address-build">{busy ? text.applying : text.apply}</button>
      </div>
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

export function parseDnp3Address(value?: string | null): { pointKind: PointKind; index: string } | null {
  const match = /^dnp3:(binaryInput|doubleBitBinaryInput|analogInput|counter|frozenCounter|binaryOutputStatus|analogOutputStatus):(\d+)$/.exec(value ?? '');
  if (!match) return null;
  return { pointKind: match[1] as PointKind, index: match[2] };
}

function isOutputKind(kind: PointKind): boolean {
  return kind === 'binaryOutputStatus' || kind === 'analogOutputStatus';
}

function compatibleDataType(kind: PointKind, current: string): string {
  if (kind === 'binaryInput' || kind === 'binaryOutputStatus') return 'boolean';
  if (kind === 'doubleBitBinaryInput') return 'enum';
  if (kind === 'counter' || kind === 'frozenCounter') return current === 'int32' || current === 'int64' ? current : 'int64';
  return ['int16', 'int32', 'float', 'double'].includes(current) ? current : 'float';
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'DNP3 address assistant', help: 'Builds the canonical DNP3 point identity used by Runtime. Advanced variations and command tuning remain editable through the canonical binding contract.',
    kind: 'Point kind', index: 'Point index', writable: 'Writable output', apply: 'Use assisted address', applying: 'Applying...', indexInvalid: 'DNP3 point index must be an integer from 0 to 65535.'
  };
  if (locale === 'es') return {
    title: 'Asistente de dirección DNP3', help: 'Construye la identidad canónica del punto DNP3 usada por Runtime. Variaciones avanzadas y comandos permanecen en el binding canónico.',
    kind: 'Tipo de punto', index: 'Índice del punto', writable: 'Salida escribible', apply: 'Usar dirección asistida', applying: 'Aplicando...', indexInvalid: 'El índice DNP3 debe ser un entero entre 0 y 65535.'
  };
  return {
    title: 'Assistente de endereço DNP3', help: 'Monta a identidade canônica do ponto DNP3 usada pelo Runtime. Variações avançadas e ajustes de comando permanecem no binding canônico.',
    kind: 'Tipo de ponto', index: 'Índice do ponto', writable: 'Saída gravável', apply: 'Usar endereço assistido', applying: 'Aplicando...', indexInvalid: 'O índice DNP3 deve ser inteiro entre 0 e 65535.'
  };
}
