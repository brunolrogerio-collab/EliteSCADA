import { expect, test } from '@playwright/test';
import type { EngineeringSnapshot } from '../src/engineering/types';
import {
  buildLibraryCatalogEntries,
  filterLibraryCatalogEntries,
  libraryCatalogOriginCounts,
  listLibraryCatalogKinds
} from '../src/engineering/libraryCatalogModel';
import {
  libraryCatalogCategoryLabel,
  libraryCatalogCategoryMatches,
  resolveDynamoCategoryPath
} from '../src/engineering/libraryCatalogTaxonomy';

function snapshot(dynamos: unknown[] = [], visualAssets: unknown[] = [], extras: Record<string, unknown> = {}): EngineeringSnapshot {
  return {
    workspace: {
      projectKey: 'catalog-test',
      projectName: 'Catalog Test',
      baseRevision: 1,
      checkedOutAtUtc: '2026-10-03T00:00:00Z',
      lastSavedAtUtc: '2026-10-03T00:00:00Z',
      isDirty: false,
      changeVersion: 4,
      tagCount: 0,
      alarmCount: 0,
      dataSourceCount: 0,
      templateCount: 0,
      equipmentCount: 0,
      dynamoCount: dynamos.length,
      screenCount: 0,
      popupCount: 0,
      securityRoleCount: 0,
      commandCount: 0,
      visualAssetCount: visualAssets.length
    },
    package: {
      schema: 'scada.engineering',
      schemaVersion: 10,
      exportedAt: '2026-10-03T00:00:00Z',
      tags: [],
      alarms: [],
      dynamos: dynamos as never[],
      visualAssets: visualAssets as never[],
      ...extras
    }
  };
}

test('catalog consumes Asset Factory taxonomy labels and preserves hierarchical matching', () => {
  expect(libraryCatalogCategoryLabel('industrial/process/pumps', 'pt-BR')).toBe('Bombas');
  expect(libraryCatalogCategoryLabel('industrial/process/pumps', 'en')).toBe('Pumps');
  expect(libraryCatalogCategoryLabel('industrial/process/pumps', 'es')).toBe('Bombas');
  expect(libraryCatalogCategoryMatches('industrial/process/pumps', 'industrial')).toBe(true);
  expect(libraryCatalogCategoryMatches('industrial/process/pumps', 'industrial/process')).toBe(true);
  expect(libraryCatalogCategoryMatches('electrical/protection', 'industrial')).toBe(false);
});

test('legacy Dynamo categories map deterministically and unknown values fall back safely', () => {
  expect(resolveDynamoCategoryPath('pump')).toBe('industrial/process/pumps');
  expect(resolveDynamoCategoryPath('motor')).toBe('industrial/rotating/motors');
  expect(resolveDynamoCategoryPath('substation')).toBe('electrical/protection');
  expect(resolveDynamoCategoryPath('unknown-future-flat-value')).toBe('generic/symbols');
  expect(resolveDynamoCategoryPath(undefined)).toBe('generic/symbols');
});

