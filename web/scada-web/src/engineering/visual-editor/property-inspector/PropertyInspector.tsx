import { useEffect, useMemo, useState } from 'react';
import type { EngineeringLocale } from '../../i18n';
import type { VisualEditorPropertyInspectorContractProps } from '../visualEditorContracts';
import type { VisualAssetEngineering, VisualElementEngineering, VisualEngineeringPropertyValue } from '../../types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../../visual-runtime';
import { BrowserConfigurationEditor } from '../BrowserConfigurationEditor';
import { SvgPaintOverrideEditor } from '../SvgPaintOverrideEditor';
import { EventsEditor } from '../events-editor/EventsEditor';
import { TrendPenEditor } from '../TrendPenEditor';
import { c07VisualEditorText, useC07VisualEditorText } from '../c07VisualEditorI18n';
import {
  trendInspectorControlCopy,
  trendPropertyLabel,
  trendPropertyOptionLabel
} from '../trendAuthoringModel';
import { PropertyEditorControl } from './PropertyEditorControl';
import {
  buildPropertyInspectorModel,
  buildPropertyInspectorRemoveIntent,
  buildPropertyInspectorSetIntent,
  type PropertyInspectorModel,
  type PropertyInspectorRow
} from './propertyInspectorModel';
import './PropertyInspector.css';

export type PropertyInspectorCopy = Readonly<{
  title: string;
  identity: string;
  developerKey: string;
  stableId: string;
  renameHint: string;
  keyRequired: string;
  noSelection: string;
  selectHint: string;
  selected: (count: number) => string;
  useDefault: string;
  mixed: string;
  trueLabel: string;
  falseLabel: string;
  noAsset: string;
  assetBrowserHint: string;
  chooseImage: string;
  importingAsset: string;
  transparent: string;
  alpha: string;
  fontFamilyPlaceholder: string;
  fontFamilyOptions: Readonly<Record<string, string>>;
  fontWeightLabel: string;
  defaultState: string;
  engineeringState: string;
  mixedState: (explicitCount: number, selectionCount: number) => string;
  filterLabel: string;
  filterPlaceholder: string;
  noMatches: string;
  imageFitLabel: string;
  imagePositionXLabel: string;
  imagePositionYLabel: string;
  imageZoomLabel: string;
  fitOptions: Readonly<Record<string, string>>;
  category: Readonly<Record<string, string>>;
}>;

export type PropertyInspectorProps = VisualEditorPropertyInspectorContractProps & Readonly<{
  visualAssets?: readonly VisualAssetEngineering[];
  copy?: Partial<PropertyInspectorCopy>;
  showEvents?: boolean;
  onImportImage?: (file: File) => Promise<string | null | void> | string | null | void;
  imageImportDisabled?: boolean;
  imageImportBusy?: boolean;
}>;

const DEFAULT_COPY: PropertyInspectorCopy = {
  title: 'Properties',
  identity: 'Identity',
  developerKey: 'Development identifier',
  stableId: 'Stable Id',
  renameHint: 'Changing this updates only the development-facing identifier; stable identity is preserved.',
  keyRequired: 'A development identifier is required.',
  noSelection: 'No selection',
  selectHint: 'Select a visual object to inspect its registered properties.',
  selected: count => `${count} selected`,
  useDefault: 'Use default',
  mixed: 'Mixed',
  trueLabel: 'True',
  falseLabel: 'False',
  noAsset: 'No asset',
  assetBrowserHint: 'Import a PNG, JPG, BMP, SVG, PDF, MP4 or WebM project asset.',
  chooseImage: 'Import project asset…',
  importingAsset: 'Importing…',
  transparent: 'Transparent',
  alpha: 'Alpha',
  fontFamilyPlaceholder: 'Choose a font family',
  fontFamilyOptions: {
    system: 'System default',
    'Arimo Variable': 'Arimo — similar to Arial',
    Lato: 'Lato — humanist sans serif',
    Tinos: 'Tinos — similar to Times New Roman',
    Cousine: 'Cousine — similar to Courier New',
    custom: 'Custom…'
  },
  fontWeightLabel: 'Bold',
  defaultState: 'Default',
  engineeringState: 'Engineering',
  mixedState: (explicitCount, selectionCount) => `Mixed · ${explicitCount}/${selectionCount} explicit`,
  filterLabel: 'Filter properties',
  filterPlaceholder: 'Name or canonical key',
  noMatches: 'No properties match this filter.',
  imageFitLabel: 'Image fit',
  imagePositionXLabel: 'Horizontal crop position',
  imagePositionYLabel: 'Vertical crop position',
  imageZoomLabel: 'Image zoom',
  fitOptions: { contain: 'Contain', cover: 'Cover / crop', fill: 'Stretch', native: 'Original size' },
  category: {
    general: 'General',
    geometry: 'Geometry',
    appearance: 'Appearance',
    text: 'Text',
    image: 'Image',
    control: 'Control',
    trend: 'Trend'
  }
};

