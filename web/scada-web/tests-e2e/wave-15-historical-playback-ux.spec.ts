import { expect, test, type Page, type Route } from '@playwright/test';

const ANALOG_ID = '11111111-1111-4111-8111-111111111111';
const DIGITAL_ID = '22222222-2222-4222-8222-222222222222';
const GAP_ID = '33333333-3333-4333-8333-333333333333';
const COMMAND_ID = '44444444-4444-4444-8444-444444444444';
const SCREEN_HOME_ID = '55555555-5555-4555-8555-555555555555';
const SCREEN_SECONDARY_ID = '66666666-6666-4666-8666-666666666666';
const POPUP_ID = '77777777-7777-4777-8777-777777777777';
const DYNAMO_ID = '88888888-8888-4888-8888-888888888888';
const DYNAMO_INSTANCE_ID = '99999999-9999-4999-8999-999999999999';

const valueDisplay = (id:string,key:string,tagId:string,path:string,dataType:string,x:number,y:number) => ({
  id,key,type:'core.valueDisplay',
  properties:{x,y,width:220,height:58,text:'—',visible:true},
  bindings:[{key:'text',kind:'tag',target:path,direction:'read',metadata:{sourceDataType:dataType},tagReference:{tagId}}]
});
const button = (id:string,key:string,text:string,x:number,y:number,action:Record<string,unknown>) => ({
  id,key,type:'core.button',properties:{x,y,width:220,height:52,text,visible:true},
  actions:[{eventKey:'click',version:1,...action}]
});

function projection(enabled:boolean){
 return {
  mode:'engineering',projectKey:'w15-playback',projectName:'W15 Playback',revision:445,
  activatedAtUtc:'2026-10-02T14:20:00Z',
  package:{
   schema:'scada.engineering',schemaVersion:20,exportedAt:'2026-10-02T14:20:00Z',
   startupScreenId:SCREEN_HOME_ID,runtimePresentation:{historicalPlaybackEnabled:enabled,version:1},
   tags:[
    {id:ANALOG_ID,name:'Analog',path:'Plant.Analog',dataType:'Double',readOnly:false},
    {id:DIGITAL_ID,name:'Digital',path:'Plant.Digital',dataType:'Boolean',readOnly:false},
    {id:GAP_ID,name:'Gap',path:'Plant.Gap',dataType:'Double',readOnly:false}
   ],alarms:[],templates:[],equipment:[],dynamos:[{
     id:DYNAMO_ID,key:'history.value',name:'Historical value',elements:[
       valueDisplay('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','analog-dynamo',ANALOG_ID,'Plant.Analog','Double',0,0)
     ]
   }],scripts:[],scriptVisualEventReferences:[],visualAssets:[],
   commands:[{id:COMMAND_ID,key:'cmd.test',name:'Test',kind:'WriteValue',value:'1',enabled:true}],
   screens:[
    {id:SCREEN_HOME_ID,key:'home',name:'Home',elements:[
      valueDisplay('10000000-0000-4000-8000-000000000001','analog-home',ANALOG_ID,'Plant.Analog','Double',40,40),
      valueDisplay('10000000-0000-4000-8000-000000000006','gap-home',GAP_ID,'Plant.Gap','Double',40,110),
      {id:DYNAMO_INSTANCE_ID,key:'history-dynamo',type:'dynamo',dynamoKey:'history.value',dynamoDefinitionId:DYNAMO_ID,
       properties:{x:600,y:40,width:260,height:100}},
      button('10000000-0000-4000-8000-000000000002','open-popup','Abrir popup',300,40,{kind:'OpenPopup',targetKey:'details'}),
      button('10000000-0000-4000-8000-000000000003','navigate-secondary','Ir secundária',300,110,{kind:'NavigateScreen',targetKey:'secondary'}),
      button('10000000-0000-4000-8000-000000000004','write-tag','Escrever TAG',300,180,{kind:'SetTagValue',targetKey:ANALOG_ID,parameters:{value:42}}),
      button('10000000-0000-4000-8000-000000000005','execute-command','Executar comando',300,250,{kind:'ExecuteCommand',targetKey:null,commandId:COMMAND_ID,parameters:null})
    ]},
    {id:SCREEN_SECONDARY_ID,key:'secondary',name:'Secondary',elements:[
      valueDisplay('20000000-0000-4000-8000-000000000001','analog-secondary',ANALOG_ID,'Plant.Analog','Double',40,40)
    ]}
   ],
   popups:[{id:POPUP_ID,key:'details',name:'Details',x:900,y:160,properties:{width:'340',height:'180'},elements:[
     valueDisplay('30000000-0000-4000-8000-000000000001','digital-popup',DIGITAL_ID,'Plant.Digital','Boolean',20,20)
   ]}]
  }
 };
}

