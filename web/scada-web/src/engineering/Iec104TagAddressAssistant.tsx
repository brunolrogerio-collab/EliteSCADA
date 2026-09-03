import React, { useMemo, useState } from 'react';
import type { EngineeringLocale } from './i18n';
import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';

type Props = {
  tag: TagSourceAwareEngineering;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
};

const monitoredTypes = [
  ['MSpNa1', 'boolean'], ['MSpTb1', 'boolean'],
  ['MDpNa1', 'enum'], ['MDpTb1', 'enum'],
  ['MBoNa1', 'int32'], ['MBoTb1', 'int32'],
  ['MMeNa1', 'float'], ['MMeTd1', 'float'],
  ['MMeNb1', 'int16'], ['MMeTe1', 'int16'],
  ['MMeNc1', 'float'], ['MMeTf1', 'float']
] as const;

const commandTypes = [
  ['CScNa1', 'boolean'],
  ['CDcNa1', 'enum'],
  ['CSeNa1', 'float'],
  ['CSeNb1', 'int16'],
  ['CSeNc1', 'float']
] as const;

export function Iec104TagAddressAssistant({ tag, locale, onChange }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const parsed = parseIec104Address(tag.address);
  const currentType = tag.communicationBinding?.settings?.['iec104.typeId'];
  const [commonAddress, setCommonAddress] = useState(parsed?.commonAddress ?? '1');
  const [ioa, setIoa] = useState(parsed?.ioa ?? '0');
  const [typeId, setTypeId] = useState(currentType && monitoredTypes.some(([name]) => name === currentType) ? currentType : 'MMeNc1');
  const [writable, setWritable] = useState(!tag.readOnly);
  const currentCommandType = tag.communicationBinding?.settings?.['iec104.commandTypeId'];
  const [commandTypeId, setCommandTypeId] = useState(currentCommandType && commandTypes.some(([name]) => name === currentCommandType) ? currentCommandType : 'CSeNc1');
  const [commandMode, setCommandMode] = useState(tag.communicationBinding?.settings?.['iec104.commandMode'] ?? 'sbo');
  const [qualifier, setQualifier] = useState(tag.communicationBinding?.settings?.['iec104.qualifier'] ?? '0');
  const [error, setError] = useState<string | null>(null);

  const apply = () => {
    setError(null);
    const ca = integerInRange(commonAddress, 0, 65535);
    const point = integerInRange(ioa, 0, 16777215);
    if (ca == null) { setError(text.commonAddressInvalid); return; }
    if (point == null) { setError(text.ioaInvalid); return; }

    const monitored = monitoredTypes.find(([name]) => name === typeId);
    if (!monitored) { setError(text.typeInvalid); return; }
    const dataType = monitored[1];
    const settings: Record<string, string> = { 'iec104.typeId': typeId };

    if (writable) {
      const command = commandTypes.find(([name]) => name === commandTypeId);
      if (!command || command[1] !== dataType) { setError(text.commandTypeInvalid); return; }
      const qoc = integerInRange(qualifier, 0, 31);
      if (qoc == null) { setError(text.qualifierInvalid); return; }
      settings['iec104.commandTypeId'] = commandTypeId;
      settings['iec104.commandMode'] = commandMode;
      settings['iec104.qualifier'] = String(qoc);
    }

    const address = `ca=${ca};ioa=${point}`;
    onChange({
      ...tag,
      address,
      dataType,
      readOnly: !writable,
      addressSelector: null,
      communicationBinding: {
        contractVersion: 1,
        schemaId: 'elite.iec60870.5.104.point',
        schemaVersion: 1,
        portableAddress: address,
        settings
      }
    });
  };

  const compatibleCommands = commandTypes.filter(([, dataType]) =>
    dataType === monitoredTypes.find(([name]) => name === typeId)?.[1]);

  return (
    <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="iec104-address-assistant">
      <header><strong>{text.title}</strong><span>{text.help}</span></header>
      <div className="eng-editor-form-grid">
        <label className="eng-editor-field"><span>{text.commonAddress}</span><input type="number" min="0" max="65535" step="1" value={commonAddress} onChange={event => setCommonAddress(event.target.value)} data-testid="iec104-common-address" /></label>
        <label className="eng-editor-field"><span>{text.ioa}</span><input type="number" min="0" max="16777215" step="1" value={ioa} onChange={event => setIoa(event.target.value)} data-testid="iec104-ioa" /></label>
        <label className="eng-editor-field"><span>{text.typeId}</span><select value={typeId} onChange={event => {
          const next = event.target.value;
          setTypeId(next);
          const nextType = monitoredTypes.find(([name]) => name === next)?.[1];
          const compatible = commandTypes.find(([, dataType]) => dataType === nextType);
          if (compatible) setCommandTypeId(compatible[0]);
        }} data-testid="iec104-type-id">{monitoredTypes.map(([name]) => <option key={name} value={name}>{name}</option>)}</select></label>
        <label className="eng-editor-field"><span>{text.writable}</span><input type="checkbox" checked={writable} onChange={event => setWritable(event.target.checked)} data-testid="iec104-writable" /></label>
        {writable && <>
          <label className="eng-editor-field"><span>{text.commandType}</span><select value={commandTypeId} onChange={event => setCommandTypeId(event.target.value)} data-testid="iec104-command-type">{compatibleCommands.map(([name]) => <option key={name} value={name}>{name}</option>)}</select></label>
          <label className="eng-editor-field"><span>{text.commandMode}</span><select value={commandMode} onChange={event => setCommandMode(event.target.value)} data-testid="iec104-command-mode"><option value="sbo">SBO</option><option value="direct">Direct Operate</option></select></label>
          <label className="eng-editor-field"><span>{text.qualifier}</span><input type="number" min="0" max="31" step="1" value={qualifier} onChange={event => setQualifier(event.target.value)} data-testid="iec104-qualifier" /></label>
        </>}
      </div>
      <div className="eng-editor-actions"><button type="button" className="secondary" onClick={apply} data-testid="iec104-address-build">{text.apply}</button></div>
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

export function parseIec104Address(value?: string | null): { commonAddress: string; ioa: string } | null {
  const match = /^ca=(\d+);ioa=(\d+)$/.exec(value ?? '');
  if (!match) return null;
  return { commonAddress: match[1], ioa: match[2] };
}

function integerInRange(value: string, minimum: number, maximum: number): number | null {
  if (!/^\d+$/.test(value.trim())) return null;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed >= minimum && parsed <= maximum ? parsed : null;
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'IEC-104 address assistant', help: 'Builds canonical CA/IOA identity plus the monitored Type ID required by Runtime. Writable points require an explicit compatible command profile.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Monitored Type ID', writable: 'Writable command point', commandType: 'Command Type ID', commandMode: 'Command mode', qualifier: 'Qualifier (QOC)', apply: 'Use assisted address',
    commonAddressInvalid: 'Common Address must be an integer from 0 to 65535.', ioaInvalid: 'IOA must be an integer from 0 to 16777215.', typeInvalid: 'Select a supported monitored IEC-104 Type ID.', commandTypeInvalid: 'Command Type ID must use the same canonical TAG data type as the monitored Type ID.', qualifierInvalid: 'Command qualifier must be an integer from 0 to 31.'
  };
  if (locale === 'es') return {
    title: 'Asistente de dirección IEC-104', help: 'Construye la identidad canónica CA/IOA y el Type ID monitoreado exigido por Runtime. Los puntos escribibles requieren un perfil de comando compatible explícito.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Type ID monitoreado', writable: 'Punto de comando escribible', commandType: 'Command Type ID', commandMode: 'Modo de comando', qualifier: 'Qualifier (QOC)', apply: 'Usar dirección asistida',
    commonAddressInvalid: 'Common Address debe ser un entero entre 0 y 65535.', ioaInvalid: 'IOA debe ser un entero entre 0 y 16777215.', typeInvalid: 'Seleccione un Type ID IEC-104 monitoreado compatible.', commandTypeInvalid: 'Command Type ID debe usar el mismo tipo canónico de TAG que el Type ID monitoreado.', qualifierInvalid: 'El qualifier debe ser un entero entre 0 y 31.'
  };
  return {
    title: 'Assistente de endereço IEC-104', help: 'Monta a identidade canônica CA/IOA e o Type ID monitorado exigido pelo Runtime. Pontos graváveis exigem perfil de comando compatível explícito.',
    commonAddress: 'Common Address', ioa: 'Information Object Address (IOA)', typeId: 'Type ID monitorado', writable: 'Ponto de comando gravável', commandType: 'Command Type ID', commandMode: 'Modo de comando', qualifier: 'Qualifier (QOC)', apply: 'Usar endereço assistido',
    commonAddressInvalid: 'Common Address deve ser inteiro entre 0 e 65535.', ioaInvalid: 'IOA deve ser inteiro entre 0 e 16777215.', typeInvalid: 'Selecione um Type ID IEC-104 monitorado suportado.', commandTypeInvalid: 'O Command Type ID deve usar o mesmo tipo canônico de TAG do Type ID monitorado.', qualifierInvalid: 'O qualifier deve ser inteiro entre 0 e 31.'
  };
}
