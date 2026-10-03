import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import {
  buildSearchIndex,
  cleanGenerated,
  createThumbnailSvg,
  detectDuplicates,
  processBatches,
  sanitizeAndNormalizeSvg,
  scaffoldBatch,
  searchIndex,
  sha256,
  validateCatalog,
  validateTaxonomy
} from '../lib/pipeline.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const taxonomyPath = join(repoRoot, 'assets/catalog/taxonomy.v1.json');
const batchPath = join(repoRoot, 'assets/sources/seed/seed.batch.json');

async function fixtures() {
  const taxonomy = JSON.parse(await readFile(taxonomyPath, 'utf8'));
  const result = await processBatches({ repoRoot, batchFiles: [batchPath], taxonomy, writeOutputs: false });
  return { taxonomy, catalog: result.catalog };
}

const SAFE = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><defs><linearGradient id="g"><stop offset="0" stop-color="#000000"/><stop offset="1" stop-color="#FFFFFF"/></linearGradient></defs><rect x="10" y="10" width="80" height="80" fill="url(#g)" stroke="#333333" stroke-width="2"/></svg>';

test('1. valid manifest is accepted', async () => {
  const { taxonomy, catalog } = await fixtures();
  assert.deepEqual(validateCatalog(catalog, taxonomy), []);
});

test('2. invalid manifest is rejected', async () => {
  const { taxonomy, catalog } = await fixtures();
  const invalid = structuredClone(catalog);
  invalid.schemaVersion = 999;
  assert.ok(validateCatalog(invalid, taxonomy).some(x => x.includes('schema/schemaVersion')));
});

test('3. duplicate stable asset ID is rejected', async () => {
  const { taxonomy, catalog } = await fixtures();
  const invalid = structuredClone(catalog);
  const duplicate = structuredClone(invalid.assets[0]);
  duplicate.key += '.copy';
  invalid.assets.push(duplicate);
  assert.ok(validateCatalog(invalid, taxonomy).some(x => x.includes('duplicate stable asset id')));
});

test('4. duplicate category ID is rejected', async () => {
  const { taxonomy } = await fixtures();
  const invalid = structuredClone(taxonomy);
  invalid.categories.push(structuredClone(invalid.categories[0]));
  assert.ok(validateTaxonomy(invalid).some(x => x.includes('duplicate category id')));
});

test('5. invalid taxonomy path is rejected', async () => {
  const { taxonomy } = await fixtures();
  const invalid = structuredClone(taxonomy);
  invalid.categories.push({ id: 'Bad Path/Upper', labels: { 'pt-BR': 'Ruim', en: 'Bad' } });
  assert.ok(validateTaxonomy(invalid).some(x => x.includes('invalid taxonomy path')));
});

test('6. safe SVG is accepted and normalized deterministically', () => {
  const result = sanitizeAndNormalizeSvg(SAFE);
  assert.match(result.normalized, /<linearGradient id="a1">/);
  assert.match(result.normalized, /fill="url\(#a1\)"/);
  assert.equal(result.viewBox, '0 0 100 100');
});

test('7. SVG script is rejected', () => {
  assert.throws(() => sanitizeAndNormalizeSvg('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><script>alert(1)</script></svg>'), /script/i);
});

test('8. SVG event handler is rejected', () => {
  assert.throws(() => sanitizeAndNormalizeSvg('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect x="0" y="0" width="1" height="1" onclick="alert(1)"/></svg>'), /event handler/i);
});

test('9. external SVG URL is rejected', () => {
  assert.throws(() => sanitizeAndNormalizeSvg('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><linearGradient id="g" href="https://example.com/x.svg#g"/></svg>'), /external|href/i);
});

test('10. javascript URL is rejected', () => {
  assert.throws(() => sanitizeAndNormalizeSvg('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><linearGradient id="g" href="javascript:alert(1)"/></svg>'), /javascript|href/i);
});

test('11. malformed SVG is rejected', () => {
  assert.throws(() => sanitizeAndNormalizeSvg('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><g><rect x="0" y="0" width="1" height="1"/></svg>'), /malformed/i);
});

test('12. missing provenance blocks approval', async () => {
  const { taxonomy, catalog } = await fixtures();
  const invalid = structuredClone(catalog);
  invalid.assets[0].status = 'approved';
  invalid.assets[0].provenance = null;
  assert.ok(validateCatalog(invalid, taxonomy).some(x => x.includes('approved asset requires distributable provenance')));
});

test('13. missing or unknown license blocks approval', async () => {
  const { taxonomy, catalog } = await fixtures();
  const invalid = structuredClone(catalog);
  invalid.assets[0].status = 'approved';
  invalid.assets[0].provenance.classification = 'original-elitescada';
  invalid.assets[0].license = { identifier: 'UNKNOWN', reference: null, attributionRequired: false, commercialRedistributionAllowed: false, modificationAllowed: false };
  assert.ok(validateCatalog(invalid, taxonomy).some(x => x.includes('known license')));
});

