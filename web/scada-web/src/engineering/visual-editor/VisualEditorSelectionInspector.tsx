import React from 'react';
import type { EngineeringLocale } from '../i18n';
import type { ScreenEngineering, VisualAssetEngineering, VisualElementEngineering } from '../types';
import { BindingEditor } from './binding-editor';
import { DynamoInstanceInspector } from './canvas/DynamoInstanceInspector';
import { DynamicPropertyEditor } from './dynamic-property-editor';
import { EventsEditor } from './events-editor/EventsEditor';
import { PropertyInspector } from './property-inspector';
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
  onCommand
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
}) {
  const text = inspectorText(locale);
  const selectedElement = selectedElements.length === 1 ? selectedElements[0] : null;
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
        type="button"
        role="tab"
        aria-selected={activeTab === tab}
        className={activeTab === tab ? 'is-active' : ''}
        onClick={() => onActiveTabChange(tab)}
        data-testid={`visual-editor-inspector-tab-${tab}`}
      >{text.tabs[tab]}</button>)}
    </div>

    <div className="visual-editor-inspector-panel" role="tabpanel" data-inspector-tab={activeTab}>
      {activeTab === 'properties' ? <>
        <p className="visual-editor-inspector-hint">{text.propertiesHint}</p>
        <PropertyInspector
          selectedElements={selectedElements}
          visualAssets={visualAssets}
          onMutationIntent={onMutationIntent}
          showEvents={false}
        />
        <DynamoInstanceInspector
          screen={screen}
          selectedObjectIds={selectedObjectIds}
          onCommand={onCommand}
        />
      </> : null}

      {activeTab === 'dynamics' ? selectedElement?.id ? <>
        <DynamicPropertyEditor
          element={selectedElement}
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
        <BindingEditor
          element={selectedElement}
          sourceCatalog={sourceCatalog}
          onMutationIntent={onMutationIntent}
          locale={locale}
        />
      </> : <p className="visual-editor-selection-hint">{text.selectOne}</p> : null}

      {activeTab === 'events' ? selectedElements.length <= 1 ? <>
        <p className="visual-editor-inspector-hint">{selectedElement ? text.objectEvents : text.screenEvents}</p>
        <EventsEditor
          visualDefinitionId={screen.id}
          visualObjectId={selectedElement?.id ?? null}
          sourceCatalog={sourceCatalog}
          disabled={!screen.id}
        />
      </> : <p className="visual-editor-selection-hint">{text.selectOne}</p> : null}
    </div>
  </div>;
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
