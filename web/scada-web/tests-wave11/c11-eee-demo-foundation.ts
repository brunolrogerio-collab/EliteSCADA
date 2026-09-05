export const EEE_PROJECT_KEY = 'eee-demo';
export const EEE_PROJECT_NAME = 'EliteSCADA — EEE Demo';

const uid = (group: number, value: number) =>
  `c11${group}0000-0000-4000-8000-${String(value).padStart(12, '0')}`;

export const EEE_IDS = {
  source: uid(0, 1),
  script: uid(0, 2),
  tags: {
    levelPct: uid(1, 1), inflowM3h: uid(1, 2), totalFlowM3h: uid(1, 3), dischargePressureBar: uid(1, 4),
    autoMode: uid(1, 5), highDemand: uid(1, 6), dutyPump: uid(1, 7), cycleCount: uid(1, 8), badQualityScenario: uid(1, 9),
    p01Running: uid(1, 101), p01Available: uid(1, 102), p01Fault: uid(1, 103), p01Trip: uid(1, 104),
    p01FrequencyHz: uid(1, 105), p01CurrentA: uid(1, 106), p01FlowM3h: uid(1, 107), p01PressureBar: uid(1, 108),
    p02Running: uid(1, 201), p02Available: uid(1, 202), p02Fault: uid(1, 203), p02Trip: uid(1, 204),
    p02FrequencyHz: uid(1, 205), p02CurrentA: uid(1, 206), p02FlowM3h: uid(1, 207), p02PressureBar: uid(1, 208),
    cmdAutoEnable: uid(1, 301), cmdAutoDisable: uid(1, 302), cmdP01Start: uid(1, 303), cmdP01Stop: uid(1, 304),
    cmdP02Start: uid(1, 305), cmdP02Stop: uid(1, 306), cmdResetFaults: uid(1, 307), cmdInjectP01Fault: uid(1, 308),
    cmdInjectP02Fault: uid(1, 309), cmdHighDemandEnable: uid(1, 310), cmdHighDemandDisable: uid(1, 311),
    cmdBadQualityEnable: uid(1, 312), cmdBadQualityDisable: uid(1, 313)
  },
  alarms: {
    levelHigh: uid(3, 1), levelHighHigh: uid(3, 2), levelLow: uid(3, 3),
    p01Fault: uid(3, 101), p01Trip: uid(3, 102), p02Fault: uid(3, 201), p02Trip: uid(3, 202), p01Communication: uid(3, 301)
  },
  events: {
    pumpStarted: uid(4, 1), pumpStopped: uid(4, 2), faultInjected: uid(4, 3), faultReset: uid(4, 4),
    dutyChanged: uid(4, 5), modeChanged: uid(4, 6), highDemandChanged: uid(4, 7), qualityScenarioChanged: uid(4, 8)
  },
  commands: {
    autoEnable: uid(5, 1), autoDisable: uid(5, 2), p01Start: uid(5, 3), p01Stop: uid(5, 4), p02Start: uid(5, 5), p02Stop: uid(5, 6),
    resetFaults: uid(5, 7), injectP01Fault: uid(5, 8), injectP02Fault: uid(5, 9), highDemandEnable: uid(5, 10),
    highDemandDisable: uid(5, 11), badQualityEnable: uid(5, 12), badQualityDisable: uid(5, 13)
  }
} as const;

