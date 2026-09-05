import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type APIRequestContext } from '@playwright/test';
import { EEE_HMI } from './c11-eee-demo-hmi';
import { EEE_PATHS } from './c11-eee-demo-foundation-canonical';
import { buildEeeDemoPackage } from './c11-eee-demo-hmi';

const canonicalProjectKey = 'eee-demo';
const canonicalProjectName = 'EliteSCADA — EEE Demo';
const sourceProductSha = '5962bee401fadd700041e7c61cd430d4b4f28e27';
const packageFileName = 'EliteSCADA-EEE-Demo.escadapkg';
const packageMediaType = 'application/vnd.elitescada.project-package';
const builtinDynamoCount = 8;
const applicationDynamoKey = 'eee.dynamo.pump';

test('C11 canonical eee-demo Active project exports, inspects and re-previews as a portable package', async ({ request }) => {
  await createFreshProject(request);
  const original = await loadWorking(request);
  expect(builtinDynamos(original.dynamos)).toHaveLength(builtinDynamoCount);
  expect(applicationDynamos(original.dynamos)).toHaveLength(0);

  const candidate = buildEeeDemoPackage(original);

  expect(candidate.startupScreenId).toBe(EEE_HMI.screens.overview.id);
  expect(candidate.screens).toHaveLength(6);
  expect(candidate.popups).toHaveLength(2);
  expect(candidate.dynamos).toHaveLength(1);
  expect(candidate.dynamos[0].key).toBe(applicationDynamoKey);

  await previewAndApply(request, candidate);
  const saved = await savePublishActivate(request);

  await expect.poll(async () => {
    const response = await request.get('/api/runtime/application');
    if (!response.ok()) return null;
    const body = await response.json() as any;
    return body.projectKey === canonicalProjectKey && body.revision === saved.revision ? body : null;
  }, { timeout: 15_000 }).not.toBeNull();

  const activeResponse = await request.get('/api/runtime/application');
  expect(
    activeResponse.ok(),
    `C11 Active Runtime application read failed: HTTP ${activeResponse.status()} ${await activeResponse.text()}`
  ).toBeTruthy();
  const active = await activeResponse.json() as any;

  expect(active.mode).toBe('engineering');
  expect(active.projectKey).toBe(canonicalProjectKey);
  expect(active.revision).toBe(saved.revision);
  expect(active.package.startupScreenId).toBe(EEE_HMI.screens.overview.id);
  expect(active.package.screens).toHaveLength(6);
  expect(active.package.popups).toHaveLength(2);
  expect(builtinDynamos(active.package.dynamos)).toHaveLength(builtinDynamoCount);
  expect(applicationDynamos(active.package.dynamos).map((x: any) => x.key)).toEqual([applicationDynamoKey]);

  await expect.poll(async () => Number((await readCurrent(request, EEE_PATHS.levelPct)).value), { timeout: 15_000 })
    .toBeGreaterThan(0);

  const packageResponse = await request.get(
    `/api/project-package/export?projectKey=${encodeURIComponent(canonicalProjectKey)}&projectName=${encodeURIComponent(canonicalProjectName)}`
  );
  expect(
    packageResponse.ok(),
    `C11 package export failed: HTTP ${packageResponse.status()} ${await packageResponse.text()}`
  ).toBeTruthy();
  expect(packageResponse.headers()['content-type']).toContain(packageMediaType);
  const packageBytes = await packageResponse.body();
  expect(packageBytes.length).toBeGreaterThan(0);

  const inspectionResponse = await request.post('/api/project-package/inspect', {
    headers: {
      accept: 'application/json',
      'content-type': packageMediaType
    },
    data: packageBytes
  });
  expect(
    inspectionResponse.ok(),
    `C11 package inspection failed: HTTP ${inspectionResponse.status()} ${await inspectionResponse.text()}`
  ).toBeTruthy();
  const inspection = await inspectionResponse.json() as any;
  expect(inspection.manifest.projectKey).toBe(canonicalProjectKey);
  expect(inspection.manifest.projectName).toBe(canonicalProjectName);
  expect(inspection.manifest.product).toBe('EliteSCADA');
  expect(inspection.manifest.engineeringSchema).toBe('scada.engineering');
  expect(inspection.engineering.screens).toBe(6);
  expect(inspection.engineering.popups).toBe(2);
  expect(inspection.engineering.dynamos).toBe(builtinDynamoCount + 1);
  expect(inspection.engineering.tags).toBeGreaterThan(0);
  expect(inspection.engineering.alarms).toBeGreaterThan(0);
  expect(inspection.engineering.dataSources).toBeGreaterThan(0);
  expect(inspection.engineering.commands).toBeGreaterThan(0);
  expect(inspection.engineering.securityRoles).toBeGreaterThan(0);

  const previewResponse = await request.post('/api/project-package/import/preview?mode=CreateAndUpdate', {
    headers: {
      accept: 'application/json',
      'content-type': packageMediaType
    },
    data: packageBytes
  });
  expect(
    previewResponse.ok(),
    `C11 package re-preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`
  ).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
  expect(preview.canApply, `C11 package re-preview issues: ${JSON.stringify(preview.items ?? [], null, 2)}`).toBe(true);
  expect(preview.errorCount).toBe(0);

  const c11DemoCommitSha = process.env.GITHUB_SHA ?? 'local-worktree';
  if (process.env.CI) expect(c11DemoCommitSha).toMatch(/^[0-9a-f]{40}$/i);

  const outputDirectory = path.resolve(process.cwd(), 'c11-eee-demo-artifacts');
  await mkdir(outputDirectory, { recursive: true });
  const packagePath = path.join(outputDirectory, packageFileName);
  await writeFile(packagePath, packageBytes);

  const sha256 = createHash('sha256').update(packageBytes).digest('hex');
  await writeFile(
    path.join(outputDirectory, `${packageFileName}.sha256`),
    `${sha256}  ${packageFileName}\n`,
    'utf8'
  );

  const provenance = {
    projectKey: canonicalProjectKey,
    projectName: canonicalProjectName,
    sourceProductSha,
    c11DemoCommitSha,
    activeProjectKey: active.projectKey,
    activeRevision: active.revision,
    packageFileName,
    packageSha256: sha256,
    packageId: inspection.manifest.packageId,
    packageCreatedAtUtc: inspection.manifest.createdAtUtc,
    packageFormat: inspection.manifest.format,
    packageFormatVersion: inspection.manifest.formatVersion,
    engineeringSchema: inspection.manifest.engineeringSchema,
    engineeringSchemaVersion: inspection.manifest.engineeringSchemaVersion,
    applicationDynamoCount: applicationDynamos(active.package.dynamos).length,
    builtinLibraryDynamoCount: builtinDynamos(active.package.dynamos).length,
    constructionMethod: [
      'Fresh isolated Engineering persistence database',
      'First Project Setup',
      'Engineering JSON Import Preview',
      'Engineering JSON Import Apply',
      'Save',
      'Publish',
      'Activate',
      'Active Runtime verification',
      'Project Package Export',
      'Project Package Inspect',
      'Project Package Import Preview'
    ],
    validationBoundaries: [
      '/api/engineering/persistence/projects/first',
      '/api/engineering/import/json/preview',
      '/api/engineering/import/json/apply',
      `/api/engineering/persistence/${canonicalProjectKey}/save`,
      `/api/engineering/persistence/${canonicalProjectKey}/published/activate`,
      '/api/runtime/application',
      '/api/project-package/export',
      '/api/project-package/inspect',
      '/api/project-package/import/preview'
    ],
    workflow: process.env.GITHUB_WORKFLOW ?? null,
    workflowRunId: process.env.GITHUB_RUN_ID ?? null,
    workflowRunNumber: process.env.GITHUB_RUN_NUMBER ?? null
  };

  await writeFile(
    path.join(outputDirectory, 'EliteSCADA-EEE-Demo.provenance.json'),
    `${JSON.stringify(provenance, null, 2)}\n`,
    'utf8'
  );
});