async function installShell(page:Page, enabled:boolean){
 await page.routeWebSocket('**/ws/tags',()=>{});
 await page.route('**/api/auth/config',r=>r.fulfill({json:{authenticationEnabled:true,localLoginEnabled:true,initialAdministratorRequired:false,initialAdministratorSetupAvailable:false,initialAdministratorBlockedReason:null,passwordPolicy:{minimumLength:8,maximumLength:1024}}}));
 await page.route('**/api/auth/me',r=>r.fulfill({json:{subjectId:'w15-user',username:'w15',displayName:'W15',roles:['developer'],identityProvider:'local'}}));
 await page.route('**/api/auth/local-session',r=>r.fulfill({json:{authenticated:true,username:'w15'}}));
 await page.route('**/api/auth/effective-capabilities',r=>r.fulfill({json:{
   authorityPolicy:{schema:'elitescada.authority-policy',schemaVersion:1},authenticationEnabled:true,
   runtime:['View','TrendUse','SystemAdmin'],workspace:['EngineeringView','EngineeringModify','SystemAdmin']
 }}));
 await page.route('**/api/engineering/persistence/status',r=>r.fulfill({json:{enabled:true,hasProjects:true}}));
 await page.route('**/api/runtime/application',r=>r.fulfill({json:projection(enabled)}));
 await page.route('**/api/tags',r=>r.fulfill({json:[
   {id:ANALOG_ID,name:'Analog',path:'Plant.Analog',dataType:'Double',readOnly:false,current:{tagId:ANALOG_ID,value:999,timestamp:'2026-10-02T14:00:00Z',quality:'Good'}},
   {id:DIGITAL_ID,name:'Digital',path:'Plant.Digital',dataType:'Boolean',readOnly:false,current:{tagId:DIGITAL_ID,value:true,timestamp:'2026-10-02T14:00:00Z',quality:'Good'}},
   {id:GAP_ID,name:'Gap',path:'Plant.Gap',dataType:'Double',readOnly:false,current:{tagId:GAP_ID,value:777,timestamp:'2026-10-02T14:00:00Z',quality:'Good'}}
 ]}));
 await page.route('**/api/runtime/historical-playback/resolve',async r=>{
   const body=r.request().postDataJSON() as {screenKey:string;popupKeys:string[]};
   const tags=[
     {id:ANALOG_ID,path:'Plant.Analog',dataType:'Double',retrievalMode:'interpolated'},
     {id:GAP_ID,path:'Plant.Gap',dataType:'Double',retrievalMode:'interpolated'}
   ];
   if(body.popupKeys?.includes('details')) tags.push({id:DIGITAL_ID,path:'Plant.Digital',dataType:'Boolean',retrievalMode:'atOrBefore'} as any);
   await r.fulfill({json:{screenKey:body.screenKey,popupKeys:body.popupKeys??[],tags}});
 });
}

async function historical(route:Route){
 const body=route.request().postDataJSON() as any;
 const definition=body.definition; const target=String(definition.historianRetrieval.targetUtc);
 const mode=String(definition.historianRetrieval.mode);
 const ids=(definition.query.filters?.[0]?.values??[]).map((v:any)=>String(v.value));
 const rows:any[]=[];
 if(ids.includes(ANALOG_ID)) rows.push({data:{cells:{
   'tag.id':{kind:'guid',value:ANALOG_ID},'tag.path':{kind:'string',value:'Plant.Analog'},
   quality:{kind:'enum',value:'Good'},value:{kind:'double',value:'12.5'},timestamp:{kind:'dateTime',value:target}
 }},provenance:{kind:'interpolated',sourceTimestampsUtc:[target],reason:null}});
 if(ids.includes(DIGITAL_ID)) rows.push({data:{cells:{
   'tag.id':{kind:'guid',value:DIGITAL_ID},'tag.path':{kind:'string',value:'Plant.Digital'},
   quality:{kind:'enum',value:'Good'},value:{kind:'boolean',value:'false'},timestamp:{kind:'dateTime',value:target}
 }},provenance:{kind:'measured',sourceTimestampsUtc:[target],reason:null}});
 await route.fulfill({json:{version:1,queryId:definition.id,queryKey:definition.key,datasetKey:'historian.samples',retrievalMode:mode,columns:[],rows,fromUtc:definition.query.timeRange.fromUtc,toUtc:definition.query.timeRange.toUtc,nextCursor:null,resultLimit:Math.max(1,ids.length)}});
}

test('Playback hidden when project setting is disabled',async({page})=>{
 await installShell(page,false); await page.goto('/');
 await expect(page.getByTestId('runtime-engineering-application')).toBeVisible();
 await expect(page.getByRole('button',{name:/Playback histórico|Historical Playback/i})).toHaveCount(0);
});