export const EEE_PATHS = {
  levelPct: 'EEE.Process.LevelPct', inflowM3h: 'EEE.Process.InflowM3h', totalFlowM3h: 'EEE.Process.TotalFlowM3h',
  dischargePressureBar: 'EEE.Process.DischargePressureBar', autoMode: 'EEE.Process.AutoMode', highDemand: 'EEE.Process.HighDemand',
  dutyPump: 'EEE.Process.DutyPump', cycleCount: 'EEE.Process.CycleCount', badQualityScenario: 'EEE.Process.BadQualityScenario',
  p01Running: 'EEE.P01.Running', p01Available: 'EEE.P01.Available', p01Fault: 'EEE.P01.Fault', p01Trip: 'EEE.P01.Trip',
  p01FrequencyHz: 'EEE.P01.FrequencyHz', p01CurrentA: 'EEE.P01.CurrentA', p01FlowM3h: 'EEE.P01.FlowM3h', p01PressureBar: 'EEE.P01.PressureBar',
  p02Running: 'EEE.P02.Running', p02Available: 'EEE.P02.Available', p02Fault: 'EEE.P02.Fault', p02Trip: 'EEE.P02.Trip',
  p02FrequencyHz: 'EEE.P02.FrequencyHz', p02CurrentA: 'EEE.P02.CurrentA', p02FlowM3h: 'EEE.P02.FlowM3h', p02PressureBar: 'EEE.P02.PressureBar',
  cmdAutoEnable: 'EEE.Command.AutoEnable', cmdAutoDisable: 'EEE.Command.AutoDisable', cmdP01Start: 'EEE.Command.P01Start', cmdP01Stop: 'EEE.Command.P01Stop',
  cmdP02Start: 'EEE.Command.P02Start', cmdP02Stop: 'EEE.Command.P02Stop', cmdResetFaults: 'EEE.Command.ResetFaults',
  cmdInjectP01Fault: 'EEE.Command.InjectP01Fault', cmdInjectP02Fault: 'EEE.Command.InjectP02Fault',
  cmdHighDemandEnable: 'EEE.Command.HighDemandEnable', cmdHighDemandDisable: 'EEE.Command.HighDemandDisable',
  cmdBadQualityEnable: 'EEE.Command.BadQualityEnable', cmdBadQualityDisable: 'EEE.Command.BadQualityDisable'
} as const;

const source = {
  id: EEE_IDS.source,
  key: 'eee.sim.server-memory',
  name: 'EEE Simulation — Server Memory',
  driver: 'builtin.memory.server',
  enabled: true,
  settings: {},
  secretReferences: {},
  metadata: { application: 'eee-demo', role: 'process-truth' }
};

function memoryTag(id: string, name: string, path: string, dataType: 'boolean' | 'int32' | 'double', initialValue: boolean | number,
  options: { unit?: string; historian?: boolean; min?: number; max?: number } = {}) {
  return {
    id, name, path, dataType,
    source: source.key,
    dataSourceId: source.id,
    address: null,
    engineeringUnit: options.unit ?? null,
    description: `Canonical EEE simulation state: ${name}`,
    readOnly: false,
    scaleMinimum: options.min ?? null,
    scaleMaximum: options.max ?? null,
    historian: options.historian
      ? { enabled: true, strategy: 'onChange', deadband: 0.01, periodMilliseconds: null, maximumPeriodMilliseconds: 5000 }
      : { enabled: false, strategy: 'none', deadband: null, periodMilliseconds: null, maximumPeriodMilliseconds: null },
    metadata: { application: 'eee-demo' },
    accessPolicy: null,
    initialValue: { dataType, value: initialValue },
    addressSelector: null,
    communicationBinding: null
  };
}

function command(id: string, key: string, name: string, tagId: string, tagPath: string, equipmentPath: string | null = null) {
  return {
    id, key, name,
    kind: 'writeTagValue',
    value: 'true',
    targetTagId: tagId,
    targetTagPath: tagPath,
    description: `${name} through canonical Command -> Server Memory request TAG.`,
    area: 'EEE',
    equipmentPath,
    enabled: true,
    metadata: { application: 'eee-demo', request: 'one-shot' }
  };
}

function event(id: string, key: string, name: string) {
  return {
    id, key, name,
    type: 'state-change',
    category: 'operation',
    source: 'server-script',
    area: 'EEE',
    equipmentPath: null,
    tagId: null,
    tagPath: null,
    message: name,
    enabled: true,
    metadata: { application: 'eee-demo' }
  };
}

function alarm(id: string, name: string, tagId: string, tagPath: string, type: string, priority: string,
  options: { setpoint?: number; message: string; alarmClass: string }) {
  return {
    id, name, tagId, tagPath, type, priority,
    setpoint: options.setpoint ?? null,
    digitalActiveValue: true,
    alarmClass: options.alarmClass,
    area: 'EEE',
    message: options.message,
    activationDelayMilliseconds: 0,
    requiresAcknowledgement: true,
    shelvingAllowed: true,
    enabled: true,
    metadata: { application: 'eee-demo' }
  };
}

