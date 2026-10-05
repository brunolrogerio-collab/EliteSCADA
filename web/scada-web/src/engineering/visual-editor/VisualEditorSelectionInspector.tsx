import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { ScreenEngineering, VisualAssetEngineering, VisualElementEngineering } from '../types';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';
import { BindingEditor } from './binding-editor';
import { DynamoInstanceInspector } from './canvas/DynamoInstanceInspector';
import { DynamicPropertyEditor } from './dynamic-property-editor';
import { EventsEditor } from './events-editor/EventsEditor';
import { PropertyInspector } from './property-inspector';
import { VisualDefinitionSurfaceInspector } from './canvas/VisualDefinitionSurfaceInspector';
import type { VisualEditorBindingSourceCatalogItem, VisualEditorMutationIntent } from './visualEditorContracts';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';

export type VisualEditorInspectorTab = 'properties' | 'dynamics' | 'events';

export function VisualEditorSelectionInspector({
  screen,
  selectedElements,
  selectedObjectIds,
  sourceCatalog,
  visualAssets,
  locale,
  activeTab,
  onActiveTabChange,
  onMutationIntent,
  onCommand,
  onImportImage,
  imageImportDisabled,
  imageImportBusy,
  dynamoCommandParameters
}: {
  screen: ScreenEngineering;
  selectedElements: readonly VisualElementEngineering[];
  selectedObjectIds: readonly string[];
  sourceCatalog: readonly VisualEditorBindingSourceCatalogItem[];
  visualAssets: readonly VisualAssetEngineering[];
  locale: EngineeringLocale;
  activeTab: VisualEditorInspectorTab;
  onActiveTabChange: (tab: VisualEditorInspectorTab) => void;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
  onImportImage?: (file: File) => Promise<string | null | void> | string | null | void;
  imageImportDisabled?: boolean;
  imageImportBusy?: boolean;
  dynamoCommandParameters?: readonly string[];
}) {
  const text = inspectorText(locale);
  const selectedElement = selectedElements.length === 1 ? selectedElements[0] : null;
  const selectedVisualAsset = selectedElement?.type === BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol
    ? findVisualAsset(selectedElement, visualAssets)
    : null;
  const contextLabel = selectedElement
    ? selectedElement.key
    : selectedElements.length > 1
      ? text.multi(selectedElements.length)
      : screen.name || screen.key;

  return <div className="visual-editor-selection-inspector" data-testid="visual-editor-selection-inspector">
    <header className="visual-editor-selection-context">
      <span>{selectedElement ? text.object : text.screen}</span>
      <strong>{contextLabel}</strong>
      {selectedElement?.id ? <code>{selectedElement.id}</code> : null}
    </header>

    <div className="visual-editor-inspector-tabs" role="tablist" aria-label={text.context}>
      {(['properties', 'dynamics', 'events'] as const).map(tab => <button
        key={tab}
        id={`visual-editor-inspector-tab-${tab}`}
        type="button"
        role="tab"
        aria-selected={activeTab === tab}
        aria-controls={`visual-editor-inspector-section-${tab}`}
        className={activeTab === tab ? 'is-active' : ''}
        onClick={() => onActiveTabChange(tab)}
        data-testid={`visual-editor-inspector-tab-${tab}`}
      >{text.tabs[tab]}</button>)}
    </div>

    <div className="visual-editor-inspector-panel" data-inspector-tab={activeTab}>
      <section id="visual-editor-inspector-section-properties" role="tabpanel" aria-labelledby="visual-editor-inspector-tab-properties" hidden={activeTab !== 'properties'} className={`visual-editor-inspector-section${activeTab === 'properties' ? ' is-active' : ''}`} data-inspector-section="properties">
        <p className="visual-editor-inspector-hint">{text.propertiesHint}</p>
        {selectedElements.length === 0 ? <VisualDefinitionSurfaceInspector
          screen={screen}
          onCommand={onCommand}
          onImportAsset={onImportImage}
          importDisabled={imageImportDisabled}
          importing={imageImportBusy}
        /> : null}
        <PropertyInspector
          selectedElements={selectedElements}
          visualAssets={visualAssets}
          onMutationIntent={onMutationIntent}
          showEvents={false}
          onImportImage={onImportImage}
          imageImportDisabled={imageImportDisabled}
          imageImportBusy={imageImportBusy}
        />
        {selectedElement?.id && isValueBindingControl(selectedElement.type) ? <BindingEditor
          element={selectedElement}
          sourceCatalog={sourceCatalog}
          onMutationIntent={onMutationIntent}
          locale={locale}
          preferredPropertyKey={valueBindingProperty(selectedElement.type)}
          copy={valueBindingEditorCopy(locale, selectedElement.type)}
        /> : null}
        <DynamoInstanceInspector
          screen={screen}
          selectedObjectIds={selectedObjectIds}
          clientMemorySources={sourceCatalog.filter(source => source.kind === 'ClientMemory' && source.tagReference?.tagId)}
          onCommand={onCommand}
        />
      </section>

      <section id="visual-editor-inspector-section-dynamics" role="tabpanel" aria-labelledby="visual-editor-inspector-tab-dynamics" hidden={activeTab !== 'dynamics'} className={`visual-editor-inspector-section${activeTab === 'dynamics' ? ' is-active' : ''}`} data-inspector-section="dynamics">
        {selectedElement?.id ? <>
        <DynamicPropertyEditor
          element={selectedElement}
          visualAsset={selectedVisualAsset}
          sourceCatalog={sourceCatalog}
          onBindingIntent={onMutationIntent}
          onSetExpression={configuration => onMutationIntent({ kind: 'propertyExpression.set', objectId: selectedElement.id!, configuration })}
          onRemoveExpression={propertyKey => onMutationIntent({ kind: 'propertyExpression.remove', objectId: selectedElement.id!, propertyKey })}
          onSetBooleanCondition={configuration => onMutationIntent({ kind: 'booleanCondition.set', objectId: selectedElement.id!, configuration })}
          onRemoveBooleanCondition={propertyKey => onMutationIntent({ kind: 'booleanCondition.remove', objectId: selectedElement.id!, propertyKey })}
          onSetAnalogFill={configuration => onMutationIntent({ kind: 'analogFill.set', objectId: selectedElement.id!, configuration })}
          onRemoveAnalogFill={() => onMutationIntent({ kind: 'analogFill.remove', objectId: selectedElement.id! })}
          onSetPropertyMap={configuration => onMutationIntent({ kind: 'propertyMap.set', objectId: selectedElement.id!, configuration })}
          onRemovePropertyMap={propertyKey => onMutationIntent({ kind: 'propertyMap.remove', objectId: selectedElement.id!, propertyKey })}
        />
        {!isValueBindingControl(selectedElement.type) ? <BindingEditor
          element={selectedElement}
          sourceCatalog={sourceCatalog}
          onMutationIntent={onMutationIntent}
          locale={locale}
          copy={bindingEditorCopy(locale)}
        /> : null}
      </> : <p className="visual-editor-selection-hint">{text.selectOne}</p>}
      </section>

      <section id="visual-editor-inspector-section-events" role="tabpanel" aria-labelledby="visual-editor-inspector-tab-events" hidden={activeTab !== 'events'} className={`visual-editor-inspector-section${activeTab === 'events' ? ' is-active' : ''}`} data-inspector-section="events">
        {selectedElements.length <= 1 ? <>
          <p className="visual-editor-inspector-hint">{selectedElement ? text.objectEvents : text.screenEvents}</p>
          <EventsEditor
            visualDefinitionId={screen.id}
            visualObjectId={selectedElement?.id ?? null}
            element={selectedElement ?? undefined}
            sourceCatalog={sourceCatalog}
            commandParameterKeys={dynamoCommandParameters}
            onMutationIntent={onMutationIntent}
            disabled={!screen.id}
          />
        </> : <p className="visual-editor-selection-hint">{text.selectOne}</p>}
      </section>
    </div>
  </div>;
}