export function PropertyInspector({
  selectedElements,
  onMutationIntent,
  visualAssets = [],
  copy,
  showEvents = true,
  onImportImage,
  imageImportDisabled = false,
  imageImportBusy = false
}: PropertyInspectorProps) {
  const currentVisualText = useC07VisualEditorText();
  const locale = localeForVisualText(currentVisualText);
  const chromeText = propertyInspectorChromeText(locale);
  const text: PropertyInspectorCopy = {
    ...DEFAULT_COPY,
    ...chromeText,
    ...copy,
    category: { ...DEFAULT_COPY.category, ...(copy?.category ?? {}) }
  };
  const [filter, setFilter] = useState('');
  const [collapsedCategories, setCollapsedCategories] = useState<ReadonlySet<string>>(() => new Set());
  const model = useMemo(() => buildPropertyInspectorModel(selectedElements), [selectedElements]);
  const filteredRows = useMemo(() => filterPropertyRows(model.rows, filter, text), [model.rows, filter, text]);
  const isTextObject = selectedElements.length === 1 && selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.text;
  const groupedRows = useMemo(() => groupRows(filteredRows, isTextObject), [filteredRows, isTextObject]);

  const toggleCategory = (category: string) => {
    setCollapsedCategories(current => {
      const next = new Set(current);
      if (next.has(category)) next.delete(category);
      else next.add(category);
      return next;
    });
  };

  if (selectedElements.length === 0) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>{text.title}</strong>
          <span>{text.noSelection}</span>
        </header>
        <p className="property-inspector__empty">{text.selectHint}</p>
      </aside>
    );
  }

  if (model.error) {
    return (
      <aside className="property-inspector" data-testid="visual-property-inspector">
        <header className="property-inspector__header">
          <strong>{text.title}</strong>
          <span>{text.selected(selectedElements.length)}</span>
        </header>
        <p className="property-inspector__error" role="alert">{model.error}</p>
      </aside>
    );
  }

  const selectedTrend = selectedElements.length === 1 && selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.trend
    ? selectedElements[0]
    : null;
  const selectedBrowser = selectedElements.length === 1 && (
    selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.alarmBrowser ||
    selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.eventBrowser
  ) ? selectedElements[0] : null;
  const selectedSvg = selectedElements.length === 1 &&
    selectedElements[0].type === BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol
      ? selectedElements[0]
      : null;

  return (
    <aside className="property-inspector" data-testid="visual-property-inspector">
      <header className="property-inspector__header">
        <strong>{text.title}</strong>
        {selectedElements.length > 1 ? <span>{text.selected(selectedElements.length)}</span> : null}
      </header>

      {model.diagnostic ? <p className="property-inspector__diagnostic" role="status">{model.diagnostic}</p> : null}

      {selectedElements.length === 1 && selectedElements[0].id ? <IdentityEditor
        element={selectedElements[0]}
        text={text}
        onMutationIntent={onMutationIntent}
      /> : null}

      <label className="property-inspector__filter">
        <span>{text.filterLabel}</span>
        <input
          type="search"
          value={filter}
          aria-label={text.filterLabel}
          placeholder={text.filterPlaceholder}
          onChange={event => setFilter(event.currentTarget.value)}
        />
      </label>

      {groupedRows.length === 0 ? <p className="property-inspector__empty">{text.noMatches}</p> : null}
      {groupedRows.map(([category, rows]) => {
        const collapsed = collapsedCategories.has(category);
        return <section className={`property-inspector__group property-inspector__group--${category}`} key={category}>
          <button
            type="button"
            className="property-inspector__group-toggle"
            aria-expanded={!collapsed}
            onClick={() => toggleCategory(category)}
          >
            <span>{text.category[category] ?? category}</span>
            <small>{rows.length}</small>
          </button>
          {!collapsed ? <div className="property-inspector__group-fields">
            {rows.map(row => (
              <PropertyField
                key={row.definition.key}
                model={model}
                row={row}
                objectType={selectedElements.length === 1 ? selectedElements[0].type : undefined}
                text={text}
                locale={locale}
                visualAssets={visualAssets}
                onMutationIntent={onMutationIntent}
                onImportImage={onImportImage}
                imageImportDisabled={imageImportDisabled}
                imageImportBusy={imageImportBusy}
              />
            ))}
          </div> : null}
        </section>;
      })}

      {selectedSvg ? <SvgPaintOverrideEditor
        element={selectedSvg}
        visualAssets={visualAssets}
        locale={locale}
        onMutationIntent={onMutationIntent}
      /> : null}
      {selectedTrend ? <TrendPenEditor element={selectedTrend} onMutationIntent={onMutationIntent} /> : null}
      {selectedBrowser ? <BrowserConfigurationEditor element={selectedBrowser} locale={locale} onMutationIntent={onMutationIntent} /> : null}

      {showEvents && selectedElements.length === 1 && selectedElements[0].id ? (
        <EventsEditor
          visualObjectId={selectedElements[0].id}
          element={selectedElements[0]}
          onMutationIntent={onMutationIntent}
        />
      ) : null}
    </aside>
  );
}

