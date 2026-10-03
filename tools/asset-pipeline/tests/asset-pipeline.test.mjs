import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  buildSearchIndex,
  createThumbnailSvg,
  detectDuplicates,
  processBatches,
  sanitizeAndNormalizeSvg,
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