function findVisualAsset(
  element: VisualElementEngineering,
  visualAssets: readonly VisualAssetEngineering[]
): VisualAssetEngineering | null {
  const reference = element.properties?.assetRef;
  if (!reference || typeof reference !== 'object' || Array.isArray(reference) || !('assetId' in reference)) return null;
  const raw = typeof reference.assetId === 'string' ? reference.assetId.trim() : '';
  const id = raw.startsWith('asset:') ? raw.slice('asset:'.length) : raw;
  if (!id) return null;
  return visualAssets.find(asset => asset.id?.trim().toLocaleLowerCase() === id.toLocaleLowerCase()) ?? null;
}

function isValueBindingControl(objectType: string): boolean {
  return objectType === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay ||
    objectType === BUILTIN_VISUAL_OBJECT_TYPES.slider ||
    objectType === BUILTIN_VISUAL_OBJECT_TYPES.numericInput;
}

function valueBindingProperty(objectType: string): string {
  return objectType === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay
    ? VISUAL_PROPERTY_KEYS.text
    : VISUAL_PROPERTY_KEYS.value;
}

function valueBindingEditorCopy(locale: EngineeringLocale, objectType: string) {
  const destination = objectType === BUILTIN_VISUAL_OBJECT_TYPES.valueDisplay
    ? locale === 'en' ? 'Displayed value' : locale === 'es' ? 'Valor mostrado' : 'Valor exibido'
    : objectType === BUILTIN_VISUAL_OBJECT_TYPES.slider
      ? locale === 'en' ? 'Slider value' : locale === 'es' ? 'Valor del slider' : 'Valor do slider'
      : locale === 'en' ? 'Numeric value' : locale === 'es' ? 'Valor numérico' : 'Valor numérico';
  return {
    ...bindingEditorCopy(locale),
    title: locale === 'en' ? 'TAG / variable value' : locale === 'es' ? 'Valor de TAG / variable' : 'Vincular valor à TAG / variável',
    destination,
    source: locale === 'en' ? 'TAG / variable' : locale === 'es' ? 'TAG / variable' : 'TAG / variável'
  };
}

function bindingEditorCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    title: 'Binding',
    destination: 'Visual property',
    source: 'Project source',
    apply: 'Apply binding',
    remove: 'Remove binding',
    noDestinations: 'This object has no bindable visual properties.',
    noSources: 'No compatible canonical project sources are available.',
    current: 'Current binding',
    browse: 'Browse project references',
    exactReference: 'Exact reference',
    exactReferencePlaceholder: 'Type the canonical TAG or variable reference',
    exactNotFound: 'No compatible source matches this exact reference.'
  };
  if (locale === 'es') return {
    title: 'Binding',
    destination: 'Propiedad visual',
    source: 'Fuente del proyecto',
    apply: 'Aplicar binding',
    remove: 'Eliminar binding',
    noDestinations: 'Este objeto no tiene propiedades visuales enlazables.',
    noSources: 'No hay fuentes canónicas compatibles disponibles.',
    current: 'Binding actual',
    browse: 'Explorar referencias del proyecto',
    exactReference: 'Referencia exacta',
    exactReferencePlaceholder: 'Escriba la referencia canónica del TAG o variable',
    exactNotFound: 'Ninguna fuente compatible coincide con esta referencia.'
  };
  return {
    title: 'Binding',
    destination: 'Propriedade visual',
    source: 'Fonte do projeto',
    apply: 'Aplicar binding',
    remove: 'Remover binding',
    noDestinations: 'Este objeto não possui propriedades visuais com binding.',
    noSources: 'Não há fontes canônicas compatíveis disponíveis.',
    current: 'Binding atual',
    browse: 'Procurar referências do projeto',
    exactReference: 'Referência exata',
    exactReferencePlaceholder: 'Digite a referência canônica do TAG ou variável',
    exactNotFound: 'Nenhuma fonte compatível corresponde a esta referência.'
  };
}

function inspectorText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    context: 'Selected context',
    tabs: { properties: 'Properties', dynamics: 'Dynamics', events: 'Events' },
    object: 'Selected object',
    screen: 'Visual definition',
    multi: (count: number) => `${count} objects selected`,
    propertiesHint: 'Static appearance, geometry and developer identity.',
    selectOne: 'Select one object to edit Dynamics or object Events.',
    objectEvents: 'Associate an object event with an existing compatible Script handler.',
    screenEvents: 'Associate a screen event with an existing compatible Script handler.'
  };
  if (locale === 'es') return {
    context: 'Contexto seleccionado',
    tabs: { properties: 'Propiedades', dynamics: 'Dinámicas', events: 'Eventos' },
    object: 'Objeto seleccionado',
    screen: 'Definición visual',
    multi: (count: number) => `${count} objetos seleccionados`,
    propertiesHint: 'Apariencia estática, geometría e identidad de desarrollo.',
    selectOne: 'Seleccione un objeto para editar Dinámicas o Eventos.',
    objectEvents: 'Asocie el evento del objeto a un handler compatible de un Script existente.',
    screenEvents: 'Asocie el evento de la pantalla a un handler compatible de un Script existente.'
  };
  return {
    context: 'Contexto selecionado',
    tabs: { properties: 'Propriedades', dynamics: 'Dinâmicas', events: 'Eventos' },
    object: 'Objeto selecionado',
    screen: 'Definição visual',
    multi: (count: number) => `${count} objetos selecionados`,
    propertiesHint: 'Aparência estática, geometria e identidade amigável ao desenvolvimento.',
    selectOne: 'Selecione um objeto para editar Dinâmicas ou Eventos do objeto.',
    objectEvents: 'Associe o evento do objeto a um handler compatível de um Script existente.',
    screenEvents: 'Associe o evento da tela a um handler compatível de um Script existente.'
  };
}