type PropertyFieldProps = Readonly<{
  model: PropertyInspectorModel;
  row: PropertyInspectorRow;
  objectType?: string;
  text: PropertyInspectorCopy;
  locale: EngineeringLocale;
  visualAssets: readonly VisualAssetEngineering[];
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
  onImportImage?: PropertyInspectorProps['onImportImage'];
  imageImportDisabled: boolean;
  imageImportBusy: boolean;
}>;

function PropertyField({ model, row, text, locale, visualAssets, objectType, onMutationIntent, onImportImage, imageImportDisabled, imageImportBusy }: PropertyFieldProps) {
  const [error, setError] = useState<string | null>(null);
  const definition = row.definition;
  const localizedTrendLabel = trendPropertyLabel(locale, definition.key);
  const rowText: PropertyInspectorCopy = localizedTrendLabel
    ? { ...text, ...trendInspectorControlCopy(locale) }
    : text;

  const commit = (value: VisualEngineeringPropertyValue) => {
    const result = buildPropertyInspectorSetIntent(model, definition.key, value);
    if (!result.ok) {
      setError(result.error);
      return false;
    }
    setError(null);
    onMutationIntent(result.intent);
    return true;
  };

  const remove = () => {
    const result = buildPropertyInspectorRemoveIntent(model, definition.key);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setError(null);
    onMutationIntent(result.intent);
  };

  const trendModeControl = definition.key === VISUAL_PROPERTY_KEYS.trendMode && definition.type === 'enum';
  const trendModeValue = row.state === 'mixed' ? '__mixed__' : String(row.value);

  return (
    <div
      className="property-inspector__field"
      data-property-key={definition.key}
      data-editor-type={definition.type}
      data-editor-hint={definition.presentationHint ?? undefined}
    >
      <div className="property-inspector__field-heading">
        <div className="property-inspector__field-label">
          <label htmlFor={`visual-property-${definition.key}`}>
            {localizedTrendLabel ?? visualPropertyLabel(definition.key, rowText, objectType, locale)}
          </label>
          <code title="Canonical property key">{definition.key}</code>
        </div>
        <span className={`property-inspector__state property-inspector__state--${row.state}`}>{stateLabel(row, rowText)}</span>
      </div>

      {trendModeControl ? (
        <select
          id={`visual-property-${definition.key}`}
          value={trendModeValue}
          disabled={!definition.engineeringEditable}
          onChange={event => commit(event.currentTarget.value)}
        >
          {row.state === 'mixed' ? <option value="__mixed__" disabled>{rowText.mixed}</option> : null}
          {definition.allowedValues.map(option => (
            <option key={option} value={option}>{trendPropertyOptionLabel(locale, definition.key, option)}</option>
          ))}
        </select>
      ) : (
        <PropertyEditorControl
          definition={definition}
          objectType={objectType}
          row={row}
          text={rowText}
          visualAssets={visualAssets}
          onImportImage={definition.key === VISUAL_PROPERTY_KEYS.assetRef ? onImportImage : undefined}
          imageImportDisabled={imageImportDisabled}
          imageImportBusy={imageImportBusy}
          commit={commit}
          setError={setError}
        />
      )}

      <div className="property-inspector__field-meta">
        {definition.key === VISUAL_PROPERTY_KEYS.fontWeight ? null : <span>{definition.type}{definition.unit ? ` · ${definition.unit}` : ''}</span>}
        <button
          type="button"
          className="property-inspector__reset"
          disabled={row.state === 'default' || !definition.engineeringEditable}
          onClick={remove}
        >
          {rowText.useDefault}
        </button>
      </div>

      {objectType === BUILTIN_VISUAL_OBJECT_TYPES.numericInput && definition.key === VISUAL_PROPERTY_KEYS.interactionEnabled ? (
        <p className="property-inspector__field-hint">{numericWritePermissionHint(locale)}</p>
      ) : null}

      {error ? <p className="property-inspector__validation" role="alert">{error}</p> : null}
    </div>
  );
}