test('generated/original cannot be approved without explicit review evidence', async () => {
  const { taxonomy, catalog } = await fixtures();
  const invalid = structuredClone(catalog);
  invalid.assets[0].status = 'approved';
  invalid.assets[0].approval = null;
  assert.ok(validateCatalog(invalid, taxonomy).some(x => x.includes('explicit review evidence')));
});

test('14. hash is deterministic', () => {
  const a = sanitizeAndNormalizeSvg(SAFE);
  const b = sanitizeAndNormalizeSvg(SAFE);
  assert.equal(a.sha256, b.sha256);
  assert.equal(a.sourceSha256, sha256(SAFE));
});

test('15. thumbnail generation is deterministic', () => {
  const normalized = sanitizeAndNormalizeSvg(SAFE);
  assert.equal(createThumbnailSvg(normalized.normalized, normalized.viewBox), createThumbnailSvg(normalized.normalized, normalized.viewBox));
});

test('16. duplicate detection reports raw and normalized duplicates without destructive merge', async () => {
  const { catalog } = await fixtures();
  const a = structuredClone(catalog.assets[0]);
  const b = structuredClone(a);
  b.id += '.copy';
  b.key += '.copy';
  const issues = detectDuplicates([a, b]);
  assert.ok(issues.some(x => x.kind === 'exact-source-duplicate'));
  assert.ok(issues.some(x => x.kind === 'normalized-svg-duplicate'));
});

test('17. 600 synthetic catalog entries validate, index and search', async () => {
  const { taxonomy, catalog } = await fixtures();
  const sample = structuredClone(catalog.assets[0]);
  sample.provenance.classification = 'original-elitescada';
  sample.status = 'draft';
  sample.approval = null;
  sample.categoryPath = 'generic/symbols';
  const synthetic = Array.from({ length: 600 }, (_, i) => {
    const item = structuredClone(sample);
    item.id = `synthetic.asset.${i}`;
    item.key = `synthetic.asset.${i}`;
    item.displayName = `Synthetic Asset ${i}`;
    item.tags = ['synthetic', `group-${i % 20}`];
    item.searchKeywords = ['scale', 'test'];
    item.sourceSha256 = sha256(`source-${i}`);
    item.sha256 = sha256(`normalized-${i}`);
    item.normalizedFingerprint = sha256(`fingerprint-${i}`);
    return item;
  });
  const large = { ...catalog, assets: synthetic };
  assert.deepEqual(validateCatalog(large, taxonomy), []);
  const index = buildSearchIndex(synthetic);
  assert.equal(index.length, 600);
  assert.equal(searchIndex(index, 'synthetic group 7').length, 30);
});

test('18. representative seed fully passes source -> SVG -> manifest -> taxonomy -> dedupe pipeline', async () => {
  const { taxonomy } = await fixtures();
  const result = await processBatches({ repoRoot, batchFiles: [batchPath], taxonomy, writeOutputs: false });
  assert.equal(result.catalog.assets.length, 10);
  assert.deepEqual(validateCatalog(result.catalog, taxonomy), []);
  assert.deepEqual(result.duplicates, []);
  const roots = new Set(result.catalog.assets.map(x => x.categoryPath.split('/')[0]));
  for (const required of ['industrial', 'pid', 'sanitation', 'electrical', 'building', 'residential', 'navigation', 'ui', 'indicators', 'flow']) assert.ok(roots.has(required), `missing seed root ${required}`);
  assert.ok(result.catalog.assets.every(x => x.status === 'draft'));
});


const REGRESSION_TAXONOMY = {
  schema: 'elitescada.asset-taxonomy',
  schemaVersion: 1,
  categories: [
    { id: 'generic', labels: { 'pt-BR': 'Generico', en: 'Generic' } },
    { id: 'generic/symbols', labels: { 'pt-BR': 'Simbolos', en: 'Symbols' } }
  ]
};
const REGRESSION_SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect x="1" y="1" width="8" height="8"/></svg>';
const REGRESSION_LICENSE = { identifier: 'Test', reference: 'LICENSE', attributionRequired: false, commercialRedistributionAllowed: true, modificationAllowed: true };

async function regressionRoot() {
  const root = await mkdtemp(join(tmpdir(), 'asset-factory-'));
  await mkdir(join(root, 'assets', 'sources', 'batch'), { recursive: true });
  await mkdir(join(root, 'assets', 'builtin'), { recursive: true });
  await writeFile(join(root, 'assets', 'builtin', 'README.md'), 'keep');
  await writeFile(join(root, 'assets', 'sources', 'batch', 'ok.svg'), REGRESSION_SVG);
  return root;
}

function regressionBatch(sourceFile = 'assets/sources/batch/ok.svg', categoryPath = 'generic/symbols') {
  return {
    schema: 'elitescada.asset-batch',
    schemaVersion: 1,
    defaults: { styleFamily: 'test', license: REGRESSION_LICENSE },
    assets: [{
      id: 'builtin.asset.generic.symbols.test',
      key: 'generic.symbols.test',
      displayName: 'Test',
      sourceFile,
      categoryPath,
      tags: [],
      searchKeywords: [],
      status: 'draft',
      provenance: { classification: 'original-elitescada', author: 'test', sourceUrl: null, derivationNote: 'test' }
    }]
  };
}

