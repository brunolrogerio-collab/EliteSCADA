import React from 'react';
import type {
  DynamoParameterDefinitionEngineering,
  DynamoParameterKindEngineering
} from '../../../runtime/visual-navigation/runtimeVisualNavigationModel';
import type { EngineeringLocale } from '../../i18n';

type Props = Readonly<{
  parameters: readonly DynamoParameterDefinitionEngineering[];
  locale: EngineeringLocale;
  disabled?: boolean;
  onChange: (parameters: readonly DynamoParameterDefinitionEngineering[]) => void;
}>;

const KINDS: readonly DynamoParameterKindEngineering[] = Object.freeze([
  'TagReference',
  'Boolean',
  'Number',
  'String',
  'EquipmentPath'
]);

export function DynamoDefinitionParametersEditor({
  parameters,
  locale,
  disabled = false,
  onChange
}: Props) {
  const text = copy(locale);

  const replace = (index: number, update: DynamoParameterDefinitionEngineering) => {
    onChange(Object.freeze(parameters.map((parameter, current) =>
      current === index ? Object.freeze(update) : parameter)));
  };

  const add = () => {
    const used = new Set(parameters.map(parameter => parameter.key.trim().toLocaleLowerCase('en-US')));
    let index = parameters.length + 1;
    let key = `parameter${index}`;
    while (used.has(key.toLocaleLowerCase('en-US'))) {
      index += 1;
      key = `parameter${index}`;
    }
    onChange(Object.freeze([
      ...parameters,
      Object.freeze({
        key,
        kind: 'Boolean' as const,
        required: false,
        version: 1
      })
    ]));
  };

  const remove = (index: number) =>
    onChange(Object.freeze(parameters.filter((_, current) => current !== index)));

  return <section className="visual-editor-dynamo-definition-parameters" data-testid="dynamo-definition-parameters">
    <header>
      <div>
        <strong>{text.title}</strong>
        <small>{text.hint}</small>
      </div>
      <button type="button" className="secondary" disabled={disabled} onClick={add}>+ {text.add}</button>
    </header>

    {parameters.length === 0
      ? <p>{text.empty}</p>
      : <div className="visual-editor-dynamo-definition-parameters__list">
        {parameters.map((parameter, index) => <div
          className="visual-editor-dynamo-definition-parameter"
          key={`${parameter.key}-${index}`}
          data-parameter-kind={parameter.kind}
        >
          <label>
            <span>{text.key}</span>
            <input
              className="mono"
              value={parameter.key}
              disabled={disabled}
              onChange={event => replace(index, {
                ...parameter,
                key: event.currentTarget.value,
                version: parameter.version ?? 1
              })}
            />
          </label>

          <label>
            <span>{text.kind}</span>
            <select
              value={parameter.kind}
              disabled={disabled}
              onChange={event => {
                const kind = event.currentTarget.value as DynamoParameterKindEngineering;
                replace(index, resetPayloadForKind(parameter, kind));
              }}
            >
              {KINDS.map(kind => <option key={kind} value={kind}>{kindLabel(kind, locale)}</option>)}
            </select>
          </label>

          <label className="visual-editor-dynamo-definition-parameter__required">
            <input
              type="checkbox"
              checked={parameter.required === true}
              disabled={disabled}
              onChange={event => replace(index, {
                ...parameter,
                required: event.currentTarget.checked,
                version: parameter.version ?? 1
              })}
            />
            <span>{text.required}</span>
          </label>

          <DefaultValueEditor
            parameter={parameter}
            locale={locale}
            disabled={disabled}
            onChange={next => replace(index, next)}
          />

          <button type="button" className="secondary" disabled={disabled} onClick={() => remove(index)}>
            {text.remove}
          </button>
        </div>)}
      </div>}
  </section>;
}

