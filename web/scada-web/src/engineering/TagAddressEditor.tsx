import React, { useEffect, useMemo, useState } from 'react';
import type { DataSourceEngineering } from './types';
import type { EngineeringLocale } from './i18n';
import { applyModbusAddressBuild, metadataValue, parseCanonicalModbusAddress } from './TagAddressAssistant.logic';
import {
  resolveTagDataSource,
  updateManualTagAddress,
  type TagSourceAwareEngineering
} from './TagSourceSelector.logic';
import { buildModbusTagAddress } from './tagAddressApi';
import { OpcUaTagBrowser } from './OpcUaTagBrowser';
import { Dnp3TagAddressAssistant } from './Dnp3TagAddressAssistant';
import { Iec104TagAddressAssistant } from './Iec104TagAddressAssistant';
import { GenericTagBindingAssistant } from './GenericTagBindingAssistant';

type Props = {
  tag: TagSourceAwareEngineering;
  sources: readonly DataSourceEngineering[];
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
};

type AssistantContext = Readonly<{
  tag: TagSourceAwareEngineering;
  source: DataSourceEngineering;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
}>;

type AssistantRenderer = (context: AssistantContext) => React.ReactNode;

const valueTypes = ['', 'Boolean', 'Int16', 'UInt16', 'Int32', 'UInt32', 'Float32', 'Int64', 'UInt64', 'Float64'];
const wordOrders = ['', 'HighWordFirst', 'LowWordFirst'];

const specializedAssistants: Readonly<Record<string, AssistantRenderer>> = {
  'modbus.tcp': ({ tag, locale, onChange }) => (
    <ModbusAssistant tag={tag} locale={locale} onChange={onChange} />
  ),
  'opc-ua': ({ tag, source, locale, onChange }) => (
    <OpcUaTagBrowser tag={tag} source={source} locale={locale} onChange={onChange} />
  ),
  'dnp3.master': ({ tag, locale, onChange }) => (
    <Dnp3TagAddressAssistant tag={tag} locale={locale} onChange={onChange} />
  ),
  'iec60870.5.104': ({ tag, locale, onChange }) => (
    <Iec104TagAddressAssistant tag={tag} locale={locale} onChange={onChange} />
  )
};

export function TagAddressEditor({ tag, sources, locale, onChange }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const source = resolveTagDataSource(tag, sources).source;
  const driverType = source?.driver.trim().toLowerCase() ?? null;
  const specialized = driverType ? specializedAssistants[driverType] : undefined;
  const manualHelp = driverType ? manualHelpForDriver(driverType, text) : text.manualHelp;

  return (
    <>
      <label className="eng-editor-field">
        <span>{text.address}</span>
        <input
          className="mono"
          value={tag.address ?? ''}
          onChange={event => onChange(updateManualTagAddress(tag, emptyToNull(event.target.value)))}
          data-testid="tag-address-manual"
        />
        <small>{manualHelp}</small>
      </label>
      {source && specialized?.({ tag, source, locale, onChange })}
      {source && driverType && !specialized && (
        <GenericTagBindingAssistant
          tag={tag}
          driverType={driverType}
          locale={locale}
          onChange={onChange}
        />
      )}
    </>
  );
}

function manualHelpForDriver(driverType: string, text: ReturnType<typeof copy>): string {
  const byDriver: Readonly<Record<string, string>> = {
    'modbus.tcp': text.modbusManualHelp,
    'opc-ua': text.opcUaManualHelp,
    'dnp3.master': text.dnp3ManualHelp,
    'iec60870.5.104': text.iec104ManualHelp
  };
  return byDriver[driverType] ?? text.manualHelp;
}