export function buildEeeFoundationPackage(base: any): any {
  const t = EEE_IDS.tags;
  const p = EEE_PATHS;
  const tags = [
    memoryTag(t.levelPct, 'Nível do Poço', p.levelPct, 'double', 45, { unit: '%', historian: true, min: 0, max: 100 }),
    memoryTag(t.inflowM3h, 'Vazão Afluente', p.inflowM3h, 'double', 20, { unit: 'm³/h', historian: true, min: 0, max: 100 }),
    memoryTag(t.totalFlowM3h, 'Vazão Recalcada Total', p.totalFlowM3h, 'double', 0, { unit: 'm³/h', historian: true, min: 0, max: 100 }),
    memoryTag(t.dischargePressureBar, 'Pressão de Recalque', p.dischargePressureBar, 'double', 0, { unit: 'bar', historian: true, min: 0, max: 6 }),
    memoryTag(t.autoMode, 'Modo Automático', p.autoMode, 'boolean', true),
    memoryTag(t.highDemand, 'Cenário Alta Demanda', p.highDemand, 'boolean', false),
    memoryTag(t.dutyPump, 'Bomba de Vez', p.dutyPump, 'int32', 1),
    memoryTag(t.cycleCount, 'Contador de Ciclos', p.cycleCount, 'int32', 0),
    memoryTag(t.badQualityScenario, 'Cenário Qualidade Ruim', p.badQualityScenario, 'boolean', false),
    memoryTag(t.p01Running, 'P01 Em Operação', p.p01Running, 'boolean', false), memoryTag(t.p01Available, 'P01 Disponível', p.p01Available, 'boolean', true),
    memoryTag(t.p01Fault, 'P01 Falha', p.p01Fault, 'boolean', false), memoryTag(t.p01Trip, 'P01 Trip', p.p01Trip, 'boolean', false),
    memoryTag(t.p01FrequencyHz, 'P01 Frequência', p.p01FrequencyHz, 'double', 0, { unit: 'Hz', historian: true, min: 0, max: 60 }),
    memoryTag(t.p01CurrentA, 'P01 Corrente', p.p01CurrentA, 'double', 0, { unit: 'A', historian: true, min: 0, max: 40 }),
    memoryTag(t.p01FlowM3h, 'P01 Vazão', p.p01FlowM3h, 'double', 0, { unit: 'm³/h', historian: true, min: 0, max: 50 }),
    memoryTag(t.p01PressureBar, 'P01 Pressão', p.p01PressureBar, 'double', 0, { unit: 'bar', historian: true, min: 0, max: 6 }),
    memoryTag(t.p02Running, 'P02 Em Operação', p.p02Running, 'boolean', false), memoryTag(t.p02Available, 'P02 Disponível', p.p02Available, 'boolean', true),
    memoryTag(t.p02Fault, 'P02 Falha', p.p02Fault, 'boolean', false), memoryTag(t.p02Trip, 'P02 Trip', p.p02Trip, 'boolean', false),
    memoryTag(t.p02FrequencyHz, 'P02 Frequência', p.p02FrequencyHz, 'double', 0, { unit: 'Hz', historian: true, min: 0, max: 60 }),
    memoryTag(t.p02CurrentA, 'P02 Corrente', p.p02CurrentA, 'double', 0, { unit: 'A', historian: true, min: 0, max: 40 }),
    memoryTag(t.p02FlowM3h, 'P02 Vazão', p.p02FlowM3h, 'double', 0, { unit: 'm³/h', historian: true, min: 0, max: 50 }),
    memoryTag(t.p02PressureBar, 'P02 Pressão', p.p02PressureBar, 'double', 0, { unit: 'bar', historian: true, min: 0, max: 6 }),
    memoryTag(t.cmdAutoEnable, 'Comando Habilitar Automático', p.cmdAutoEnable, 'boolean', false), memoryTag(t.cmdAutoDisable, 'Comando Desabilitar Automático', p.cmdAutoDisable, 'boolean', false),
    memoryTag(t.cmdP01Start, 'Comando Partir P01', p.cmdP01Start, 'boolean', false), memoryTag(t.cmdP01Stop, 'Comando Parar P01', p.cmdP01Stop, 'boolean', false),
    memoryTag(t.cmdP02Start, 'Comando Partir P02', p.cmdP02Start, 'boolean', false), memoryTag(t.cmdP02Stop, 'Comando Parar P02', p.cmdP02Stop, 'boolean', false),
    memoryTag(t.cmdResetFaults, 'Comando Reset Falhas', p.cmdResetFaults, 'boolean', false), memoryTag(t.cmdInjectP01Fault, 'Comando Injetar Falha P01', p.cmdInjectP01Fault, 'boolean', false),
    memoryTag(t.cmdInjectP02Fault, 'Comando Injetar Falha P02', p.cmdInjectP02Fault, 'boolean', false),
    memoryTag(t.cmdHighDemandEnable, 'Comando Habilitar Alta Demanda', p.cmdHighDemandEnable, 'boolean', false),
    memoryTag(t.cmdHighDemandDisable, 'Comando Desabilitar Alta Demanda', p.cmdHighDemandDisable, 'boolean', false),
    memoryTag(t.cmdBadQualityEnable, 'Comando Habilitar Qualidade Ruim', p.cmdBadQualityEnable, 'boolean', false),
    memoryTag(t.cmdBadQualityDisable, 'Comando Desabilitar Qualidade Ruim', p.cmdBadQualityDisable, 'boolean', false)
  ];

  const commands = [
    command(EEE_IDS.commands.autoEnable, 'eee.auto.enable', 'Habilitar Automático', t.cmdAutoEnable, p.cmdAutoEnable),
    command(EEE_IDS.commands.autoDisable, 'eee.auto.disable', 'Desabilitar Automático', t.cmdAutoDisable, p.cmdAutoDisable),
    command(EEE_IDS.commands.p01Start, 'eee.p01.start', 'Partir P01', t.cmdP01Start, p.cmdP01Start, 'EEE.P01'),
    command(EEE_IDS.commands.p01Stop, 'eee.p01.stop', 'Parar P01', t.cmdP01Stop, p.cmdP01Stop, 'EEE.P01'),
    command(EEE_IDS.commands.p02Start, 'eee.p02.start', 'Partir P02', t.cmdP02Start, p.cmdP02Start, 'EEE.P02'),
    command(EEE_IDS.commands.p02Stop, 'eee.p02.stop', 'Parar P02', t.cmdP02Stop, p.cmdP02Stop, 'EEE.P02'),
    command(EEE_IDS.commands.resetFaults, 'eee.faults.reset', 'Resetar Falhas', t.cmdResetFaults, p.cmdResetFaults),
    command(EEE_IDS.commands.injectP01Fault, 'eee.p01.fault.inject', 'Injetar Falha P01', t.cmdInjectP01Fault, p.cmdInjectP01Fault, 'EEE.P01'),
    command(EEE_IDS.commands.injectP02Fault, 'eee.p02.fault.inject', 'Injetar Falha P02', t.cmdInjectP02Fault, p.cmdInjectP02Fault, 'EEE.P02'),
    command(EEE_IDS.commands.highDemandEnable, 'eee.high-demand.enable', 'Habilitar Alta Demanda', t.cmdHighDemandEnable, p.cmdHighDemandEnable),
    command(EEE_IDS.commands.highDemandDisable, 'eee.high-demand.disable', 'Desabilitar Alta Demanda', t.cmdHighDemandDisable, p.cmdHighDemandDisable),
    command(EEE_IDS.commands.badQualityEnable, 'eee.quality.bad.enable', 'Habilitar Qualidade Ruim', t.cmdBadQualityEnable, p.cmdBadQualityEnable),
    command(EEE_IDS.commands.badQualityDisable, 'eee.quality.bad.disable', 'Desabilitar Qualidade Ruim', t.cmdBadQualityDisable, p.cmdBadQualityDisable)
  ];

  const alarms = [
    alarm(EEE_IDS.alarms.levelHigh, 'Nível Alto', t.levelPct, p.levelPct, 'high', 'high', { setpoint: 75, alarmClass: 'Process', message: 'Nível alto no poço de sucção.' }),
    alarm(EEE_IDS.alarms.levelHighHigh, 'Nível Alto-Alto', t.levelPct, p.levelPct, 'highHigh', 'critical', { setpoint: 90, alarmClass: 'Process', message: 'Nível crítico no poço de sucção.' }),
    alarm(EEE_IDS.alarms.levelLow, 'Nível Baixo', t.levelPct, p.levelPct, 'low', 'medium', { setpoint: 20, alarmClass: 'Process', message: 'Nível baixo no poço de sucção.' }),
    alarm(EEE_IDS.alarms.p01Fault, 'Falha P01', t.p01Fault, p.p01Fault, 'digital', 'high', { alarmClass: 'Electrical', message: 'Bomba P01 em falha.' }),
    alarm(EEE_IDS.alarms.p01Trip, 'Trip P01', t.p01Trip, p.p01Trip, 'digital', 'high', { alarmClass: 'Electrical', message: 'Bomba P01 em trip.' }),
    alarm(EEE_IDS.alarms.p02Fault, 'Falha P02', t.p02Fault, p.p02Fault, 'digital', 'high', { alarmClass: 'Electrical', message: 'Bomba P02 em falha.' }),
    alarm(EEE_IDS.alarms.p02Trip, 'Trip P02', t.p02Trip, p.p02Trip, 'digital', 'high', { alarmClass: 'Electrical', message: 'Bomba P02 em trip.' }),
    alarm(EEE_IDS.alarms.p01Communication, 'Qualidade P01 Pressão', t.p01PressureBar, p.p01PressureBar, 'communication', 'high', { alarmClass: 'Communication', message: 'Medição de pressão P01 indisponível ou com qualidade não-Good.' })
  ];

  const operationalEvents = [
    event(EEE_IDS.events.pumpStarted, 'eee.pump.started', 'Bomba iniciada'), event(EEE_IDS.events.pumpStopped, 'eee.pump.stopped', 'Bomba parada'),
    event(EEE_IDS.events.faultInjected, 'eee.pump.fault-injected', 'Falha de bomba injetada'), event(EEE_IDS.events.faultReset, 'eee.pump.fault-reset', 'Falha de bomba resetada'),
    event(EEE_IDS.events.dutyChanged, 'eee.duty.changed', 'Bomba de vez alterada'), event(EEE_IDS.events.modeChanged, 'eee.mode.changed', 'Modo de operação alterado'),
    event(EEE_IDS.events.highDemandChanged, 'eee.high-demand.changed', 'Cenário de alta demanda alterado'), event(EEE_IDS.events.qualityScenarioChanged, 'eee.quality-scenario.changed', 'Cenário de qualidade alterado')
  ];

  const script = {
    id: EEE_IDS.script,
    path: 'scripts/eee-process.py',
    name: 'EEE Deterministic Process',
    scope: 'server',
    source: buildServerScriptSource(),
    enabled: true,
    language: 'python',
    languageVersion: '3',
    entryPoints: [
      { eventKind: 'initialize', handlerName: 'on_initialize', targetReference: null, tagReference: null, timerIntervalMs: null },
      { eventKind: 'timer', handlerName: 'on_tick', targetReference: null, tagReference: null, timerIntervalMs: 1000 }
    ],
    dependencies: tags.map(tag => ({ kind: 'serverMemoryTag', stableReference: tag.id })),
    description: 'Canonical generic Server Memory process model for the EliteSCADA EEE Demo.',
    metadata: { application: 'eee-demo', deterministicTickMs: '1000', writeStrategy: 'on-change' }
  };

  return {
    schema: base.schema,
    schemaVersion: base.schemaVersion,
    exportedAt: '2026-09-04T00:00:00.000Z',
    tags, alarms, dataSources: [source], templates: [], equipment: [], dynamos: [], screens: [], popups: [], securityRoles: [],
    commands, gateways: [], scripts: [script], scriptVisualEventReferences: [], visualAssets: [], reports: [], operationalEvents, startupScreenId: null
  };
}