function IdentityEditor({
  element,
  text,
  onMutationIntent
}: {
  element: VisualElementEngineering;
  text: PropertyInspectorCopy;
  onMutationIntent: VisualEditorPropertyInspectorContractProps['onMutationIntent'];
}) {
  const [keyDraft, setKeyDraft] = useState(element.key);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setKeyDraft(element.key);
    setError(null);
  }, [element.id, element.key]);

  const commit = () => {
    const key = keyDraft.trim();
    if (!key) {
      setError(text.keyRequired);
      return;
    }
    setKeyDraft(key);
    setError(null);
    if (key === element.key || !element.id) return;
    onMutationIntent({ kind: 'object.rename', objectId: element.id, key });
  };

  return <details className="property-inspector__identity" data-testid="visual-property-identity">
    <summary><strong>{text.identity}</strong><code>{element.key}</code></summary>
    <div className="property-inspector__identity-body">
      <p>{text.renameHint}</p>
      <label>
        <span>{text.developerKey}</span>
        <input
          data-testid="visual-property-identity-key"
          value={keyDraft}
          onChange={event => { setKeyDraft(event.currentTarget.value); setError(null); }}
          onBlur={commit}
          onKeyDown={event => {
            if (event.key === 'Enter') event.currentTarget.blur();
            if (event.key === 'Escape') {
              setKeyDraft(element.key);
              setError(null);
              event.currentTarget.blur();
            }
          }}
        />
      </label>
      <div className="property-inspector__stable-id">
        <span>{text.stableId}</span>
        <code data-testid="visual-property-identity-id">{element.id}</code>
      </div>
      {error ? <p className="property-inspector__validation" role="alert">{error}</p> : null}
    </div>
  </details>;
}

export function humanizeVisualPropertyKey(propertyKey: string): string {
  if (!propertyKey) return propertyKey;
  const words = propertyKey.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return `${words[0].toUpperCase()}${words.slice(1)}`;
}