test('unified catalog separates built-in, project and associated-library authority', () => {
  const model = snapshot([
    {
      id: 'dynamo-built-in',
      key: 'builtin.pump',
      name: 'Built-in Pump',
      properties: { category: 'pump', tags: 'pump;water', keywords: 'centrifugal' },
      metadata: { builtinLibrary: 'true' },
      parameters: [],
      elements: []
    },
    {
      id: 'dynamo-user',
      key: 'user.motor',
      name: 'User Motor',
      properties: { category: 'motor', tags: 'motor;drive' },
      parameters: [],
      elements: []
    }
  ], [{
    id: 'asset-user',
    key: 'asset.user.arrow',
    name: 'User Arrow',
    originalFileName: 'arrow.svg',
    mediaType: 'image/svg+xml',
    byteLength: 100,
    sha256: 'a'.repeat(64),
    metadata: { categoryPath: 'navigation/arrows', tags: 'arrow;next', searchKeywords: 'forward' }
  }]);

  const associated = [{
    library: {
      libraryId: '11111111-1111-4111-8111-111111111111',
      name: 'External Process',
      version: '2.0.0',
      contentSha256: 'b'.repeat(64),
      resourceCount: 1,
      byteLength: 1000
    },
    resources: [{
      resourceId: '22222222-2222-4222-8222-222222222222',
      kind: 'screen',
      sourceKey: 'screen.external',
      displayName: 'External Screen',
      payloadPath: 'resources/screen/external.json',
      dependencies: []
    }]
  }];

  const entries = buildLibraryCatalogEntries(model, associated);
  expect(libraryCatalogOriginCounts(entries)).toEqual({ builtin: 1, project: 2, associated: 1 });
  expect(listLibraryCatalogKinds(entries)).toEqual(['dynamo', 'screen', 'visual-asset']);

  expect(filterLibraryCatalogEntries(entries, { origin: 'builtin' }).map(entry => entry.name))
    .toEqual(['Built-in Pump']);
  expect(filterLibraryCatalogEntries(entries, { origin: 'project', kind: 'visual-asset' }).map(entry => entry.name))
    .toEqual(['User Arrow']);
  expect(filterLibraryCatalogEntries(entries, { origin: 'associated' }).map(entry => entry.name))
    .toEqual(['External Screen']);

  const external = entries.find(entry => entry.origin === 'associated')!;
  expect(external.readOnly).toBe(true);
  expect(external.projectOwned).toBe(false);
  expect(external.library?.name).toBe('External Process');
});

test('search, tags, resource kind and hierarchical categories compose without mutating source entries', () => {
  const model = snapshot([
    {
      id: 'pump-1',
      key: 'pump.one',
      name: 'Bomba de recalque',
      properties: { category: 'pump', tags: 'water;critical', keywords: 'centrifugal discharge' },
      metadata: { builtinLibrary: 'true' },
      parameters: [],
      elements: []
    },
    {
      id: 'valve-1',
      key: 'valve.one',
      name: 'Válvula de entrada',
      properties: { category: 'valve', tags: 'water;isolation' },
      parameters: [],
      elements: []
    }
  ]);
  const entries = buildLibraryCatalogEntries(model);
  const before = JSON.stringify(entries);

  expect(filterLibraryCatalogEntries(entries, { query: 'recalque' }).map(entry => entry.sourceKey))
    .toEqual(['pump.one']);
  expect(filterLibraryCatalogEntries(entries, { query: 'centrifugal' }).map(entry => entry.sourceKey))
    .toEqual(['pump.one']);
  expect(filterLibraryCatalogEntries(entries, { tag: 'critical' }).map(entry => entry.sourceKey))
    .toEqual(['pump.one']);
  expect(filterLibraryCatalogEntries(entries, { categoryPath: 'industrial/process' }).map(entry => entry.sourceKey).sort())
    .toEqual(['pump.one', 'valve.one']);
  expect(filterLibraryCatalogEntries(entries, { kind: 'dynamo', origin: 'project' }).map(entry => entry.sourceKey))
    .toEqual(['valve.one']);

  expect(JSON.stringify(entries)).toBe(before);
});

test('600 synthetic resources remain deterministically searchable and filterable', () => {
  const dynamos = Array.from({ length: 600 }, (_, index) => ({
    id: `synthetic-${index}`,
    key: `synthetic.pump.${index}`,
    name: `Synthetic Pump ${index}`,
    properties: {
      category: index % 3 === 0 ? 'pump' : index % 3 === 1 ? 'motor' : 'valve',
      tags: index % 2 === 0 ? 'even;synthetic' : 'odd;synthetic',
      keywords: `batch-${Math.floor(index / 50)} item-${index}`
    },
    metadata: index % 5 === 0 ? { builtinLibrary: 'true' } : undefined,
    parameters: [],
    elements: []
  }));

  const entries = buildLibraryCatalogEntries(snapshot(dynamos));
  expect(entries).toHaveLength(600);
  expect(filterLibraryCatalogEntries(entries, { query: 'item-599' }).map(entry => entry.sourceKey))
    .toEqual(['synthetic.pump.599']);
  expect(filterLibraryCatalogEntries(entries, { categoryPath: 'industrial/process/pumps' })).toHaveLength(200);
  expect(filterLibraryCatalogEntries(entries, { tag: 'even' })).toHaveLength(300);
  expect(filterLibraryCatalogEntries(entries, { origin: 'builtin' })).toHaveLength(120);
});