async function createFreshProject(request: APIRequestContext) {
  const statusBefore = await request.get('/api/engineering/persistence/status');
  expect(
    statusBefore.ok(),
    `C11 persistence status failed: HTTP ${statusBefore.status()} ${await statusBefore.text()}`
  ).toBeTruthy();
  const before = await statusBefore.json() as { enabled?: boolean; hasProjects?: boolean | null };
  expect(before.enabled).toBe(true);
  expect(before.hasProjects).toBe(false);

  const create = await request.post('/api/engineering/persistence/projects/first', {
    data: {
      projectKey: canonicalProjectKey,
      projectName: canonicalProjectName
    }
  });
  expect(
    create.status(),
    `C11 First Project failed: HTTP ${create.status()} ${await create.text()}`
  ).toBe(201);
  const created = await create.json() as any;
  expect(created.revision.projectKey).toBe(canonicalProjectKey);
  expect(created.revision.projectName).toBe(canonicalProjectName);

  const statusAfter = await request.get('/api/engineering/persistence/status');
  expect(statusAfter.ok()).toBeTruthy();
  const after = await statusAfter.json() as { hasProjects?: boolean | null };
  expect(after.hasProjects).toBe(true);
}

async function previewAndApply(request: APIRequestContext, candidate: any) {
  const before = await loadWorkspace(request);
  const previewResponse = await request.post('/api/engineering/import/json/preview', { data: candidate });
  expect(
    previewResponse.ok(),
    `C11 package candidate preview failed: HTTP ${previewResponse.status()} ${await previewResponse.text()}`
  ).toBeTruthy();
  const preview = await previewResponse.json() as { canApply: boolean; errorCount: number; items?: any[] };
  expect(preview.canApply, `C11 package candidate issues: ${JSON.stringify(preview.items ?? [], null, 2)}`).toBe(true);
  expect(preview.errorCount).toBe(0);

  const afterPreview = await loadWorkspace(request);
  expect(afterPreview.changeVersion).toBe(before.changeVersion);
  const applyResponse = await request.post('/api/engineering/import/json/apply', {
    headers: { 'x-elitescada-workspace-version': String(afterPreview.changeVersion) },
    data: candidate
  });
  expect(
    applyResponse.ok(),
    `C11 package candidate Apply failed: HTTP ${applyResponse.status()} ${await applyResponse.text()}`
  ).toBeTruthy();
}