function visualPropertyLabel(propertyKey: string, text: PropertyInspectorCopy, objectType?: string, locale: EngineeringLocale = 'pt-BR'): string {
  switch (propertyKey) {
    case VISUAL_PROPERTY_KEYS.reportKey:
      return locale === 'en' ? 'Report reference' : locale === 'es' ? 'Referencia del informe' : 'Referência do relatório';
    case VISUAL_PROPERTY_KEYS.arcStyle:
      return locale === 'en' ? 'Arc style' : locale === 'es' ? 'Estilo del arco' : 'Estilo do arco';
    case VISUAL_PROPERTY_KEYS.arcStartAngle:
      return locale === 'en' ? 'Start angle' : locale === 'es' ? 'Ángulo inicial' : 'Ângulo inicial';
    case VISUAL_PROPERTY_KEYS.arcEndAngle:
      return locale === 'en' ? 'End angle' : locale === 'es' ? 'Ángulo final' : 'Ângulo final';
    case VISUAL_PROPERTY_KEYS.bezierPath:
      return locale === 'en' ? 'Curve path (SVG)' : locale === 'es' ? 'Ruta de la curva (SVG)' : 'Caminho da curva (SVG)';
    case VISUAL_PROPERTY_KEYS.polygonFillRule:
      return locale === 'en' ? 'Polygon fill rule' : locale === 'es' ? 'Regla de relleno del polígono' : 'Regra de preenchimento do polígono';
    case VISUAL_PROPERTY_KEYS.interactionEnabled:
      if (objectType === BUILTIN_VISUAL_OBJECT_TYPES.numericInput) {
        return locale === 'en' ? 'Allow variable writes' : locale === 'es' ? 'Permitir escritura de variable' : 'Permitir escrita da variável';
      }
      return humanizeVisualPropertyKey(propertyKey);
    case VISUAL_PROPERTY_KEYS.showSteppers:
      return locale === 'en' ? 'Show increment/decrement buttons' : locale === 'es' ? 'Mostrar botones para aumentar/disminuir' : 'Mostrar botões de aumentar/diminuir';
    case VISUAL_PROPERTY_KEYS.unit:
      return locale === 'en' ? 'Unit' : locale === 'es' ? 'Unidad' : 'Unidade';
    case VISUAL_PROPERTY_KEYS.showEngineeringUnit:
      return locale === 'en' ? 'Show TAG engineering unit' : locale === 'es' ? 'Mostrar unidad de ingeniería del TAG' : 'Exibir unidade de engenharia da TAG';
    case VISUAL_PROPERTY_KEYS.decimalPlacesEnabled:
      return locale === 'en' ? 'Show fixed decimal places' : locale === 'es' ? 'Mostrar decimales fijos' : 'Exibir casas decimais fixas';
    case VISUAL_PROPERTY_KEYS.decimalPlaces:
      return locale === 'en' ? 'Decimal places' : locale === 'es' ? 'Cantidad de decimales' : 'Quantidade de casas decimais';
    case VISUAL_PROPERTY_KEYS.fontWeight: return text.fontWeightLabel;
    case VISUAL_PROPERTY_KEYS.imageFit: return text.imageFitLabel;
    case VISUAL_PROPERTY_KEYS.imagePositionX: return text.imagePositionXLabel;
    case VISUAL_PROPERTY_KEYS.imagePositionY: return text.imagePositionYLabel;
    case VISUAL_PROPERTY_KEYS.imageZoom: return text.imageZoomLabel;
    default: return humanizeVisualPropertyKey(propertyKey);
  }
}

function numericWritePermissionHint(locale: EngineeringLocale): string {
  if (locale === 'en') return 'This enables the control only; the TAG write direction and the user\'s Runtime authorization are still required.';
  if (locale === 'es') return 'Esto habilita el control; también se requiere dirección de escritura en el TAG y autorización Runtime del usuario.';
  return 'Esta opção habilita o controle; a TAG também precisa aceitar escrita e o usuário precisa de autorização no Runtime.';
}

function filterPropertyRows(
  rows: readonly PropertyInspectorRow[],
  filter: string,
  text: PropertyInspectorCopy
): readonly PropertyInspectorRow[] {
  const query = filter.trim().toLocaleLowerCase();
  if (!query) return rows;
  return rows.filter(row => {
    const category = row.definition.category ?? 'general';
    return [
      row.definition.key,
      humanizeVisualPropertyKey(row.definition.key),
      text.category[category] ?? category
    ].some(value => value.toLocaleLowerCase().includes(query));
  });
}

function groupRows(rows: readonly PropertyInspectorRow[], prioritizeText = false): readonly [string, readonly PropertyInspectorRow[]][] {
  const groups = new Map<string, PropertyInspectorRow[]>();
  for (const row of rows) {
    const category = row.definition.category ?? 'general';
    const existing = groups.get(category);
    if (existing) existing.push(row);
    else groups.set(category, [row]);
  }
  const entries = [...groups.entries()];
  if (prioritizeText) {
    const textIndex = entries.findIndex(([category]) => category === 'text');
    const geometryIndex = entries.findIndex(([category]) => category === 'geometry');
    if (textIndex >= 0 && geometryIndex >= 0 && textIndex > geometryIndex) {
      const [textGroup] = entries.splice(textIndex, 1);
      entries.splice(geometryIndex, 0, textGroup);
    }
  }
  return entries;
}

function stateLabel(row: PropertyInspectorRow, text: PropertyInspectorCopy): string {
  switch (row.state) {
    case 'default': return text.defaultState;
    case 'engineered': return text.engineeringState;
    case 'mixed': return text.mixedState(row.explicitCount, row.selectionCount);
  }
}

