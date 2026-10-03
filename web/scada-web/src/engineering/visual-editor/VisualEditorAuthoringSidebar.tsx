import React from 'react';
import type { DynamoEngineering, EquipmentEngineering, ScreenEngineering, TemplateEngineering, VisualAssetEngineering } from '../types';
import type { EngineeringLocale } from '../i18n';
import type { VisualEditorMutationIntent, VisualEditorUiIntent } from './visualEditorContracts';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';
import { DynamoLibraryPalette } from './DynamoLibraryPalette';
import { EquipmentFaceplatePalette } from './EquipmentFaceplatePalette';
import { VisualEditorOutliner } from './canvas/VisualEditorOutliner';
import { BUILTIN_VISUAL_OBJECT_TYPES, VISUAL_PROPERTY_KEYS } from '../../visual-runtime';

export type VisualEditorAuthoringTab = 'structure' | 'library' | 'assets';

export type VisualEditorAssetImport = Readonly<{
  busy: boolean;
  disabled: boolean;
  disabledHint?: string;
  onFile: (file: File) => Promise<string | null | void> | string | null | void;
}>;

export function VisualEditorAuthoringSidebar({
  screen,
  selectedObjectIds,
  definitions,
  equipment,
  templates,
  visualAssets,
  locale,
  activeTab,
  onActiveTabChange,
  onUiIntent,
  onMutationIntent,
  onCommand,
  assetImport,
  allowEquipmentInstances = true
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  definitions: readonly DynamoEngineering[];
  equipment: readonly EquipmentEngineering[];
  templates: readonly TemplateEngineering[];
  visualAssets: readonly VisualAssetEngineering[];
  locale: EngineeringLocale;
  activeTab: VisualEditorAuthoringTab;
  onActiveTabChange: (tab: VisualEditorAuthoringTab) => void;
  onUiIntent: (intent: VisualEditorUiIntent) => void;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
  assetImport?: VisualEditorAssetImport;
  allowEquipmentInstances?: boolean;
}) {
  const text = authoringText(locale);
  return <div className="visual-editor-authoring-sidebar" data-testid="visual-editor-authoring-sidebar">
    <div className="visual-editor-side-tabs" role="tablist" aria-label={text.authoring}>
      {(['structure', 'library', 'assets'] as const).map(tab => <button
        key={tab}
        id={`visual-editor-side-tab-${tab}`}
        type="button"
        role="tab"
        aria-selected={activeTab === tab}
        aria-controls={`visual-editor-side-section-${tab}`}
        className={activeTab === tab ? 'is-active' : ''}
        onClick={() => onActiveTabChange(tab)}
        data-testid={`visual-editor-side-tab-${tab}`}
      >{text.tabs[tab]}</button>)}
    </div>

    <div className="visual-editor-side-panel" data-authoring-tab={activeTab}>
      <section id="visual-editor-side-section-structure" role="tabpanel" aria-labelledby="visual-editor-side-tab-structure" hidden={activeTab !== 'structure'} className={`visual-editor-side-section${activeTab === 'structure' ? ' is-active' : ''}`} data-side-section="structure">
        <VisualEditorOutliner
          screen={screen}
          selectedObjectIds={selectedObjectIds}
          onSelection={(objectId, mode) => onUiIntent({
            kind: 'selection.change',
            objectIds: [objectId],
            mode
          })}
        />
      </section>

      <section id="visual-editor-side-section-library" role="tabpanel" aria-labelledby="visual-editor-side-tab-library" hidden={activeTab !== 'library'} className={`visual-editor-side-section${activeTab === 'library' ? ' is-active' : ''}`} data-side-section="library">
        <DynamoLibraryPalette
          definitions={definitions}
          locale={locale}
          onMutationIntent={onMutationIntent}
        />
        {allowEquipmentInstances ? <EquipmentFaceplatePalette equipment={equipment} templates={templates} locale={locale} onMutationIntent={onMutationIntent}/> : null}
      </section>

      <section id="visual-editor-side-section-assets" role="tabpanel" aria-labelledby="visual-editor-side-tab-assets" hidden={activeTab !== 'assets'} className={`visual-editor-side-section${activeTab === 'assets' ? ' is-active' : ''}`} data-side-section="assets">
        <section className="visual-editor-asset-library" data-testid="visual-editor-asset-library">
          <header>
            <strong>{text.assets}</strong>
            <span>{text.assetHint}</span>
          </header>
          {assetImport ? <label className="visual-editor-file-import">
            <span>{assetImport.busy ? text.importing : text.importAsset}</span>
            <input
              type="file"
              accept="image/png,image/jpeg,image/bmp,image/svg+xml,.png,.jpg,.jpeg,.bmp,.svg"
              disabled={assetImport.disabled || assetImport.busy}
              onChange={event => {
                const file = event.currentTarget.files?.[0];
                event.currentTarget.value = '';
                if (file) void assetImport.onFile(file);
              }}
            />
          </label> : null}
          {assetImport?.disabledHint && assetImport.disabled ? <small>{assetImport.disabledHint}</small> : null}
          <div className="visual-editor-asset-list" role="list" aria-label={text.assets}>
            {visualAssets.length === 0 ? <span>{text.noAssets}</span> : visualAssets.map(asset => <div
              role="listitem"
              className="visual-editor-asset-list__item"
              key={asset.id ?? `${asset.name}:${asset.originalFileName}`}
            >
              <strong>{asset.name}</strong>
              <small>{asset.originalFileName}</small>
              <code>{asset.mediaType === 'image/svg+xml' ? 'SVG' : `${asset.pixelWidth ?? '?'}×${asset.pixelHeight ?? '?'}`}</code>
              {asset.id ? <button
                type="button"
                onClick={() => onMutationIntent({
                  kind: 'object.add',
                  objectType: asset.mediaType === 'image/svg+xml'
                    ? BUILTIN_VISUAL_OBJECT_TYPES.svgSymbol
                    : BUILTIN_VISUAL_OBJECT_TYPES.image,
                  initialProperties: {
                    [VISUAL_PROPERTY_KEYS.assetRef]: { assetId: asset.id! },
                    [VISUAL_PROPERTY_KEYS.width]: asset.pixelWidth ?? 120,
                    [VISUAL_PROPERTY_KEYS.height]: asset.pixelHeight ?? 120
                  }
                })}
              >{asset.mediaType === 'image/svg+xml' ? text.addSvg : text.addImage}</button> : null}
            </div>)}
          </div>
        </section>
      </section>
    </div>
  </div>;
}

