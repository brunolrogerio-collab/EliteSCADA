#!/usr/bin/env node
import { readFile, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  buildSearchIndex,
  cleanGenerated,
  detectDuplicates,
  findBatchFiles,
  processBatches,
  scaffoldBatch,
  searchIndex,
  validateCatalog,
  validateTaxonomy,
  writeCatalogOutputs
} from './lib/pipeline.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const args = process.argv.slice(2);
const command = args.shift();

function value(name, fallback = undefined) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : fallback;
}
function flag(name) { return args.includes(name); }
function usage() {
  console.log(`EliteSCADA Asset Factory\n\nCommands:\n  build [--all | --batch <file>] [--check-duplicates]\n  check\n  search <query> [--approved-only]\n  scaffold --input <dir> --category <path> --out <batch.json> [--author <name>]\n  scale-test [--count <n>]\n\nExamples:\n  node tools/asset-pipeline/cli.mjs build --all\n  node tools/asset-pipeline/cli.mjs check\n  node tools/asset-pipeline/cli.mjs search pump\n`);
}

const taxonomyPath = join(repoRoot, 'assets/catalog/taxonomy.v1.json');
async function taxonomy() { return JSON.parse(await readFile(taxonomyPath, 'utf8')); }

async function build() {
  const tax = await taxonomy();
  const taxErrors = validateTaxonomy(tax);
  if (taxErrors.length) throw new Error(taxErrors.join('\n'));
  let batches;
  const batchArg = value('--batch');
  if (batchArg) batches = [resolve(repoRoot, batchArg)];
  else if (flag('--all') || !batchArg) batches = await findBatchFiles(join(repoRoot, 'assets/sources'));
  if (!batches.length) throw new Error('No *.batch.json files found.');
  await cleanGenerated(repoRoot);
  const result = await processBatches({ repoRoot, batchFiles: batches, taxonomy: tax, writeOutputs: true });
  await writeCatalogOutputs(repoRoot, result.catalog);
  if (flag('--check-duplicates') && result.duplicates.length) {
    console.error(JSON.stringify(result.duplicates, null, 2));
    process.exitCode = 2;
  }
  console.log(JSON.stringify({ assets: result.catalog.assets.length, batches: batches.length, duplicates: result.duplicates, generatedFiles: result.generated.length }, null, 2));
}

async function check() {
  const tax = await taxonomy();
  const catalog = JSON.parse(await readFile(join(repoRoot, 'assets/catalog/builtin-assets.v1.json'), 'utf8'));
  const errors = validateCatalog(catalog, tax);
  const duplicates = detectDuplicates(catalog.assets ?? []);
  if (errors.length || duplicates.length) {
    console.error(JSON.stringify({ errors, duplicates }, null, 2));
    process.exitCode = 2;
    return;
  }
  console.log(JSON.stringify({ valid: true, assets: catalog.assets.length, categories: tax.categories.length }, null, 2));
}

async function search() {
  const query = args.filter(x => !x.startsWith('--')).join(' ');
  if (!query) throw new Error('search requires a query');
  const catalog = JSON.parse(await readFile(join(repoRoot, 'assets/catalog/builtin-assets.v1.json'), 'utf8'));
  const results = searchIndex(buildSearchIndex(catalog.assets ?? []), query, { includeNonApproved: !flag('--approved-only') });
  console.log(JSON.stringify(results, null, 2));
}

async function scaffold() {
  const input = value('--input');
  const category = value('--category');
  const out = value('--out');
  if (!input || !category || !out) throw new Error('scaffold requires --input, --category and --out');
  const batch = await scaffoldBatch({ repoRoot, inputDir: resolve(repoRoot, input), categoryPath: category, outputFile: resolve(repoRoot, out), author: value('--author', 'EliteSCADA') });
  console.log(JSON.stringify({ output: out, assets: batch.assets.length }, null, 2));
}

async function scaleTest() {
  const count = Number(value('--count', '600'));
  if (!Number.isInteger(count) || count < 500) throw new Error('scale-test count must be an integer >= 500');
  const assets = Array.from({ length: count }, (_, i) => ({
    id: `synthetic.asset.${i}`,
    key: `synthetic.asset.${i}`,
    displayName: `Synthetic Asset ${i}`,
    resourceKind: 'svg', mediaType: 'image/svg+xml', categoryPath: 'generic/symbols',
    tags: ['synthetic', `group-${i % 20}`], searchKeywords: ['scale', 'test'], styleFamily: 'test',
    sourceType: 'original-elitescada', provenance: { classification: 'original-elitescada', sourceUrl: null, author: 'test', vendor: null, derivationNote: 'synthetic' },
    license: { identifier: 'Proprietary-EliteSCADA-Original', reference: 'assets/licenses/ELITESCADA-ORIGINAL-ASSET-NOTICE.md', attributionRequired: false, commercialRedistributionAllowed: true, modificationAllowed: true },
    sourceSha256: String(i).padStart(64, '0').slice(-64).replace(/[^a-f0-9]/g, 'a'),
    sha256: String(i + 1).padStart(64, '0').slice(-64).replace(/[^a-f0-9]/g, 'b'),
    normalizedFingerprint: String(i + 2).padStart(64, '0').slice(-64).replace(/[^a-f0-9]/g, 'c'),
    dimensions: { viewBox: '0 0 1 1', width: 1, height: 1 }, assetVersion: '1.0.0', status: 'draft', editablePaintSlots: [], approval: null
  }));
  const index = buildSearchIndex(assets);
  const matches = searchIndex(index, 'synthetic group-7');
  console.log(JSON.stringify({ count, indexEntries: index.length, queryMatches: matches.length }, null, 2));
}

try {
  if (!command || command === 'help' || command === '--help') usage();
  else if (command === 'build') await build();
  else if (command === 'check') await check();
  else if (command === 'search') await search();
  else if (command === 'scaffold') await scaffold();
  else if (command === 'scale-test') await scaleTest();
  else throw new Error(`Unknown command: ${command}`);
} catch (error) {
  console.error(error.message);
  if (error.details) console.error(JSON.stringify(error.details, null, 2));
  process.exitCode = 1;
}
