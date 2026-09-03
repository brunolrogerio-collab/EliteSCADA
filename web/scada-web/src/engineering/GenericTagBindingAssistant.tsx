import React, { useEffect, useMemo, useState } from 'react';
import { loadDataSourceTypeCatalog } from './DataSourceCatalogEditor';
import {
  tagBindingSchemaIdentity,
  type DataSourceConfigurationField,
  type DataSourceTypeDefinition
} from './DataSourceCatalogEditor.logic';
import type { EngineeringLocale } from './i18n';
import { c04Text, type C04Text } from './c04I18n';
import type { TagSourceAwareEngineering } from './TagSourceSelector.logic';

type Props = {
  tag: TagSourceAwareEngineering;
  driverType: string;
  locale: EngineeringLocale;
  onChange: (tag: TagSourceAwareEngineering) => void;
};

export function GenericTagBindingAssistant({ tag, driverType, locale, onChange }: Props) {
  const text = useMemo(() => c04Text(locale).generic, [locale]);
  const [definition, setDefinition] = useState<DataSourceTypeDefinition | null>(null);
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    void loadDataSourceTypeCatalog()
      .then(types => {
        if (!alive) return;
        const next = types.find(candidate =>
          candidate.typeKey.toLowerCase() === driverType.trim().toLowerCase()) ?? null;
        setDefinition(next);
        setSettings(initialSettings(next, tag));
      })
      .catch(() => {
        if (!alive) return;
        setDefinition(null);
        setSettings({});
        setError(text.schemaUnavailable);
      })
      .finally(() => {
        if (alive) setLoading(false);
      });
    return () => { alive = false; };
  }, [driverType, tag.communicationBinding, text.schemaUnavailable]);

  const fields = definition?.configurationSchema?.tagBindingFields ?? [];
  if (loading) return <small data-testid="generic-tag-binding-loading">{text.loading}</small>;
  if (!definition || fields.length === 0) return error ? <small role="alert">{error}</small> : null;

  const apply = () => {
    setError(null);
    const address = tag.address?.trim();
    if (!address) {
      setError(text.addressRequired);
      return;
    }

    const identity = tagBindingSchemaIdentity(definition);
    if (!identity) {
      setError(text.schemaUnavailable);
      return;
    }

    const validationError = validateSettings(fields, settings, text);
    if (validationError) {
      setError(validationError);
      return;
    }

    const normalized: Record<string, string> = {};
    for (const field of fields) {
      const value = settings[field.key]?.trim() ?? '';
      if (value) normalized[field.key] = value;
    }

    onChange({
      ...tag,
      address,
      communicationBinding: {
        contractVersion: 1,
        schemaId: identity.schemaId,
        schemaVersion: identity.schemaVersion,
        portableAddress: address,
        settings: normalized
      }
    });
  };

  return (
    <section className="eng-dictionary-editor eng-editor-field-wide" data-testid="generic-tag-binding-assistant">
      <header>
        <strong>{text.title}</strong>
        <span>{text.help}</span>
      </header>
      <div className="eng-editor-form-grid">
        {fields.map(field => (
          <GenericField
            key={field.key}
            field={field}
            value={settings[field.key] ?? ''}
            protectedMaterialHint={text.protectedMaterialHint}
            onChange={value => setSettings(current => ({ ...current, [field.key]: value }))}
          />
        ))}
      </div>
      <div className="eng-editor-actions">
        <button type="button" className="secondary" onClick={apply} data-testid="generic-tag-binding-apply">
          {text.apply}
        </button>
      </div>
      {error && <pre className="eng-preview-error" role="alert">{error}</pre>}
    </section>
  );
}

function GenericField({ field, value, protectedMaterialHint, onChange }: {
  field: DataSourceConfigurationField;
  value: string;
  protectedMaterialHint: string;
  onChange: (value: string) => void;
}) {
  const testId = `generic-tag-binding-${field.key.replace(/[^A-Za-z0-9_-]+/g, '-')}`;
  const unsupportedProtected = field.valueKind === 'secretReference' || field.valueKind === 'certificateReference';

  return (
    <label className="eng-editor-field">
      <span>{field.displayName || field.key}{field.required ? ' *' : ''}</span>
      {field.valueKind === 'enum' ? (
        <select value={value} onChange={event => onChange(event.target.value)} data-testid={testId}>
          {!field.required && <option value="" />}
          {field.allowedValues.map(option => <option key={option} value={option}>{option}</option>)}
        </select>
      ) : field.valueKind === 'boolean' ? (
        <select value={value} onChange={event => onChange(event.target.value)} data-testid={testId}>
          {!field.required && <option value="" />}
          <option value="true">true</option>
          <option value="false">false</option>
        </select>
      ) : (
        <input
          className={field.valueKind === 'identifier' ? 'mono' : undefined}
          type={field.valueKind === 'integer' || field.valueKind === 'number' ? 'number' : 'text'}
          step={field.valueKind === 'integer' ? '1' : field.valueKind === 'number' ? 'any' : undefined}
          value={value}
          disabled={unsupportedProtected}
          onChange={event => onChange(event.target.value)}
          placeholder={field.exampleValue ?? undefined}
          data-testid={testId}
        />
      )}
      {field.description && <small>{field.description}</small>}
      {unsupportedProtected && <small>{protectedMaterialHint}</small>}
    </label>
  );
}

function initialSettings(
  definition: DataSourceTypeDefinition | null,
  tag: TagSourceAwareEngineering
): Record<string, string> {
  const fields = definition?.configurationSchema?.tagBindingFields ?? [];
  const identity = tagBindingSchemaIdentity(definition);
  const matches = Boolean(identity && tag.communicationBinding?.schemaId === identity.schemaId);
  const result: Record<string, string> = {};

  for (const field of fields) {
    const current = matches ? tag.communicationBinding?.settings?.[field.key] : undefined;
    const value = current ?? field.defaultValue;
    if (value != null && value !== '') result[field.key] = value;
  }

  return result;
}

function validateSettings(
  fields: readonly DataSourceConfigurationField[],
  settings: Readonly<Record<string, string>>,
  text: C04Text['generic']
): string | null {
  for (const field of fields) {
    const value = settings[field.key]?.trim() ?? '';
    if (field.required && !value) return `${field.displayName || field.key}: ${text.required}`;
    if (!value) continue;

    if (field.valueKind === 'integer') {
      if (!/^[+-]?\d+$/.test(value)) return `${field.displayName || field.key}: ${text.integer}`;
      const parsed = Number(value);
      if (!Number.isSafeInteger(parsed)) return `${field.displayName || field.key}: ${text.integer}`;
      if (field.minimum != null && parsed < field.minimum) return `${field.displayName || field.key}: >= ${field.minimum}`;
      if (field.maximum != null && parsed > field.maximum) return `${field.displayName || field.key}: <= ${field.maximum}`;
    }

    if (field.valueKind === 'number') {
      const parsed = Number(value);
      if (!Number.isFinite(parsed)) return `${field.displayName || field.key}: ${text.number}`;
      if (field.minimum != null && parsed < field.minimum) return `${field.displayName || field.key}: >= ${field.minimum}`;
      if (field.maximum != null && parsed > field.maximum) return `${field.displayName || field.key}: <= ${field.maximum}`;
    }

    if (field.valueKind === 'enum' && field.allowedValues.length > 0 && !field.allowedValues.includes(value))
      return `${field.displayName || field.key}: ${text.enumValue}`;

    if ((field.valueKind === 'secretReference' || field.valueKind === 'certificateReference') && value)
      return `${field.displayName || field.key}: ${text.protectedMaterial}`;
  }

  return null;
}