function authoringText(locale: EngineeringLocale) {
  if (locale === 'en') return {
    authoring: 'Authoring surface',
    tabs: { structure: 'Structure', library: 'Library', assets: 'Assets' },
    assets: 'Visual assets',
    assetHint: 'Canonical project assets for image objects and surface backgrounds.',
    importAsset: 'Import image',
    importing: 'Importing…',
    noAssets: 'No visual assets in this project.',
    addSvg: 'Add editable SVG',
    addImage: 'Add image'
  };
  if (locale === 'es') return {
    authoring: 'Superficie de autoría',
    tabs: { structure: 'Estructura', library: 'Biblioteca', assets: 'Assets' },
    assets: 'Assets visuales',
    assetHint: 'Assets canónicos del proyecto para imágenes y fondos.',
    importAsset: 'Importar imagen',
    importing: 'Importando…',
    noAssets: 'No hay assets visuales en este proyecto.',
    addSvg: 'Agregar SVG editable',
    addImage: 'Agregar imagen'
  };
  return {
    authoring: 'Superfície de autoria',
    tabs: { structure: 'Estrutura', library: 'Biblioteca', assets: 'Assets' },
    assets: 'Assets visuais',
    assetHint: 'Assets canônicos do projeto para objetos de imagem e fundos.',
    importAsset: 'Importar imagem',
    importing: 'Importando…',
    noAssets: 'Nenhum asset visual neste projeto.',
    addSvg: 'Adicionar SVG editável',
    addImage: 'Adicionar imagem'
  };
}