function buildServerScriptSource(): string {
  const t = EEE_IDS.tags;
  const e = EEE_IDS.events;
  return [
    'def on_initialize():',
    `    if read_server_memory("${t.cmdAutoEnable}"):` , `        write_server_memory("${t.cmdAutoEnable}", False)`,
    `    if read_server_memory("${t.cmdAutoDisable}"):` , `        write_server_memory("${t.cmdAutoDisable}", False)`,
    `    if read_server_memory("${t.cmdP01Start}"):` , `        write_server_memory("${t.cmdP01Start}", False)`,
    `    if read_server_memory("${t.cmdP01Stop}"):` , `        write_server_memory("${t.cmdP01Stop}", False)`,
    `    if read_server_memory("${t.cmdP02Start}"):` , `        write_server_memory("${t.cmdP02Start}", False)`,
    `    if read_server_memory("${t.cmdP02Stop}"):` , `        write_server_memory("${t.cmdP02Stop}", False)`,
    `    if read_server_memory("${t.cmdResetFaults}"):` , `        write_server_memory("${t.cmdResetFaults}", False)`,
    `    if read_server_memory("${t.cmdInjectP01Fault}"):` , `        write_server_memory("${t.cmdInjectP01Fault}", False)`,
    `    if read_server_memory("${t.cmdInjectP02Fault}"):` , `        write_server_memory("${t.cmdInjectP02Fault}", False)`,
    `    if read_server_memory("${t.cmdHighDemandEnable}"):` , `        write_server_memory("${t.cmdHighDemandEnable}", False)`,
    `    if read_server_memory("${t.cmdHighDemandDisable}"):` , `        write_server_memory("${t.cmdHighDemandDisable}", False)`,
    `    if read_server_memory("${t.cmdBadQualityEnable}"):` , `        write_server_memory("${t.cmdBadQualityEnable}", False)`,
    `    if read_server_memory("${t.cmdBadQualityDisable}"):` , `        write_server_memory("${t.cmdBadQualityDisable}", False)`, '',
    'def on_tick():',
    `    level = read_server_memory("${t.levelPct}")`, `    old_inflow = read_server_memory("${t.inflowM3h}")`,
    `    old_total_flow = read_server_memory("${t.totalFlowM3h}")`, `    old_station_pressure = read_server_memory("${t.dischargePressureBar}")`,
    `    auto_mode = read_server_memory("${t.autoMode}")`, `    high_demand = read_server_memory("${t.highDemand}")`,
    `    bad_quality = read_server_memory("${t.badQualityScenario}")`, `    duty = read_server_memory("${t.dutyPump}")`,
    `    cycles = read_server_memory("${t.cycleCount}")`, `    p01_running = read_server_memory("${t.p01Running}")`,
    `    p02_running = read_server_memory("${t.p02Running}")`, `    p01_available = read_server_memory("${t.p01Available}")`,
    `    p02_available = read_server_memory("${t.p02Available}")`, `    p01_fault = read_server_memory("${t.p01Fault}")`,
    `    p02_fault = read_server_memory("${t.p02Fault}")`, `    p01_trip = read_server_memory("${t.p01Trip}")`,
    `    p02_trip = read_server_memory("${t.p02Trip}")`, `    old_p01_frequency = read_server_memory("${t.p01FrequencyHz}")`,
    `    old_p01_current = read_server_memory("${t.p01CurrentA}")`, `    old_p01_flow = read_server_memory("${t.p01FlowM3h}")`,
    `    old_p02_frequency = read_server_memory("${t.p02FrequencyHz}")`, `    old_p02_current = read_server_memory("${t.p02CurrentA}")`,
    `    old_p02_flow = read_server_memory("${t.p02FlowM3h}")`, `    old_p02_pressure = read_server_memory("${t.p02PressureBar}")`,
    `    cmd_auto_enable = read_server_memory("${t.cmdAutoEnable}")`, `    cmd_auto_disable = read_server_memory("${t.cmdAutoDisable}")`,
    `    cmd_p01_start = read_server_memory("${t.cmdP01Start}")`, `    cmd_p01_stop = read_server_memory("${t.cmdP01Stop}")`,
    `    cmd_p02_start = read_server_memory("${t.cmdP02Start}")`, `    cmd_p02_stop = read_server_memory("${t.cmdP02Stop}")`,
    `    cmd_reset_faults = read_server_memory("${t.cmdResetFaults}")`, `    cmd_inject_p01_fault = read_server_memory("${t.cmdInjectP01Fault}")`,
    `    cmd_inject_p02_fault = read_server_memory("${t.cmdInjectP02Fault}")`, `    cmd_high_demand_enable = read_server_memory("${t.cmdHighDemandEnable}")`,
    `    cmd_high_demand_disable = read_server_memory("${t.cmdHighDemandDisable}")`, `    cmd_bad_quality_enable = read_server_memory("${t.cmdBadQualityEnable}")`,
    `    cmd_bad_quality_disable = read_server_memory("${t.cmdBadQualityDisable}")`,
    '    old_level = level', '    old_auto_mode = auto_mode', '    old_high_demand = high_demand', '    old_bad_quality = bad_quality',
    '    old_duty = duty', '    old_cycles = cycles', '    old_p01_running = p01_running', '    old_p02_running = p02_running',
    '    old_p01_fault = p01_fault', '    old_p02_fault = p02_fault', '    old_p01_trip = p01_trip', '    old_p02_trip = p02_trip',
    '    if cmd_auto_enable:', '        auto_mode = True',
    '    if cmd_auto_disable:', '        auto_mode = False',
    '    if cmd_high_demand_enable:', '        high_demand = True',
    '    if cmd_high_demand_disable:', '        high_demand = False',
    '    if cmd_bad_quality_enable:', '        bad_quality = True',
    '    if cmd_bad_quality_disable:', '        bad_quality = False',
    '    if cmd_inject_p01_fault:', '        p01_fault = True', '        p01_trip = True',
    '    if cmd_inject_p02_fault:', '        p02_fault = True', '        p02_trip = True',
    '    if cmd_reset_faults:', '        p01_fault = False', '        p02_fault = False', '        p01_trip = False', '        p02_trip = False',
    '    if auto_mode:',
    '        if level >= 65.0 and not p01_running and not p02_running:',
    '            if duty == 1:',
    '                if p01_available and not p01_fault and not p01_trip:', '                    p01_running = True',
    '                else:', '                    if p02_available and not p02_fault and not p02_trip:', '                        p02_running = True',
    '            else:',
    '                if p02_available and not p02_fault and not p02_trip:', '                    p02_running = True',
    '                else:', '                    if p01_available and not p01_fault and not p01_trip:', '                        p01_running = True',
    '        if level >= 80.0 or high_demand:',
    '            if p01_running and not p02_running and p02_available and not p02_fault and not p02_trip:', '                p02_running = True',
    '            if p02_running and not p01_running and p01_available and not p01_fault and not p01_trip:', '                p01_running = True',
    '        if level <= 35.0 and (p01_running or p02_running):', '            p01_running = False', '            p02_running = False',
    '            cycles = cycles + 1', '            if duty == 1:', '                duty = 2', '            else:', '                duty = 1',
    '    else:',
    '        if cmd_p01_start and p01_available and not p01_fault and not p01_trip:', '            p01_running = True',
    '        if cmd_p01_stop:', '            p01_running = False',
    '        if cmd_p02_start and p02_available and not p02_fault and not p02_trip:', '            p02_running = True',
    '        if cmd_p02_stop:', '            p02_running = False',
    '    if p01_fault or p01_trip or not p01_available:', '        p01_running = False',
    '    if p02_fault or p02_trip or not p02_available:', '        p02_running = False',
    '    p01_flow = 0.0', '    p02_flow = 0.0', '    p01_frequency = 0.0', '    p02_frequency = 0.0',
    '    p01_current = 0.0', '    p02_current = 0.0', '    station_pressure = 0.0',
    '    if p01_running and p02_running:', '        p01_flow = 35.0', '        p02_flow = 35.0',
    '        p01_frequency = 45.0', '        p02_frequency = 45.0', '        p01_current = 20.0', '        p02_current = 20.0', '        station_pressure = 3.0',
    '    else:',
    '        if p01_running:', '            p01_flow = 38.0', '            p01_frequency = 48.0', '            p01_current = 22.0', '            station_pressure = 2.6',
    '        if p02_running:', '            p02_flow = 38.0', '            p02_frequency = 48.0', '            p02_current = 22.0', '            station_pressure = 2.6',
    '    total_flow = p01_flow + p02_flow', '    inflow = 20.0', '    if high_demand:', '        inflow = 55.0',
    '    level = level + ((inflow - total_flow) * 0.03)', '    if level < 0.0:', '        level = 0.0', '    if level > 100.0:', '        level = 100.0',
    '    p01_pressure = station_pressure if p01_running else 0.0', '    p02_pressure = station_pressure if p02_running else 0.0',
    '    if old_level != level:', `        write_server_memory("${t.levelPct}", level)`,
    '    if old_inflow != inflow:', `        write_server_memory("${t.inflowM3h}", inflow)`,
    '    if old_total_flow != total_flow:', `        write_server_memory("${t.totalFlowM3h}", total_flow)`,
    '    if old_station_pressure != station_pressure:', `        write_server_memory("${t.dischargePressureBar}", station_pressure)`,
    '    if old_auto_mode != auto_mode:', `        write_server_memory("${t.autoMode}", auto_mode)`,
    '    if old_high_demand != high_demand:', `        write_server_memory("${t.highDemand}", high_demand)`,
    '    if old_bad_quality != bad_quality:', `        write_server_memory("${t.badQualityScenario}", bad_quality)`,
    '    if old_duty != duty:', `        write_server_memory("${t.dutyPump}", duty)`,
    '    if old_cycles != cycles:', `        write_server_memory("${t.cycleCount}", cycles)`,
    '    if old_p01_running != p01_running:', `        write_server_memory("${t.p01Running}", p01_running)`,
    '    if old_p01_fault != p01_fault:', `        write_server_memory("${t.p01Fault}", p01_fault)`,
    '    if old_p01_trip != p01_trip:', `        write_server_memory("${t.p01Trip}", p01_trip)`,
    '    if old_p01_frequency != p01_frequency:', `        write_server_memory("${t.p01FrequencyHz}", p01_frequency)`,
    '    if old_p01_current != p01_current:', `        write_server_memory("${t.p01CurrentA}", p01_current)`,
    '    if old_p01_flow != p01_flow:', `        write_server_memory("${t.p01FlowM3h}", p01_flow)`,
    '    if old_p02_running != p02_running:', `        write_server_memory("${t.p02Running}", p02_running)`,
    '    if old_p02_fault != p02_fault:', `        write_server_memory("${t.p02Fault}", p02_fault)`,
    '    if old_p02_trip != p02_trip:', `        write_server_memory("${t.p02Trip}", p02_trip)`,
    '    if old_p02_frequency != p02_frequency:', `        write_server_memory("${t.p02FrequencyHz}", p02_frequency)`,
    '    if old_p02_current != p02_current:', `        write_server_memory("${t.p02CurrentA}", p02_current)`,
    '    if old_p02_flow != p02_flow:', `        write_server_memory("${t.p02FlowM3h}", p02_flow)`,
    '    if old_p02_pressure != p02_pressure:', `        write_server_memory("${t.p02PressureBar}", p02_pressure)`,
    '    if bad_quality:', `        publish_server_memory_sample("${t.p01PressureBar}", p01_pressure, "Unavailable")`,
    '    else:', `        write_server_memory("${t.p01PressureBar}", p01_pressure)`,
    '    if cmd_auto_enable:', `        write_server_memory("${t.cmdAutoEnable}", False)`,
    '    if cmd_auto_disable:', `        write_server_memory("${t.cmdAutoDisable}", False)`,
    '    if cmd_p01_start:', `        write_server_memory("${t.cmdP01Start}", False)`,
    '    if cmd_p01_stop:', `        write_server_memory("${t.cmdP01Stop}", False)`,
    '    if cmd_p02_start:', `        write_server_memory("${t.cmdP02Start}", False)`,
    '    if cmd_p02_stop:', `        write_server_memory("${t.cmdP02Stop}", False)`,
    '    if cmd_reset_faults:', `        write_server_memory("${t.cmdResetFaults}", False)`,
    '    if cmd_inject_p01_fault:', `        write_server_memory("${t.cmdInjectP01Fault}", False)`,
    '    if cmd_inject_p02_fault:', `        write_server_memory("${t.cmdInjectP02Fault}", False)`,
    '    if cmd_high_demand_enable:', `        write_server_memory("${t.cmdHighDemandEnable}", False)`,
    '    if cmd_high_demand_disable:', `        write_server_memory("${t.cmdHighDemandDisable}", False)`,
    '    if cmd_bad_quality_enable:', `        write_server_memory("${t.cmdBadQualityEnable}", False)`,
    '    if cmd_bad_quality_disable:', `        write_server_memory("${t.cmdBadQualityDisable}", False)`,
    '    if old_p01_running != p01_running:', '        if p01_running:', `            emit_operational_event("${e.pumpStarted}", "P01 iniciada", {"pump": "P01"})`,
    '        else:', `            emit_operational_event("${e.pumpStopped}", "P01 parada", {"pump": "P01"})`,
    '    if old_p02_running != p02_running:', '        if p02_running:', `            emit_operational_event("${e.pumpStarted}", "P02 iniciada", {"pump": "P02"})`,
    '        else:', `            emit_operational_event("${e.pumpStopped}", "P02 parada", {"pump": "P02"})`,
    '    if not old_p01_fault and p01_fault:', `        emit_operational_event("${e.faultInjected}", "Falha P01 injetada", {"pump": "P01"})`,
    '    if not old_p02_fault and p02_fault:', `        emit_operational_event("${e.faultInjected}", "Falha P02 injetada", {"pump": "P02"})`,
    '    if old_p01_fault and not p01_fault:', `        emit_operational_event("${e.faultReset}", "Falha P01 resetada", {"pump": "P01"})`,
    '    if old_p02_fault and not p02_fault:', `        emit_operational_event("${e.faultReset}", "Falha P02 resetada", {"pump": "P02"})`,
    '    if old_duty != duty:', `        emit_operational_event("${e.dutyChanged}", "Bomba de vez alterada", {"duty": str(duty)})`,
    '    if old_auto_mode != auto_mode:', `        emit_operational_event("${e.modeChanged}", "Modo de operação alterado", {"auto": str(auto_mode)})`,
    '    if old_high_demand != high_demand:', `        emit_operational_event("${e.highDemandChanged}", "Cenário de alta demanda alterado", {"enabled": str(high_demand)})`,
    '    if old_bad_quality != bad_quality:', `        emit_operational_event("${e.qualityScenarioChanged}", "Cenário de qualidade alterado", {"enabled": str(bad_quality)})`, ''
  ].join('\n');
}
