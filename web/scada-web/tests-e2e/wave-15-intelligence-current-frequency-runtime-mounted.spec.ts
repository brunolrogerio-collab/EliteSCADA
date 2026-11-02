import { expect, test, type Page } from '@playwright/test';
import { readCurrentFrequencySignals } from '../src/engineering/intelligenceCurrentFrequencySignals';
const scopeId = '10000000-0000-0000-0000-000000000001';
const profileId = '30000000-0000-0000-0000-000000000003';
const assignmentId = '30000000-0000-0000-0000-000000000004';
const currentTagId = '30000000-0000-0000-0000-000000000101';
const speedTagId = '30000000-0000-0000-0000-000000000102';
const projectKey = 'intelligence-current-frequency-runtime-test';
const revision = 4;
const referencePath = '/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/query';
const signals = [
 {id:currentTagId,tagId:currentTagId,label:'Motor current',kind:'current',basis:'line_rms'},
 {id:speedTagId,tagId:speedTagId,label:'Drive measured frequency',kind:'frequency',basis:'drive_electrical'}
];
async function fillQuery(visual: ReturnType<Page['getByTestId']>) {
 await visual.getByRole('combobox',{name:'Current measurement',exact:true}).selectOption(currentTagId);
 await visual.getByRole('combobox',{name:'Frequency measurement',exact:true}).selectOption(speedTagId);
 for(const [label,value] of [['Minimum frequency (Hz)','49'],['Maximum frequency (Hz)','51'],['Primary period start (local time)','2026-10-22T10:00'],['Primary period end (local time)','2026-10-22T11:00'],['Comparison period start (local time)','2026-10-20T10:00'],['Comparison period end (local time)','2026-10-20T11:00']])await visual.getByLabel(label,{exact:true}).fill(value);
}
test('current-frequency Runtime queries explicitly, clears changed and cancelled results, rejects stale access and disables playback',async({page})=>{
 page.on('pageerror',error=>{throw error;});
 const fixture=await installRuntime(page);await page.goto('/');
 const runtime=page.getByTestId('runtime-engineering-application'),visual=page.getByTestId('runtime-intelligence-current-frequency');
 const result=visual.getByTestId('intelligence-current-frequency-result'),query=visual.getByRole('button',{name:'Compare current',exact:true});
 await fillQuery(visual);expect(fixture.requests).toHaveLength(0);await query.click();await expect(result).toContainText('20 %');
 await visual.getByLabel('Maximum frequency (Hz)',{exact:true}).fill('52');await expect(result).toHaveCount(0);
 fixture.mode='late';await query.click();await fixture.started();await visual.getByRole('button',{name:'Cancel query'}).click();fixture.release();
 await expect.poll(()=>fixture.completed).toBe(true);await expect(result).toHaveCount(0);
 fixture.mode='stale';await query.click();await expect(visual.getByRole('alert')).toBeVisible();await expect(result).toHaveCount(0);
 fixture.mode='forbidden';await query.click();await expect(visual.getByRole('alert')).toContainText('permission');await expect(result).toHaveCount(0);
 fixture.mode='valid';await query.click();await expect(result).toBeVisible();
 await runtime.getByRole('button',{name:/Historical Playback/}).click();const playback=page.getByTestId('runtime-playback-overlay');await playback.getByRole('button',{name:/Enter Playback/}).click();
 await expect(query).toBeDisabled();await expect(result).toHaveCount(0);const count=fixture.requests.length;
 await playback.getByRole('button',{name:/Return to Live/}).click();await fillQuery(visual);await expect(query).toBeEnabled();expect(fixture.requests).toHaveLength(count);await expect(result).toHaveCount(0);
});
test('invalid current-frequency scope fails closed without reads',async({page})=>{
 const fixture=await installRuntime(page,'invalid');await page.goto('/');const visual=page.getByTestId('runtime-intelligence-current-frequency');
 await expect(visual.getByRole('alert')).toBeVisible();expect(fixture.requests).toEqual([]);
});
for(const scenario of ['one','many','duplicate-id','duplicate-tag','empty-id','empty-tag','empty-label','long-label','extra','kind','basis','no-current','zero-guid'] as const)test(`current-frequency presentation rejects ${scenario}`,()=>{
 const items=structuredClone(signals);
 if(scenario==='one')items.pop();if(scenario==='many')for(let i=0;i<15;i++)items.push({...items[1],id:crypto.randomUUID(),tagId:crypto.randomUUID()});
 if(scenario==='duplicate-id')items[1].id=items[0].id;if(scenario==='duplicate-tag')items[1].tagId=items[0].tagId;
 if(scenario==='empty-id')items[0].id='';if(scenario==='empty-tag')items[0].tagId='';if(scenario==='empty-label')items[0].label=' ';
 if(scenario==='long-label')items[0].label='x'.repeat(321);if(scenario==='extra')Object.assign(items[0],{minimum:0});
 if(scenario==='kind')items[0].kind='command';if(scenario==='basis')items[0].basis='rpm';if(scenario==='no-current'){items[0].kind='frequency';items[0].basis='shaft_rotational';}
 if(scenario==='zero-guid')items[0].id='00000000-0000-0000-0000-000000000000';
 expect(readCurrentFrequencySignals(JSON.stringify(items))).toBeNull();
});
async function installRuntime(page: Page, elementScopeId = scopeId, stage = false) {
  let start!: () => void; const started = new Promise<void>(resolve => { start = resolve; });
  let release!: () => void; const delayed = new Promise<void>(resolve => { release = resolve; });
  const fixture = { requests: [] as any[], mode: 'valid', completed: false, started: () => started, release: () => release() };
  await page.addInitScript(() => localStorage.setItem('elitescada.engineering.locale', 'en'));
  await page.routeWebSocket('**/ws/tags', () => {});
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/auth/config') return route.fulfill({ json: { authenticationEnabled: false, localLoginEnabled: false, initialAdministratorRequired: false } });
    if (path === '/api/auth/effective-capabilities') return route.fulfill({ json: {
      authorityPolicy: { schema: 'elitescada.authority-policy', schemaVersion: 1 },
      authenticationEnabled: false, runtime: ['View', 'TrendUse'], workspace: []
    } });
    if (path === '/api/engineering/persistence/status') return route.fulfill({ json: { enabled: true, hasProjects: true } });
    if (path === '/api/product/info') return route.fulfill({ json: { productName: 'SCADIVO', channel: 'Test', version: '0.0.0', displayVersion: 'SCADIVO Test', informationalVersion: 'SCADIVO Test', buildCommit: 'reference-test' } });
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection(elementScopeId, stage) });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === referencePath) {
      const body = route.request().postDataJSON(); fixture.requests.push(body);
      if (fixture.mode === 'late') {
        start(); await delayed;
        try { await route.fulfill({ json: response(body) }); } catch { /* A changed window aborts the request. */ }
        fixture.completed = true; return;
      }
      if (fixture.mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_ENTITLEMENT_REQUIRED' } });
      return route.fulfill({ json: { ...response(body), ...(fixture.mode === 'stale' ? { activeRevision: revision + 1 } : {}) } });
    }
    return route.fulfill({ status: 404, json: {} });
  });
  return fixture;
}
function response(body: Record<string, any>) {
  const evidence = (window: any) => ({ window, current: { rawObservationCount: 5, acceptedSynchronizedSampleCount: 4, rejectedObservationCount: 1, rejectionCounts: { quality_not_good: 1 } },
    frequency: { rawObservationCount: 4, acceptedSynchronizedSampleCount: 4, rejectedObservationCount: 0, rejectionCounts: {} },
    exactSourceTimestampPairCount: 4, frequencyInRangePairCount: 3, frequencyOutOfRangePairCount: 1, selectedPairCount: 3,
    reusedSourceTimestampPairCount: 0, unmatchedCurrentAcceptedSampleCount: 0, unmatchedFrequencyAcceptedSampleCount: 0 });
  return { contractVersion: 1, catalogVersion: 1, ruleId: 'current-at-recorded-frequency.compare.v1', ruleVersion: '1.0.0',
    projectKey, activeRevision: revision, scopeKind: 'Equipment', scopeId, equipmentId: scopeId, assignmentId, profileId,
    currentSignal: { signalId: body.currentSignalId, tagId: currentTagId, roleCode: 'pump.electrical_current', currentBasis: 'line_rms', signalKind: null, frequencyReference: null, controlledUnitCode: 'unit.electric_current.ampere', canonicalUnit: 'A' },
    frequencySignal: { signalId: body.frequencySignalId, tagId: speedTagId, roleCode: 'pump.drive_frequency', currentBasis: null, signalKind: 'measured_output', frequencyReference: 'drive_electrical', controlledUnitCode: 'unit.frequency.hertz', canonicalUnit: 'Hz' },
    minimumFrequencyHz: body.minimumFrequencyHz, maximumFrequencyHz: body.maximumFrequencyHz, primary: evidence(body.primaryWindow), comparison: evidence(body.comparisonWindow), totalRawObservationCount: 18,
    metric: { status: 'available', reasonCodes: [] as string[], primaryMeanCurrentA: 6 as number | null, comparisonMeanCurrentA: 5 as number | null, differenceA: 1 as number | null, percentChange: 20 as number | null, percentChangeStatus: 'available', percentChangeReasonCode: null as string | null },
    provenance: { dataset: 'historian.samples.v2', datasetContractVersion: 2, physicalSimultaneity: 'unverified; exact recorded-time equality only', operatingRegimeEquivalence: 'unverified; frequency alone is insufficient', expectedCadence: 'unknown', completeness: 'unknown' } };
}