function propertyInspectorChromeText(locale: EngineeringLocale) {
  if (locale === 'pt-BR') return {
    identity: 'Identidade',
    developerKey: 'Identificador de desenvolvimento',
    stableId: 'Id estável',
    renameHint: 'Alterar este campo atualiza apenas o identificador usado no desenvolvimento; a identidade estável é preservada.',
    keyRequired: 'O identificador de desenvolvimento é obrigatório.',
    assetBrowserHint: 'Importe um asset PNG, JPG, BMP, SVG, PDF, MP4 ou WebM para este projeto.',
    chooseImage: 'Importar asset do projeto…',
    importingAsset: 'Importando…',
    fontFamilyPlaceholder: 'Escolha uma família de fontes',
    fontFamilyOptions: {
      system: 'Padrão do sistema', 'Arimo Variable': 'Arimo — semelhante à Arial', Lato: 'Lato — sem serifa humanista',
      Tinos: 'Tinos — semelhante à Times New Roman', Cousine: 'Cousine — semelhante à Courier New', custom: 'Personalizada…'
    },
    fontWeightLabel: 'Negrito',
    imageFitLabel: 'Ajuste da imagem', imagePositionXLabel: 'Posição horizontal do recorte', imagePositionYLabel: 'Posição vertical do recorte', imageZoomLabel: 'Zoom da imagem',
    fitOptions: { contain: 'Conter inteira', cover: 'Cobrir e recortar', fill: 'Esticar', native: 'Tamanho original' },
    filterLabel: 'Filtrar propriedades',
    filterPlaceholder: 'Nome de exibição ou identificador canônico',
    noMatches: 'Nenhuma propriedade corresponde ao filtro.'
  };
  if (locale === 'es') return {
    identity: 'Identidad',
    developerKey: 'Identificador de desarrollo',
    stableId: 'Id estable',
    renameHint: 'Cambiarlo actualiza solo el identificador visible para desarrollo; la identidad estable se conserva.',
    keyRequired: 'El identificador de desarrollo es obligatorio.',
    assetBrowserHint: 'Importe un recurso PNG, JPG, BMP, SVG, PDF, MP4 o WebM al proyecto.',
    chooseImage: 'Importar recurso del proyecto…',
    importingAsset: 'Importando…',
    fontFamilyPlaceholder: 'Elige una familia tipográfica',
    fontFamilyOptions: {
      system: 'Predeterminada del sistema', 'Arimo Variable': 'Arimo — similar a Arial', Lato: 'Lato — sans serif humanista',
      Tinos: 'Tinos — similar a Times New Roman', Cousine: 'Cousine — similar a Courier New', custom: 'Personalizada…'
    },
    fontWeightLabel: 'Negrita',
    imageFitLabel: 'Ajuste de imagen', imagePositionXLabel: 'Posición horizontal del recorte', imagePositionYLabel: 'Posición vertical del recorte', imageZoomLabel: 'Zoom de imagen',
    fitOptions: { contain: 'Contener completa', cover: 'Cubrir y recortar', fill: 'Estirar', native: 'Tamaño original' },
    filterLabel: 'Filtrar propiedades',
    filterPlaceholder: 'Nombre visible o identificador canónico',
    noMatches: 'Ninguna propiedad coincide con el filtro.'
  };
  return {
    identity: 'Identity',
    developerKey: 'Development identifier',
    stableId: 'Stable Id',
    renameHint: 'Changing this updates only the development-facing identifier; stable identity is preserved.',
    keyRequired: 'A development identifier is required.',
    chooseImage: 'Import project asset…',
    importingAsset: 'Importing…',
    fontFamilyPlaceholder: 'Choose a font family',
    fontFamilyOptions: {
      system: 'System default', 'Arimo Variable': 'Arimo — similar to Arial', Lato: 'Lato — humanist sans serif',
      Tinos: 'Tinos — similar to Times New Roman', Cousine: 'Cousine — similar to Courier New', custom: 'Custom…'
    },
    fontWeightLabel: 'Bold',
    imageFitLabel: 'Image fit', imagePositionXLabel: 'Horizontal crop position', imagePositionYLabel: 'Vertical crop position', imageZoomLabel: 'Image zoom',
    fitOptions: { contain: 'Contain whole image', cover: 'Cover and crop', fill: 'Stretch', native: 'Original size' },
    filterLabel: 'Filter properties',
    filterPlaceholder: 'Display name or canonical identifier',
    noMatches: 'No properties match this filter.'
  };
}

function localeForVisualText(text: ReturnType<typeof useC07VisualEditorText>): EngineeringLocale {
  if (text === c07VisualEditorText('en')) return 'en';
  if (text === c07VisualEditorText('es')) return 'es';
  return 'pt-BR';
}

export default PropertyInspector;