async function regressionBatchFile(root, batch) {
  const path = join(root, 'assets', 'sources', 'batch', 'test.batch.json');
  await writeFile(path, JSON.stringify(batch));
  return path;
}

test('19. sourceFile cannot escape assets/sources', async () => {
  const root = await regressionRoot();
  const batchFile = await regressionBatchFile(root, regressionBatch('../../../../etc/passwd'));
  await assert.rejects(
    processBatches({ repoRoot: root, batchFiles: [batchFile], taxonomy: REGRESSION_TAXONOMY, writeOutputs: false }),
    /must remain under assets\/sources/
  );
});

test('20. invalid category cannot traverse derivative output directories', async () => {
  const root = await regressionRoot();
  const batchFile = await regressionBatchFile(root, regressionBatch('assets/sources/batch/ok.svg', '../../outside'));
  await assert.rejects(
    processBatches({ repoRoot: root, batchFiles: [batchFile], taxonomy: REGRESSION_TAXONOMY, writeOutputs: true }),
    /unknown taxonomy path/
  );
  await assert.rejects(readFile(join(root, 'outside', 'test.svg'), 'utf8'));
});

test('21. cleanGenerated removes stale built-in SVG while preserving non-generated README', async () => {
  const root = await regressionRoot();
  const staleDir = join(root, 'assets', 'builtin', 'generic', 'symbols');
  await mkdir(staleDir, { recursive: true });
  await writeFile(join(staleDir, 'stale.svg'), REGRESSION_SVG);
  await cleanGenerated(root);
  assert.equal(await readFile(join(root, 'assets', 'builtin', 'README.md'), 'utf8'), 'keep');
  await assert.rejects(readFile(join(staleDir, 'stale.svg'), 'utf8'));
});


test('22. batch file itself cannot escape assets/sources', async () => {
  const root = await regressionRoot();
  const path = join(root, 'outside.batch.json');
  await writeFile(path, JSON.stringify(regressionBatch()));
  await assert.rejects(
    processBatches({ repoRoot: root, batchFiles: [path], taxonomy: REGRESSION_TAXONOMY, writeOutputs: false }),
    /batch file must remain under assets\/sources/
  );
});

test('23. derivative filenames stay unique when keys share the same final segment', async () => {
  const root = await regressionRoot();
  await writeFile(
    join(root, 'assets', 'sources', 'batch', 'other.svg'),
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><circle cx="5" cy="5" r="3"/></svg>'
  );
  const batch = regressionBatch();
  batch.assets = [
    { ...batch.assets[0], id: 'builtin.asset.one', key: 'generic.symbols.one.basic', sourceFile: 'assets/sources/batch/ok.svg' },
    { ...batch.assets[0], id: 'builtin.asset.two', key: 'generic.symbols.two.basic', sourceFile: 'assets/sources/batch/other.svg' }
  ];
  const batchFile = await regressionBatchFile(root, batch);
  const result = await processBatches({ repoRoot: root, batchFiles: [batchFile], taxonomy: REGRESSION_TAXONOMY, writeOutputs: true });
  assert.equal(result.generated.length, 4);
  assert.equal(new Set(result.generated).size, 4);
});

test('24. scaffold stays under assets/sources and produces a consumable source path', async () => {
  const root = await regressionRoot();
  const input = join(root, 'assets', 'sources', 'scaffold');
  await mkdir(input, { recursive: true });
  await writeFile(join(input, 'thing.svg'), REGRESSION_SVG);
  const output = join(root, 'assets', 'sources', 'scaffold.batch.json');
  const batch = await scaffoldBatch({ repoRoot: root, inputDir: input, categoryPath: 'generic/symbols', outputFile: output });
  assert.equal(batch.assets[0].sourceFile, 'assets/sources/scaffold/thing.svg');
  await assert.rejects(
    scaffoldBatch({ repoRoot: root, inputDir: tmpdir(), categoryPath: 'generic/symbols', outputFile: output }),
    /scaffold input must remain under assets\/sources/
  );
  await assert.rejects(
    scaffoldBatch({ repoRoot: root, inputDir: input, categoryPath: 'generic/symbols', outputFile: join(root, 'outside.batch.json') }),
    /scaffold output must remain under assets\/sources/
  );
});

test('25. CLI single-batch build is validation-only and cannot clean or publish global outputs', async () => {
  const cliSource = await readFile(join(repoRoot, 'tools/asset-pipeline/cli.mjs'), 'utf8');
  const start = cliSource.indexOf('if (batchArg) {');
  const end = cliSource.indexOf('const batches = await findBatchFiles', start);
  assert.ok(start >= 0 && end > start);
  const batchBranch = cliSource.slice(start, end);
  assert.match(batchBranch, /writeOutputs: false/);
  assert.match(batchBranch, /mutatesGlobalOutputs: false/);
  assert.doesNotMatch(batchBranch, /cleanGenerated|writeCatalogOutputs/);
});