function runtimeProjection(elementScopeId = scopeId, stage = false) {
  return {
    mode: 'engineering',
    projectKey,
    projectName: 'Intelligence Profile Coverage Runtime Test',
    revision,
    activatedAtUtc: '2026-10-21T00:00:00Z',
    package: {
      schema: 'scada.engineering',
      schemaVersion: 20,
      exportedAt: '2026-10-21T00:00:00Z',
      startupScreenId: '90000000-0000-0000-0000-000000000009',
      runtimePresentation: { historicalPlaybackEnabled: true },
      tags: [], alarms: [], templates: [], equipment: [], dynamos: [], scripts: [],
      scriptVisualEventReferences: [], visualAssets: [], popups: [],
      screens: [{
        id: '90000000-0000-0000-0000-000000000009',
        key: 'intelligence-profile-coverage',
        name: 'Historical profile coverage',
        elements: [{
          id: '90000000-0000-0000-0000-000000000010',
          key: 'profile-coverage-v1',
          type: stage ? 'core.intelligenceProcessStageInsightsV1' : 'core.intelligenceCurrentFrequencyComparisonV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            ...(stage ? {
              intelligenceStageId: '90000000-0000-0000-0000-000000000123', intelligenceStageKey: 'pumping', intelligenceStageName: 'Pumping',
              intelligenceStageMembers: JSON.stringify([
                {assignmentId,scopeId,profileId,displayLabel:'Motor with measured frequency',order:0,availabilityVersion:2,currentFrequency:signals},
                {assignmentId:profileId,scopeId:currentTagId,profileId:speedTagId,displayLabel:'Motor without measured frequency',order:1,availabilityVersion:2,referenceEvidence:true}
              ])
            } : {}),
            intelligenceScopeId: elementScopeId,
            intelligenceProfileId: profileId,
            intelligenceAssignmentId: assignmentId,
            intelligenceCurrentFrequencySignals: JSON.stringify(signals)
          }
        }]
      }]
    }
  };
}