function ModbusAssistant({ tag, locale, onChange }: {
  tag: TagSourceAwareEngineering;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
}) {
  const text = useMemo(() => copy(locale), [locale]);
  const canonical = parseCanonicalModbusAddress(tag.address);
  const [area, setArea] = useState(canonical?.area ?? 'holding');
  const [reference, setReference] = useState(canonical?.reference ?? '0');
  const [referenceBase, setReferenceBase] = useState<'zeroBased' | 'oneBased'>('zeroBased');
  const [unitId, setUnitId] = useState(metadataValue(tag, 'modbus.unitId'));
  const [valueType, setValueType] = useState(metadataValue(tag, 'modbus.valueType'));
  const [wordOrder, setWordOrder] = useState(metadataValue(tag, 'modbus.wordOrder'));
  const [scale, setScale] = useState(metadataValue(tag, 'modbus.scale'));
  const [offset, setOffset] = useState(metadataValue(tag, 'modbus.offset'));
  const [bitIndex, setBitIndex] = useState(tag.addressSelector?.kind === 'bit' ? String(tag.addressSelector.index) : '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastCanonical, setLastCanonical] = useState<string | null>(null);

  useEffect(() => {
    const parsed = parseCanonicalModbusAddress(tag.address);
    if (parsed) {
      setArea(parsed.area);
      if (referenceBase === 'zeroBased') setReference(parsed.reference);
    }
    setUnitId(metadataValue(tag, 'modbus.unitId'));
    setValueType(metadataValue(tag, 'modbus.valueType'));
    setWordOrder(metadataValue(tag, 'modbus.wordOrder'));
    setScale(metadataValue(tag, 'modbus.scale'));
    setOffset(metadataValue(tag, 'modbus.offset'));
    setBitIndex(tag.addressSelector?.kind === 'bit' ? String(tag.addressSelector.index) : '');
  }, [tag.address, tag.metadata, tag.addressSelector]);

  const apply = async () => {
    setBusy(true);
    setError(null);
    try {
      const numericReference = requiredInteger(reference, text.reference);
      const result = await buildModbusTagAddress({
        area,
        reference: numericReference,
        referenceBase,
        unitId: nullableInteger(unitId, text.unitId),
        valueType: valueType || null,
        wordOrder: wordOrder || null,
        scale: nullableNumber(scale, text.scale),
        offset: nullableNumber(offset, text.offset),
        bitIndex: nullableInteger(bitIndex, text.bit)
      });
      onChange(applyModbusAddressBuild(tag, result));
      setLastCanonical(result.address);
      if (referenceBase === 'oneBased') {
        const parsed = parseCanonicalModbusAddress(result.address);
        if (parsed) setReference(String(Number(parsed.reference) + 1));
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      setBusy(false);
    }
  };

  const areaReadOnly = area === 'discrete' || area === 'input';
  const bitAllowed = area === 'holding' || area === 'input';

  return (
    <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="modbus-address-assistant">
      <header>
        <strong>{text.modbusTitle}</strong>
        <span>{text.modbusHelp}</span>
      </header>
      <div className="eng-editor-form-grid">
        <label className="eng-editor-field">
          <span>{text.area}</span>
          <select value={area} onChange={event => setArea(event.target.value as typeof area)} data-testid="modbus-area">
            <option value="coil">Coil</option>
            <option value="discrete">Discrete Input</option>
            <option value="holding">Holding Register</option>
            <option value="input">Input Register</option>
          </select>
        </label>
        <label className="eng-editor-field">
          <span>{text.reference}</span>
          <input type="number" step="1" value={reference} onChange={event => setReference(event.target.value)} data-testid="modbus-reference" />
        </label>
        <label className="eng-editor-field">
          <span>{text.referenceBase}</span>
          <select value={referenceBase} onChange={event => setReferenceBase(event.target.value as typeof referenceBase)} data-testid="modbus-reference-base">
            <option value="zeroBased">{text.zeroBased}</option>
            <option value="oneBased">{text.oneBased}</option>
          </select>
        </label>
        <OptionalNumber label={text.unitId} value={unitId} onChange={setUnitId} integer />
        <label className="eng-editor-field">
          <span>{text.valueType}</span>
          <select value={valueType} onChange={event => setValueType(event.target.value)} data-testid="modbus-value-type">
            {valueTypes.map(value => <option key={value || 'auto'} value={value}>{value || text.auto}</option>)}
          </select>
        </label>
        <label className="eng-editor-field">
          <span>{text.wordOrder}</span>
          <select value={wordOrder} onChange={event => setWordOrder(event.target.value)} data-testid="modbus-word-order">
            {wordOrders.map(value => <option key={value || 'default'} value={value}>{value || text.defaultValue}</option>)}
          </select>
        </label>
        <OptionalNumber label={text.scale} value={scale} onChange={setScale} />
        <OptionalNumber label={text.offset} value={offset} onChange={setOffset} />
        {bitAllowed && <OptionalNumber label={text.bit} value={bitIndex} onChange={setBitIndex} integer />}
      </div>
      <div className="eng-editor-actions">
        <button type="button" className="secondary" onClick={() => void apply()} disabled={busy} data-testid="modbus-address-build">
          {busy ? text.building : text.build}
        </button>
      </div>
      {areaReadOnly && !tag.readOnly && <small role="alert">{text.readOnlyWarning}</small>}
      {lastCanonical && <small>{text.canonical}: <code>{lastCanonical}</code></small>}
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

function OptionalNumber({ label, value, onChange, integer = false }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  integer?: boolean;
}) {
  return (
    <label className="eng-editor-field">
      <span>{label}</span>
      <input type="number" step={integer ? '1' : 'any'} value={value} onChange={event => onChange(event.target.value)} />
    </label>
  );
}

function requiredInteger(value: string, label: string): number {
  if (!/^[+-]?\d+$/.test(value.trim())) throw new Error(`${label}: valor inteiro obrigatório.`);
  return Number(value);
}

function nullableInteger(value: string, label: string): number | null {
  if (!value.trim()) return null;
  if (!/^[+-]?\d+$/.test(value.trim())) throw new Error(`${label}: valor inteiro inválido.`);
  return Number(value);
}

function nullableNumber(value: string, label: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) throw new Error(`${label}: valor numérico inválido.`);
  return parsed;
}

function emptyToNull(value: string): string | null {
  return value.trim() ? value : null;
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    address: 'Address', manualHelp: 'Use the portable address format required by the selected Driver.',
    modbusManualHelp: "Canonical manual syntax is area:0-based-offset, for example 'holding:0'.",
    opcUaManualHelp: "Manual OPC UA accepts the canonical portable address, for example 'node=ns%3D2%3Bs%3DTemperature'. Legacy raw NodeId remains available for migration.",
    dnp3ManualHelp: "Canonical DNP3 syntax is 'dnp3:<pointKind>:<index>', for example 'dnp3:analogInput:0'.",
    iec104ManualHelp: "Canonical IEC-104 identity is 'ca=<0..65535>;ioa=<0..16777215>'. The assistant also authors the required Type ID binding.",
    modbusTitle: 'Modbus address assistant', modbusHelp: 'Build the same canonical address consumed by Runtime. Reference base is explicit; no 40001-style guessing is performed.',
    area: 'Data area', reference: 'Reference', referenceBase: 'Reference base', zeroBased: '0-based offset', oneBased: '1-based reference',
    unitId: 'Unit ID override', valueType: 'Value type', wordOrder: 'Word order', scale: 'Scale', offset: 'Offset', bit: 'Bit index',
    auto: 'Infer from TAG', defaultValue: 'Driver default', build: 'Use assisted address', building: 'Building...', canonical: 'Canonical address',
    readOnlyWarning: 'This Modbus area is read-only. Mark the TAG as read-only before Preview/Apply.'
  };
  if (locale === 'es') return {
    address: 'Dirección', manualHelp: 'Use el formato de dirección portátil requerido por el Driver seleccionado.',
    modbusManualHelp: "La sintaxis manual canónica es área:offset-base-0, por ejemplo 'holding:0'.",
    opcUaManualHelp: "OPC UA manual acepta la dirección portátil canónica, por ejemplo 'node=ns%3D2%3Bs%3DTemperature'. El NodeId crudo legado sigue disponible para migración.",
    dnp3ManualHelp: "La sintaxis DNP3 canónica es 'dnp3:<pointKind>:<index>', por ejemplo 'dnp3:analogInput:0'.",
    iec104ManualHelp: "La identidad IEC-104 canónica es 'ca=<0..65535>;ioa=<0..16777215>'. El asistente también configura el Type ID requerido.",
    modbusTitle: 'Asistente de dirección Modbus', modbusHelp: 'Construye la misma dirección canónica consumida por Runtime. La base es explícita y no se adivina la notación 40001.',
    area: 'Área de datos', reference: 'Referencia', referenceBase: 'Base de referencia', zeroBased: 'Offset base 0', oneBased: 'Referencia base 1',
    unitId: 'Override Unit ID', valueType: 'Tipo de valor', wordOrder: 'Orden de palabras', scale: 'Escala', offset: 'Offset', bit: 'Índice de bit',
    auto: 'Inferir del TAG', defaultValue: 'Default del Driver', build: 'Usar dirección asistida', building: 'Construyendo...', canonical: 'Dirección canónica',
    readOnlyWarning: 'Esta área Modbus es de solo lectura. Marque el TAG como read-only antes de Preview/Apply.'
  };
  return {
    address: 'Endereço', manualHelp: 'Use o formato de endereço portátil exigido pelo Driver selecionado.',
    modbusManualHelp: "A sintaxe manual canônica é área:offset-base-0, por exemplo 'holding:0'.",
    opcUaManualHelp: "OPC UA manual aceita o endereço portátil canônico, por exemplo 'node=ns%3D2%3Bs%3DTemperature'. O NodeId cru legado continua disponível para migração.",
    dnp3ManualHelp: "A sintaxe DNP3 canônica é 'dnp3:<pointKind>:<index>', por exemplo 'dnp3:analogInput:0'.",
    iec104ManualHelp: "A identidade IEC-104 canônica é 'ca=<0..65535>;ioa=<0..16777215>'. O assistente também configura o Type ID obrigatório.",
    modbusTitle: 'Assistente de endereço Modbus', modbusHelp: 'Monta o mesmo endereço canônico consumido pelo Runtime. A base é explícita e nenhuma notação 40001 é adivinhada.',
    area: 'Área de dados', reference: 'Referência', referenceBase: 'Base da referência', zeroBased: 'Offset base 0', oneBased: 'Referência base 1',
    unitId: 'Override de Unit ID', valueType: 'Tipo do valor', wordOrder: 'Ordem de words', scale: 'Escala', offset: 'Offset', bit: 'Índice do bit',
    auto: 'Inferir pelo TAG', defaultValue: 'Padrão do Driver', build: 'Usar endereço assistido', building: 'Montando...', canonical: 'Endereço canônico',
    readOnlyWarning: 'Esta área Modbus é somente leitura. Marque o TAG como read-only antes do Preview/Apply.'
  };
}
