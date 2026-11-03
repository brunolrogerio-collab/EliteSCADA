import { expect, test, type Page } from '@playwright/test';
import { readSingleTargetSignals } from '../src/engineering/intelligenceSingleTargetReferences';
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
async function installRuntime(page: Page, elementScopeId = scopeId, stage = false, single = false) {
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
    if (path === '/api/runtime/application') return route.fulfill({ json: runtimeProjection(elementScopeId, stage, single) });
    if (path === '/api/runtime/client-memory') return route.fulfill({ json: [] });
    if (path === (single ? '/api/engineering/asset-process-intelligence/analytics/contextual-reference-deviation/v1/query' : referencePath)) {
      const body = route.request().postDataJSON(); fixture.requests.push(body);
      if (fixture.mode === 'late') {
        start(); await delayed;
        try { await route.fulfill({ json: (single ? singleTargetResponse(body,fixture.mode) : response(body)) }); } catch { /* A changed window aborts the request. */ }
        fixture.completed = true; return;
      }
      if (fixture.mode === 'forbidden') return route.fulfill({ status: 403, json: { code: 'INTELLIGENCE_ENTITLEMENT_REQUIRED' } });
      return route.fulfill({ json: { ...(single ? singleTargetResponse(body,fixture.mode) : response(body)), ...(fixture.mode === 'stale' ? { activeRevision: revision + 1 } : {}) } });
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

function runtimeProjection(elementScopeId = scopeId, stage = false, single = false) {
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
          type: stage ? 'core.intelligenceProcessStageInsightsV1' : single ? 'core.intelligenceSingleTargetDeviationV1' : 'core.intelligenceCurrentFrequencyComparisonV1',
          properties: {
            x: 50, y: 60, width: 1100, height: 720,
            ...(stage ? {
              intelligenceStageId: '90000000-0000-0000-0000-000000000123', intelligenceStageKey: 'pumping', intelligenceStageName: 'Pumping',
              intelligenceStageMembers: JSON.stringify([
                {assignmentId,scopeId,profileId,displayLabel:'Motor with measured frequency',order:0,availabilityVersion:2,currentFrequency:signals,singleTarget:singleSignals},
                {assignmentId:profileId,scopeId:currentTagId,profileId:speedTagId,displayLabel:'Motor without measured frequency',order:1,availabilityVersion:2,referenceEvidence:true}
              ])
            } : {}),
            intelligenceScopeId: elementScopeId,
            intelligenceProfileId: profileId,
            intelligenceAssignmentId: assignmentId,
            intelligenceCurrentFrequencySignals: JSON.stringify(signals),
            intelligenceSingleTargetSignals: JSON.stringify(singleSignals)
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

const singleSignals=[{id:currentTagId,tagId:currentTagId,label:'Motor current',unit:'A',canonicalUnit:null,unitCode:null},
 {id:speedTagId,tagId:speedTagId,label:'Measured frequency',unit:'Hz',canonicalUnit:'Hz',unitCode:'unit.frequency.hertz'}];
function singleTargetResponse(body:any,mode:string){
 const counts={rawObservationCount:35,goodFiniteNumericCount:34,acceptedSynchronizedSampleCount:32,rejectedObservationCount:3,ambiguousSourceTimestampGroupCount:1,ambiguousSourceTimestampRowCount:2,qualityCounts:{Good:34,BadCommunication:1},rejectionCounts:{quality_not_good:1,duplicate_target_source_timestamp:2}};
 const evidence=(window:any)=>({window,target:counts,condition:{...counts,rejectionCounts:{quality_not_good:1,duplicate_condition_source_timestamp:2}},exactSourceTimestampPairCount:32,conditionInRangePairCount:30,conditionOutOfRangePairCount:2,qualifiedPairCount:30});
 const signal=(id:string,tagId:string,condition=false)=>({signalId:id,signalKey:'medição',tagId,tagPath:'Planta.Bomba01',dataType:'Double',engineeringUnit:condition?'Hz':'A',controlledUnitCode:condition?'unit.frequency.hertz':null,canonicalUnit:condition?'Hz':null});
 const insufficient=mode==='insufficient',zero=mode==='zero-mad';
 return {contractVersion:1,catalogVersion:1,ruleId:'contextual-reference-deviation.v1',ruleVersion:'1.0.0',projectKey,activeRevision:mode==='stale'?5:4,scopeKind:'Equipment',scopeId,equipmentId:scopeId,assignmentId,profileId,
 targetSignal:signal(body.targetSignalId,currentTagId),conditionSignal:signal(body.conditionSignalId,speedTagId,true),referenceWindow:body.referenceWindow,evaluationWindow:body.evaluationWindow,
 conditionLowerInclusiveCanonical:49,conditionUpperExclusiveCanonical:mode==='wrong-range'?52:51,conditionCanonicalUnit:mode==='wrong-unit'?'rpm':'Hz',referenceEvidence:{...evidence(body.referenceWindow),...(insufficient?{qualifiedPairCount:0,conditionInRangePairCount:0,conditionOutOfRangePairCount:32}:{})},evaluationEvidence:{...evidence(body.evaluationWindow),exactSourceTimestampPairCount:2,conditionInRangePairCount:1,conditionOutOfRangePairCount:1,qualifiedPairCount:1},referenceMedian:insufficient?null:10,referenceMedianAbsoluteDeviation:insufficient?null:zero?0:2,status:insufficient?'insufficient_data':'available',reasonCodes:insufficient?['no_qualified_reference_pairs']:zero?['reference_mad_zero']:[],totalRawObservationCount:140,returnedEvaluationSampleCount:2,omittedEvaluationSampleCount:0,
 evaluationSamples:[{sourceTimestampUtc:body.evaluationWindow.fromUtc,targetObservedTimestampUtc:body.evaluationWindow.fromUtc,conditionObservedTimestampUtc:body.evaluationWindow.fromUtc,targetValue:14,conditionValueCanonical:50,conditionInRange:true,rawDifferenceFromReferenceMedian:insufficient?null:4,robustScore:insufficient||zero?null:1.3489795,scoreUnavailableReason:insufficient?'no_qualified_reference_pairs':zero?'reference_mad_zero':null},
 {sourceTimestampUtc:body.evaluationWindow.toUtc,targetObservedTimestampUtc:body.evaluationWindow.toUtc,conditionObservedTimestampUtc:body.evaluationWindow.toUtc,targetValue:20,conditionValueCanonical:51,conditionInRange:false,rawDifferenceFromReferenceMedian:null,robustScore:null,scoreUnavailableReason:'condition_outside_requested_range'}],provenance:{dataset:'historian.samples.v2',datasetContractVersion:2,regimeEquivalence:'not_assessed',completeness:'unknown'}};
}

test('single-target Runtime queries explicitly, cancels, rejects stale access and disables historical playback',async({page})=>{
 page.on('pageerror',error=>{throw error;});const fixture=await installRuntime(page,scopeId,false,true);await page.goto('/');
 const visual=page.getByTestId('runtime-intelligence-single-target');await visual.locator('summary').first().click();
 await visual.getByRole('combobox',{name:'Measurement to analyze',exact:true}).selectOption(currentTagId);await visual.getByRole('combobox',{name:'Measured condition',exact:true}).selectOption(speedTagId);
 for(const [label,value] of [['Included lower bound (Hz)','49'],['Excluded upper bound (Hz)','51'],['Reference start (local time)','2026-10-20T10:00'],['Reference end (local time)','2026-10-20T11:00'],['Evaluation start (local time)','2026-10-21T10:00'],['Evaluation end (local time)','2026-10-21T12:00']])await visual.getByLabel(label,{exact:true}).fill(value);
 const query=visual.getByRole('button',{name:'Compare measurement',exact:true}),result=visual.getByTestId('intelligence-single-target-result');expect(fixture.requests).toEqual([]);await query.click();await expect(result).toContainText('10 A');
 fixture.mode='late';await query.click();await fixture.started();await visual.getByRole('button',{name:'Cancel measurement comparison',exact:true}).click();fixture.release();await expect.poll(()=>fixture.completed).toBe(true);await expect(result).toHaveCount(0);
 fixture.mode='stale';await query.click();await expect(visual.getByRole('alert')).toBeVisible();await expect(result).toHaveCount(0);
 fixture.mode='forbidden';await query.click();await expect(result).toHaveCount(0);await expect(visual.getByRole('alert')).toBeVisible();
 fixture.mode='valid';await query.click();await expect(result).toBeVisible();await result.scrollIntoViewIfNeeded();await visual.screenshot({path:test.info().outputPath('single-target-runtime.png')});
 await page.getByTestId('runtime-engineering-application').getByRole('button',{name:/Historical Playback/}).click();await page.getByTestId('runtime-playback-overlay').getByRole('button',{name:/Enter Playback/}).click();
 await expect(visual.getByRole('button',{name:'Compare measurement',exact:true,includeHidden:true})).toBeDisabled();await expect(result).toHaveCount(0);
});
for(const scenario of ['duplicate','extra','no-condition','partial-unit','too-many','empty-label'] as const)test(`single-target references reject ${scenario}`,()=>{
 const items=structuredClone(singleSignals);if(scenario==='duplicate')items[1].tagId=items[0].tagId;if(scenario==='extra')Object.assign(items[0],{policy:'forbidden'});
 if(scenario==='no-condition'){items[1].canonicalUnit=null as any;items[1].unitCode=null as any;}if(scenario==='partial-unit')items[1].unitCode=null as any;
 if(scenario==='too-many')for(let i=0;i<15;i++)items.push({...items[1],id:crypto.randomUUID(),tagId:crypto.randomUUID()});if(scenario==='empty-label')items[0].label=' ';
 expect(readSingleTargetSignals(JSON.stringify(items))).toBeNull();
});

test('single-target stage retains partial members and clears result when changing equipment',async({page})=>{
 const fixture=await installRuntime(page,scopeId,true,true);await page.goto('/');const stage=page.getByTestId('runtime-intelligence-process-stage-insights'),choice=stage.getByTestId('runtime-intelligence-process-stage-insights-single-target');
 await choice.click();const visual=stage.getByTestId('runtime-intelligence-single-target');await visual.locator('summary').first().click();
 await visual.getByRole('combobox',{name:'Measurement to analyze',exact:true}).selectOption(currentTagId);await visual.getByRole('combobox',{name:'Measured condition',exact:true}).selectOption(speedTagId);
 for(const [label,value] of [['Included lower bound (Hz)','49'],['Excluded upper bound (Hz)','51'],['Reference start (local time)','2026-10-20T10:00'],['Reference end (local time)','2026-10-20T11:00'],['Evaluation start (local time)','2026-10-21T10:00'],['Evaluation end (local time)','2026-10-21T12:00']])await visual.getByLabel(label,{exact:true}).fill(value);
 expect(fixture.requests).toEqual([]);await visual.getByRole('button',{name:'Compare measurement',exact:true}).click();await expect(visual.getByTestId('intelligence-single-target-result')).toContainText('10 A');
 await stage.getByTestId('runtime-intelligence-process-stage-insights-member').selectOption(profileId);await expect(visual).toHaveCount(0);await expect(choice).toBeDisabled();await expect(stage).toContainText('two distinct numeric measurements');await stage.screenshot({path:test.info().outputPath('single-target-stage-partial.png')});
 await stage.getByTestId('runtime-intelligence-process-stage-insights-reference').click();await expect(stage.getByTestId('runtime-intelligence-reference-evidence')).toBeVisible();
 await stage.getByTestId('runtime-intelligence-process-stage-insights-member').selectOption(assignmentId);await choice.click();await expect(stage.getByTestId('intelligence-single-target-result')).toHaveCount(0);expect(fixture.requests).toHaveLength(1);
});