test('stage current-frequency uses the eligible member and removes stale results when switching to a member without frequency',async({page})=>{
 const fixture=await installRuntime(page,scopeId,true);await page.goto('/');const stage=page.getByTestId('runtime-intelligence-process-stage-insights');
 const queryChoice=stage.getByTestId('runtime-intelligence-process-stage-insights-current-frequency');await expect(queryChoice).toBeEnabled();await queryChoice.click();
 const visual=stage.getByTestId('runtime-intelligence-current-frequency');await fillQuery(visual);await visual.getByRole('button',{name:'Compare current',exact:true}).click();
 await expect(visual.getByTestId('intelligence-current-frequency-result')).toContainText('20 %');
 const member=stage.getByTestId('runtime-intelligence-process-stage-insights-member');await member.selectOption(profileId);
 await expect(visual).toHaveCount(0);await expect(queryChoice).toBeDisabled();await expect(stage.getByText(/Current\/frequency comparison is not included/)).toBeVisible();
 await expect(stage.getByTestId('runtime-intelligence-process-stage-insights-reference')).toBeEnabled();expect(fixture.requests).toHaveLength(1);
 await member.selectOption(assignmentId);await queryChoice.click();await fillQuery(visual);fixture.mode='late';await visual.getByRole('button',{name:'Compare current',exact:true}).click();await fixture.started();
 await member.selectOption(profileId);fixture.release();await expect.poll(()=>fixture.completed).toBe(true);await expect(visual).toHaveCount(0);
 await member.selectOption(assignmentId);await queryChoice.click();await expect(visual.getByTestId('intelligence-current-frequency-result')).toHaveCount(0);expect(fixture.requests).toHaveLength(2);
});