function DefaultValueEditor({
  parameter,
  locale,
  disabled,
  onChange
}: Readonly<{
  parameter: DynamoParameterDefinitionEngineering;
  locale: EngineeringLocale;
  disabled: boolean;
  onChange: (parameter: DynamoParameterDefinitionEngineering) => void;
}>) {
  const text = copy(locale);
  if (parameter.kind === 'TagReference') {
    return <div className="visual-editor-dynamo-definition-parameter__default">
      <span>{text.defaultValue}</span>
      <small>{text.tagHint}</small>
    </div>;
  }

  if (parameter.kind === 'Boolean') {
    const hasDefault = typeof parameter.defaultValue === 'boolean';
    return <div className="visual-editor-dynamo-definition-parameter__default">
      <label>
        <span>{text.defaultValue}</span>
        <select
          value={hasDefault ? String(parameter.defaultValue) : ''}
          disabled={disabled}
          onChange={event => {
            const value = event.currentTarget.value;
            onChange({
              ...parameter,
              defaultValue: value === '' ? undefined : value === 'true',
              defaultTagReference: undefined,
              version: parameter.version ?? 1
            });
          }}
        >
          <option value="">{text.none}</option>
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      </label>
    </div>;
  }

  const current = parameter.defaultValue == null ? '' : String(parameter.defaultValue);
  return <label className="visual-editor-dynamo-definition-parameter__default">
    <span>{text.defaultValue}</span>
    <input
      type={parameter.kind === 'Number' ? 'number' : 'text'}
      value={current}
      disabled={disabled}
      placeholder={parameter.kind === 'EquipmentPath' ? '{equipmentPath}' : undefined}
      onChange={event => {
        const raw = event.currentTarget.value;
        const defaultValue = raw === ''
          ? undefined
          : parameter.kind === 'Number'
            ? Number(raw)
            : raw;
        onChange({
          ...parameter,
          defaultValue,
          defaultTagReference: undefined,
          version: parameter.version ?? 1
        });
      }}
    />
  </label>;
}

function resetPayloadForKind(
  parameter: DynamoParameterDefinitionEngineering,
  kind: DynamoParameterKindEngineering
): DynamoParameterDefinitionEngineering {
  return Object.freeze({
    key: parameter.key,
    kind,
    required: parameter.required === true,
    version: parameter.version ?? 1
  });
}

function kindLabel(kind: DynamoParameterKindEngineering, locale: EngineeringLocale): string {
  if (locale === 'en') return kind;
  if (locale === 'es') {
    switch (kind) {
      case 'TagReference': return 'Referencia TAG';
      case 'Boolean': return 'Booleano';
      case 'Number': return 'Numérico';
      case 'String': return 'Texto';
      case 'EquipmentPath': return 'Ruta de equipo';
    }
  }
  switch (kind) {
    case 'TagReference': return 'Referência TAG';
    case 'Boolean': return 'Booleano';
    case 'Number': return 'Numérico';
    case 'String': return 'Texto';
    case 'EquipmentPath': return 'Caminho de equipamento';
  }
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Public interface',
    hint: 'Canonical Dynamo parameters. TAG references are supplied by each instance; SVG remains artwork only.',
    add: 'Add parameter',
    empty: 'No public parameters yet.',
    key: 'Parameter',
    kind: 'Type',
    required: 'Required',
    defaultValue: 'Default',
    none: 'No default',
    tagHint: 'TAG defaults are intentionally not authored here; assign the TAG on each Dynamo instance.',
    remove: 'Remove'
  };
  if (locale === 'es') return {
    title: 'Interfaz pública',
    hint: 'Parámetros canónicos del Dínamo. Cada instancia suministra las referencias TAG; SVG sigue siendo solo arte.',
    add: 'Agregar parámetro',
    empty: 'Todavía no hay parámetros públicos.',
    key: 'Parámetro',
    kind: 'Tipo',
    required: 'Obligatorio',
    defaultValue: 'Predeterminado',
    none: 'Sin predeterminado',
    tagHint: 'Los TAG no se fijan como predeterminado aquí; asigne el TAG en cada instancia del Dínamo.',
    remove: 'Eliminar'
  };
  return {
    title: 'Interface pública',
    hint: 'Parâmetros canônicos do Dínamo. Referências TAG são fornecidas por cada instância; SVG continua sendo apenas artwork.',
    add: 'Adicionar parâmetro',
    empty: 'Ainda não há parâmetros públicos.',
    key: 'Parâmetro',
    kind: 'Tipo',
    required: 'Obrigatório',
    defaultValue: 'Padrão',
    none: 'Sem padrão',
    tagHint: 'TAG não é fixado como padrão aqui; atribua o TAG em cada instância do Dínamo.',
    remove: 'Remover'
  };
}

export default DynamoDefinitionParametersEditor;