async function savePublishActivate(request: APIRequestContext): Promise<{ revision: number }> {
  const save = await request.post(`/api/engineering/persistence/${canonicalProjectKey}/save`, {
    data: { projectName: canonicalProjectName }
  });
  expect(save.ok(), `C11 package Save failed: HTTP ${save.status()} ${await save.text()}`).toBeTruthy();
  const saved = await save.json() as { revision: number };

  const publish = await request.post(
    `/api/engineering/persistence/${canonicalProjectKey}/revisions/${saved.revision}/publish`,
    { data: {} }
  );
  expect(publish.ok(), `C11 package Publish failed: HTTP ${publish.status()} ${await publish.text()}`).toBeTruthy();

  const activate = await request.post(
    `/api/engineering/persistence/${canonicalProjectKey}/published/activate`,
    { data: {} }
  );
  expect(activate.ok(), `C11 package Activate failed: HTTP ${activate.status()} ${await activate.text()}`).toBeTruthy();
  return saved;
}

async function loadWorking(request: APIRequestContext): Promise<any> {
  const response = await request.get('/api/engineering/export/json');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function loadWorkspace(request: APIRequestContext): Promise<{ changeVersion: number }> {
  const response = await request.get('/api/engineering/workspace');
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

function builtinDynamos(dynamos: any[] | undefined | null): any[] {
  return (dynamos ?? []).filter(dynamo => dynamo?.metadata?.builtinLibrary === 'true');
}

function applicationDynamos(dynamos: any[] | undefined | null): any[] {
  return (dynamos ?? []).filter(dynamo => dynamo?.metadata?.builtinLibrary !== 'true');
}

async function readCurrent(request: APIRequestContext, tagPath: string): Promise<{ value?: unknown; quality?: unknown }> {
  const response = await request.get(`/api/tags/by-path/${tagPath}`);
  expect(response.ok(), `TAG ${tagPath} read failed: HTTP ${response.status()} ${await response.text()}`).toBeTruthy();
  const body = await response.json() as { current?: { value?: unknown; quality?: unknown } | null };
  return body.current ?? {};
}
