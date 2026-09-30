import React from 'react';
import type { DynamoEngineering, ScreenEngineering, VisualAssetEngineering } from '../types';
import type { EngineeringLocale } from '../i18n';
import type { VisualEditorMutationIntent, VisualEditorUiIntent } from './visualEditorContracts';
import type { VisualEditorKeyboardCommand } from './visualEditorKeyboardModel';
import { DynamoLibraryPalette } from './DynamoLibraryPalette';
import { ObjectPalette } from './object-palette';
import { VisualDefinitionSurfaceInspector } from './canvas/VisualDefinitionSurfaceInspector';
import { VisualEditorOutliner } from './canvas/VisualEditorOutliner';

export type VisualEditorAuthoringTab = 'structure' | 'library' | 'assets';

export type VisualEditorAssetImport = Readonly<{
  busy: boolean;
  disabled: boolean;
  disabledHint?: string;
  onFile: (file: File) => Promise<void> | void;
}>;

export function VisualEditorAuthoringSidebar({
  screen,
  selectedObjectIds,
  definitions,
  visualAssets,
  locale,
  activeTab,
  onActiveTabChange,
  onUiIntent,
  onMutationIntent,
  onCommand,
  assetImport
}: {
  screen: ScreenEngineering;
  selectedObjectIds: readonly string[];
  definitions: readonly DynamoEngineering[];
  visualAssets: readonly VisualAssetEngineering[];
  locale: EngineeringLocale;
  activeTab: VisualEditorAuthoringTab;
  onActiveTabChange: (tab: VisualEditorAuthoringTab) => void;
  onUiIntent: (intent: VisualEditorUiIntent) => void;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  onCommand?: (command: VisualEditorKeyboardCommand) => void;
  assetImport?: VisualEditorAssetImport;
}) {
  const text = authoringText(locale);
  return <div className="visual-editor-authoring-sidebar" data-testid="visual-editor-authoring-sidebar">
    <div className="visual-editor-side-tabs" role="tablist" aria-label={text.authoring}>
      {(['structure', 'library', 'assets'] as const).map(tab => <button
        key={tab}
        type="button"
        role="tab"
        aria-selected={activeTab === tab}
        className={activeTab === tab ? 'is-active' : ''}
        onClick={() => onActiveTabChange(tab)}
        data-testid={`visual-editor-side-tab-${tab}`}
      >{text.tabs[tab]}</button>)}
    </div>

    <div className="visual-editor-side-panel" data-authoring-tab={activeTab}>
      <section className={`visual-editor-side-section${activeTab === 'structure' ? ' is-active' : ''}`} data-side-section="structure">
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

      <section className={`visual-editor-side-section${activeTab === 'library' ? ' is-active' : ''}`} data-side-section="library">
        <ObjectPalette onMutationIntent={onMutationIntent} />
        <DynamoLibraryPalette
          definitions={definitions}
          locale={locale}
          onMutationIntent={onMutationIntent}
        />
      </section>

      <section className={`visual-editor-side-section${activeTab === 'assets' ? ' is-active' : ''}`} data-side-section="assets">
        <section className="visual-editor-asset-library" data-testid="visual-editor-asset-library">
          <header>
            <strong>{text.assets}</strong>
            <span>{text.assetHint}</span>
          </header>
          {assetImport ? <label className="visual-editor-file-import">
            <span>{assetImport.busy ? text.importing : text.importAsset}</span>
            <input
              type="file"
              accept="image/png,image/jpeg,image/bmp"
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
              <code>{asset.pixelWidth ?? '?'}×{asset.pixelHeight ?? '?'}</code>
            </div>)}
          </div>
        </section>
        <VisualDefinitionSurfaceInspector screen={screen} onCommand={onCommand} />
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
    noAssets: 'No visual assets in this project.'
  };
  if (locale === 'es') return {
    authoring: 'Superficie de autoría',
    tabs: { structure: 'Estructura', library: 'Biblioteca', assets: 'Assets' },
    assets: 'Assets visuales',
    assetHint: 'Assets canónicos del proyecto para imágenes y fondos.',
    importAsset: 'Importar imagen',
    importing: 'Importando…',
    noAssets: 'No hay assets visuales en este proyecto.'
  };
  return {
    authoring: 'Superfície de autoria',
    tabs: { structure: 'Estrutura', library: 'Biblioteca', assets: 'Assets' },
    assets: 'Assets visuais',
    assetHint: 'Assets canônicos do projeto para objetos de imagem e fundos.',
    importAsset: 'Importar imagem',
    importing: 'Importando…',
    noAssets: 'Nenhum asset visual neste projeto.'
  };
}