test('compact Playback overlay stays Live until explicit entry and preserves instant across visual navigation',async({page})=>{
 test.setTimeout(45_000); await installShell(page,true);
 let writes=0, commands=0;
 await page.route('**/api/historical/data-query',r=>historical(r));
 await page.route('**/api/tags/*/write',r=>{writes++;return r.fulfill({status:204});});
 await page.route('**/api/commands/*/execute',r=>{commands++;return r.fulfill({status:204});});
 await page.goto('/');
 const runtime=page.getByTestId('runtime-engineering-application');
 await expect(runtime).toHaveAttribute('data-runtime-temporal-mode','live');
 await runtime.getByRole('button',{name:/Tela cheia|Fullscreen|Pantalla completa/i}).click();
 await expect(runtime).toHaveAttribute('data-runtime-fullscreen','true');
 const playbackTool=page.getByRole('button',{name:/Playback histórico|Historical Playback/i});
 await expect(playbackTool).toBeVisible();
 await playbackTool.click();
 const panel=page.getByTestId('runtime-playback-overlay');
 await expect(panel).toBeVisible();
 await expect(runtime).toHaveAttribute('data-runtime-fullscreen','true');
 await expect(runtime).toHaveAttribute('data-runtime-temporal-mode','live');
 await expect(panel.locator('.runtime-playback-overlay-content')).toHaveAttribute('data-playback-position','1');

 await panel.getByRole('button',{name:/Entrar no Playback|Enter Playback|Entrar en Playback/i}).click();
 await expect(runtime).toHaveAttribute('data-runtime-temporal-mode','historical-playback');
 await expect(panel.locator('.runtime-playback-overlay-content')).toHaveAttribute('data-playback-load-state','ready');
 await expect(runtime.getByRole('button',{name:/Alarmes|Alarms|Alarmas/i})).toBeDisabled();
 const firstAt=await runtime.getAttribute('data-runtime-historical-at'); expect(firstAt).toBeTruthy();
 await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000001"]')).toContainText(/12[,.]5/);
 await expect(page.locator(`[data-object-id="${DYNAMO_INSTANCE_ID}"]`)).toContainText(/12[,.]5/);
 await expect(page.locator('[data-object-id="10000000-0000-4000-8000-000000000006"]')).toContainText('—');
 await expect(panel.locator('.runtime-playback-overlay-content')).toHaveAttribute('data-playback-gap-count',/[1-9]\d*/);

 const instant=panel.getByRole('slider',{name:/Instante|Instant/i});
 await instant.fill('750');
 await expect.poll(async()=>runtime.getAttribute('data-runtime-historical-at')).not.toBe(firstAt);
 const changedAt=await runtime.getAttribute('data-runtime-historical-at'); expect(changedAt).toBeTruthy();

 await panel.getByRole('button',{name:/Fechar|Close|Cerrar/i}).click();
 await expect(panel).toHaveCount(0);
 await expect(runtime).toHaveAttribute('data-runtime-temporal-mode','historical-playback');
 await playbackTool.click();
 await expect(page.getByTestId('runtime-playback-overlay')).toBeVisible();

 await page.getByRole('button',{name:'Abrir popup'}).click();
 await expect(page.locator('[data-popup-key="details"]')).toContainText(/Falso|False/);
 await expect(runtime).toHaveAttribute('data-runtime-historical-at',changedAt!);

 await page.getByRole('button',{name:'Escrever TAG'}).click();
 await expect(page.getByTestId('runtime-visual-diagnostic')).toHaveAttribute('data-diagnostic-code','HISTORICAL_PLAYBACK_READ_ONLY');
 await page.getByRole('button',{name:'Executar comando'}).click();
 await expect(page.getByTestId('runtime-visual-diagnostic')).toHaveAttribute('data-diagnostic-code','HISTORICAL_PLAYBACK_READ_ONLY');
 expect(writes).toBe(0); expect(commands).toBe(0);

 await page.getByRole('button',{name:'Ir secundária'}).click();
 await expect(page.getByTestId('runtime-visual-navigator')).toHaveAttribute('data-active-screen-key','secondary');
 await expect(runtime).toHaveAttribute('data-runtime-historical-at',changedAt!);
 await page.getByRole('button',{name:/Voltar ao Live|Return to Live|Volver a Live/i}).first().click();
 await expect(runtime).toHaveAttribute('data-runtime-temporal-mode','live');
 await expect(runtime.getByRole('button',{name:/Alarmes|Alarms|Alarmas/i})).toBeEnabled();
});

test('Playback mutation guard rejects before Runtime lease/fetch',async()=>{
 const guard=await import('../src/runtime/historical-playback/runtimeHistoricalPlaybackGuard');
 const {writeRuntimeTagValue}=await import('../src/runtime/runtimeTagWriteApi');
 const {executeRuntimeCommand}=await import('../src/runtime/visual-navigation/runtimeCommandApi');
 let calls=0; const fetcher=async()=>{calls++;return new Response(null,{status:204});};
 guard.setRuntimeHistoricalPlaybackActive(true);
 try{
  await expect(writeRuntimeTagValue(ANALOG_ID,12,fetcher)).rejects.toThrow(/Historical Playback/);
  await expect(executeRuntimeCommand(COMMAND_ID,fetcher)).rejects.toThrow(/Historical Playback/);
  expect(calls).toBe(0);
 }finally{guard.setRuntimeHistoricalPlaybackActive(false);}
});
