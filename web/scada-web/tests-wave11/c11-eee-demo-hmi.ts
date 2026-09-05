import { buildEeeFoundationPackage, EEE_IDS, EEE_PATHS } from './c11-eee-demo-foundation';
import { EEE_SECURITY_ROLES } from './c11-eee-demo-security';

const vid = (group: number, value: number) =>
  `c11${group}0000-0000-4000-8000-${String(value).padStart(12, '0')}`;

export const EEE_HMI = {
  dynamoKey: 'eee.dynamo.pump',
  screens: {
    main: { id: vid(7, 1), key: 'eee.main' },
    instrumentation: { id: vid(7, 2), key: 'eee.instrumentation' },
    electrical: { id: vid(7, 3), key: 'eee.electrical' },
    operation: { id: vid(7, 4), key: 'eee.operation' },
    trends: { id: vid(7, 5), key: 'eee.trends' },
    alarmsEvents: { id: vid(7, 6), key: 'eee.alarms-events' }
  },
  popups: {
    p01: { id: vid(8, 1), key: 'eee.popup.p01' },
    p02: { id: vid(8, 2), key: 'eee.popup.p02' }
  },
  dynamo: { id: vid(9, 1) },
  elements: {
    wetWell: vid(9, 100),
    p01: vid(9, 101),
    p02: vid(9, 102),
    mainPressure: vid(9, 103),
    alarmBrowser: vid(9, 104),
    eventBrowser: vid(9, 105),
    trend: vid(9, 106)
  }
} as const;

const C = {
  canvas: '#0B1220',
  panel: '#111C2E',
  panel2: '#17243A',
  border: '#334155',
  text: '#E2E8F0',
  muted: '#94A3B8',
  accent: '#38BDF8',
  water: '#0EA5E9',
  pipe: '#64748B',
  running: '#22C55E',
  warning: '#F59E0B',
  fault: '#EF4444',
  off: '#475569'
} as const;

type TagSpec = { id: string; path: string; dataType: 'boolean' | 'int32' | 'double'; unit?: string };

function tagBinding(spec: TagSpec, parameter?: string, decimalPlaces?: number) {
  return {
    key: 'text',
    kind: 'tag',
    target: spec.path,
    direction: null,
    metadata: {
      sourceDataType: spec.dataType,
      ...(spec.unit ? { engineeringUnit: spec.unit } : {}),
      ...(decimalPlaces === undefined ? {} : { decimalPlaces: String(decimalPlaces) }),
      ...(parameter ? { dynamoParameter: parameter } : {})
    },
    tagReference: { tagId: spec.id, selector: null }
  };
}

function visibleBinding(spec: TagSpec, parameter?: string) {
  return {
    key: 'visible',
    kind: 'tag',
    target: spec.path,
    direction: null,
    metadata: parameter ? { dynamoParameter: parameter } : {},
    tagReference: { tagId: spec.id, selector: null }
  };
}

function text(id: string, key: string, value: string, x: number, y: number, width: number, height: number,
  options: { fontSize?: number; color?: string; weight?: string; align?: string; z?: number } = {}) {
  return {
    id, key, type: 'core.text',
    properties: {
      x, y, width, height, text: value,
      fontSize: options.fontSize ?? 22,
      fontWeight: options.weight ?? '500',
      color: options.color ?? C.text,
      textAlign: options.align ?? 'left',
      zIndex: options.z ?? 10,
      visible: true
    }
  };
}

function rect(id: string, key: string, x: number, y: number, width: number, height: number,
  fillColor = C.panel, options: { borderColor?: string; borderWidth?: number; radius?: number; z?: number } = {}) {
  return {
    id, key, type: 'core.rectangle',
    properties: {
      x, y, width, height, fillColor,
      borderColor: options.borderColor ?? C.border,
      borderWidth: options.borderWidth ?? 1,
      borderRadius: options.radius ?? 12,
      zIndex: options.z ?? 1,
      visible: true
    }
  };
}

function valueDisplay(id: string, key: string, label: string, spec: TagSpec, x: number, y: number, width = 170, decimalPlaces = 1) {
  return {
    id, key, type: 'core.valueDisplay',
    properties: {
      x, y, width, height: 58,
      text: '—', fontSize: 24, fontWeight: '700', color: C.text,
      backgroundColor: C.panel2, borderColor: C.border, borderWidth: 1, borderRadius: 10,
      textAlign: 'center', zIndex: 15, visible: true,
      tooltip: label
    },
    bindings: [tagBinding(spec, undefined, decimalPlaces)]
  };
}

function button(id: string, key: string, label: string, x: number, y: number, width: number,
  action: any, fill = C.panel2) {
  return {
    id, key, type: 'core.button',
    properties: {
      x, y, width, height: 48, text: label, fontSize: 18, fontWeight: '700',
      color: C.text, backgroundColor: fill, borderColor: C.border, borderWidth: 1,
      borderRadius: 8, zIndex: 30, visible: true, enabled: true, cursor: 'pointer'
    },
    actions: [{ eventKey: 'click', ...action, version: 1 }]
  };
}

function tag(id: string, path: string, dataType: TagSpec['dataType'], unit?: string): TagSpec {
  return { id, path, dataType, unit };
}

const T = {
  level: tag(EEE_IDS.tags.levelPct, EEE_PATHS.levelPct, 'double', '%'),
  inflow: tag(EEE_IDS.tags.inflowM3h, EEE_PATHS.inflowM3h, 'double', 'm³/h'),
  totalFlow: tag(EEE_IDS.tags.totalFlowM3h, EEE_PATHS.totalFlowM3h, 'double', 'm³/h'),
  dischargePressure: tag(EEE_IDS.tags.dischargePressureBar, EEE_PATHS.dischargePressureBar, 'double', 'bar'),
  autoMode: tag(EEE_IDS.tags.autoMode, EEE_PATHS.autoMode, 'boolean'),
  dutyPump: tag(EEE_IDS.tags.dutyPump, EEE_PATHS.dutyPump, 'int32'),
  cycleCount: tag(EEE_IDS.tags.cycleCount, EEE_PATHS.cycleCount, 'int32'),
  p01Running: tag(EEE_IDS.tags.p01Running, EEE_PATHS.p01Running, 'boolean'),
  p01Fault: tag(EEE_IDS.tags.p01Fault, EEE_PATHS.p01Fault, 'boolean'),
  p01Trip: tag(EEE_IDS.tags.p01Trip, EEE_PATHS.p01Trip, 'boolean'),
  p01Current: tag(EEE_IDS.tags.p01CurrentA, EEE_PATHS.p01CurrentA, 'double', 'A'),
  p01Frequency: tag(EEE_IDS.tags.p01FrequencyHz, EEE_PATHS.p01FrequencyHz, 'double', 'Hz'),
  p01Flow: tag(EEE_IDS.tags.p01FlowM3h, EEE_PATHS.p01FlowM3h, 'double', 'm³/h'),
  p01Pressure: tag(EEE_IDS.tags.p01PressureBar, EEE_PATHS.p01PressureBar, 'double', 'bar'),
  p02Running: tag(EEE_IDS.tags.p02Running, EEE_PATHS.p02Running, 'boolean'),
  p02Fault: tag(EEE_IDS.tags.p02Fault, EEE_PATHS.p02Fault, 'boolean'),
  p02Trip: tag(EEE_IDS.tags.p02Trip, EEE_PATHS.p02Trip, 'boolean'),
  p02Current: tag(EEE_IDS.tags.p02CurrentA, EEE_PATHS.p02CurrentA, 'double', 'A'),
  p02Frequency: tag(EEE_IDS.tags.p02FrequencyHz, EEE_PATHS.p02FrequencyHz, 'double', 'Hz'),
  p02Flow: tag(EEE_IDS.tags.p02FlowM3h, EEE_PATHS.p02FlowM3h, 'double', 'm³/h'),
  p02Pressure: tag(EEE_IDS.tags.p02PressureBar, EEE_PATHS.p02PressureBar, 'double', 'bar')
};

function navElements(activeKey: string) {
  const items = [
    ['eee.main', 'EEE PRINCIPAL'], ['eee.instrumentation', 'INSTRUMENTAÇÃO'], ['eee.electrical', 'ELÉTRICO'],
    ['eee.operation', 'OPERAÇÃO'], ['eee.trends', 'TENDÊNCIAS'], ['eee.alarms-events', 'ALARMES / EVENTOS']
  ] as const;
  return [
    rect(vid(9, 2000 + navHash(activeKey)), `nav-bg-${activeKey}`, 0, 0, 1920, 82, '#08101D', { borderColor: '#1E293B', borderWidth: 0, radius: 0, z: 90 }),
    text(vid(9, 2100 + navHash(activeKey)), `nav-title-${activeKey}`, 'EliteSCADA  ·  ESTAÇÃO ELEVATÓRIA DE ESGOTO', 36, 18, 560, 46,
      { fontSize: 24, weight: '800', color: C.text, z: 95 }),
    ...items.map(([key, label], index) => button(
      vid(9, 2200 + navHash(activeKey) * 10 + index), `nav-${activeKey}-${key}`, label,
      650 + index * 200, 17, 184,
      { kind: 'navigateScreen', targetKey: key },
      key === activeKey ? '#075985' : '#17243A'
    ))
  ];
}

function navHash(key: string) {
  return ['eee.main', 'eee.instrumentation', 'eee.electrical', 'eee.operation', 'eee.trends', 'eee.alarms-events'].indexOf(key) + 1;
}

function pumpDynamo() {
  const parameters = [
    'running', 'fault', 'trip', 'current', 'frequency', 'flow', 'pressure'
  ].map(key => ({ key, kind: 'tagReference', required: true, defaultValue: null, defaultTagReference: null, version: 1 }));

  const p01 = {
    running: T.p01Running, fault: T.p01Fault, trip: T.p01Trip,
    current: T.p01Current, frequency: T.p01Frequency, flow: T.p01Flow, pressure: T.p01Pressure
  };

  return {
    id: EEE_HMI.dynamo.id,
    key: EEE_HMI.dynamoKey,
    name: 'Bomba EEE — reutilizável',
    templateKey: null,
    bindings: [], properties: {}, context: {},
    metadata: { application: 'eee-demo', role: 'canonical-pump-dynamo' },
    parameters,
    elements: [
      rect(vid(9, 3001), 'pump-card', 0, 0, 360, 390, C.panel, { borderColor: '#475569', borderWidth: 2, radius: 18, z: 1 }),
      text(vid(9, 3002), 'pump-title', 'CONJUNTO MOTOBOMBA', 28, 20, 304, 38, { fontSize: 21, weight: '800', align: 'center', z: 4 }),
      { id: vid(9, 3003), key: 'pump-body', type: 'core.ellipse', properties: { x: 90, y: 78, width: 180, height: 150, fillColor: C.off, borderColor: '#CBD5E1', borderWidth: 4, zIndex: 4, visible: true } },
      { id: vid(9, 3004), key: 'pump-running', type: 'core.ellipse', properties: { x: 90, y: 78, width: 180, height: 150, fillColor: C.running, borderColor: '#BBF7D0', borderWidth: 4, zIndex: 5, visible: false }, bindings: [visibleBinding(p01.running, 'running')] },
      { id: vid(9, 3005), key: 'pump-fault', type: 'core.ellipse', properties: { x: 90, y: 78, width: 180, height: 150, fillColor: C.fault, borderColor: '#FECACA', borderWidth: 5, zIndex: 7, visible: false }, bindings: [visibleBinding(p01.fault, 'fault')] },
      { id: vid(9, 3006), key: 'pump-trip-ring', type: 'core.ellipse', properties: { x: 76, y: 64, width: 208, height: 178, fillColor: 'transparent', borderColor: C.warning, borderWidth: 8, zIndex: 8, visible: false }, bindings: [visibleBinding(p01.trip, 'trip')] },
      text(vid(9, 3007), 'pump-stopped-label', 'PARADA', 115, 132, 130, 38, { fontSize: 22, weight: '900', align: 'center', z: 10 }),
      { ...text(vid(9, 3008), 'pump-running-label', 'OPERANDO', 105, 132, 150, 38, { fontSize: 22, weight: '900', align: 'center', color: '#052E16', z: 11 }), bindings: [visibleBinding(p01.running, 'running')] },
      { ...text(vid(9, 3009), 'pump-fault-label', 'FALHA', 115, 132, 130, 38, { fontSize: 24, weight: '900', align: 'center', color: '#FFFFFF', z: 12 }), bindings: [visibleBinding(p01.fault, 'fault')] },
      text(vid(9, 3010), 'pump-current-label', 'CORRENTE', 24, 260, 92, 28, { fontSize: 14, color: C.muted, weight: '700', z: 10 }),
      { id: vid(9, 3011), key: 'pump-current', type: 'core.valueDisplay', properties: { x: 24, y: 288, width: 130, height: 48, text: '—', fontSize: 21, fontWeight: '800', color: C.text, backgroundColor: C.panel2, borderColor: C.border, borderWidth: 1, borderRadius: 8, textAlign: 'center', zIndex: 10, visible: true }, bindings: [tagBinding(p01.current, 'current', 1)] },
      text(vid(9, 3012), 'pump-frequency-label', 'FREQUÊNCIA', 206, 260, 120, 28, { fontSize: 14, color: C.muted, weight: '700', z: 10 }),
      { id: vid(9, 3013), key: 'pump-frequency', type: 'core.valueDisplay', properties: { x: 206, y: 288, width: 130, height: 48, text: '—', fontSize: 21, fontWeight: '800', color: C.text, backgroundColor: C.panel2, borderColor: C.border, borderWidth: 1, borderRadius: 8, textAlign: 'center', zIndex: 10, visible: true }, bindings: [tagBinding(p01.frequency, 'frequency', 1)] },
      text(vid(9, 3014), 'pump-detail-hint', 'Toque para detalhes e comandos', 40, 350, 280, 24, { fontSize: 14, color: C.accent, align: 'center', z: 10 })
    ]
  };
}

function dynamoParameter(key: string, spec: TagSpec) {
  return { key, kind: 'tagReference', value: null, tagReference: { tagId: spec.id, selector: null }, version: 1 };
}

function pumpInstance(id: string, key: string, label: string, x: number, y: number, popupKey: string, specs: {
  running: TagSpec; fault: TagSpec; trip: TagSpec; current: TagSpec; frequency: TagSpec; flow: TagSpec; pressure: TagSpec;
}) {
  return {
    id, key, type: 'dynamo', dynamoKey: EEE_HMI.dynamoKey,
    equipmentPath: label,
    properties: { x, y, width: 360, height: 390, zIndex: 20, visible: true, cursor: 'pointer' },
    dynamoParameters: Object.entries(specs).map(([parameter, spec]) => dynamoParameter(parameter, spec)),
    actions: [{ eventKey: 'click', kind: 'openPopup', targetKey: popupKey, version: 1 }],
    metadata: { application: 'eee-demo', pump: label }
  };
}

function mainScreen() {
  const elements: any[] = [
    rect(vid(9, 4001), 'main-canvas', 0, 82, 1920, 998, C.canvas, { borderWidth: 0, radius: 0, z: 0 }),
    ...navElements(EEE_HMI.screens.main.key),
    text(vid(9, 4002), 'main-heading', 'EEE PRINCIPAL', 54, 116, 400, 50, { fontSize: 34, weight: '900' }),
    text(vid(9, 4003), 'main-subheading', 'Visão operacional do processo · DEMO Simulation', 54, 164, 560, 30, { fontSize: 17, color: C.muted }),

    rect(vid(9, 4010), 'well-panel', 54, 224, 540, 720, C.panel, { borderColor: '#334155', borderWidth: 1, radius: 18, z: 2 }),
    text(vid(9, 4011), 'well-title', 'POÇO DE SUCÇÃO', 84, 252, 480, 36, { fontSize: 22, weight: '800', align: 'center', z: 5 }),
    {
      id: EEE_HMI.elements.wetWell,
      key: 'wet-well-liquid',
      type: 'core.rectangle',
      properties: {
        x: 146, y: 326, width: 356, height: 430,
        fillColor: '#07111F', borderColor: '#94A3B8', borderWidth: 5, borderRadius: 22,
        zIndex: 5, visible: true, tooltip: 'Nível do poço de sucção'
      },
      analogFill: {
        direction: 'bottomToTop',
        source: { kind: 'tag', valueType: 'number', target: T.level.path, tagReference: { tagId: T.level.id, selector: null }, version: 1 },
        inputMinimum: 0,
        inputMaximum: 100,
        fillColor: C.water,
        fillOpacity: 0.82,
        version: 1
      }
    },
    text(vid(9, 4013), 'well-level-label', 'NÍVEL', 230, 790, 120, 28, { fontSize: 15, color: C.muted, weight: '800', align: 'center', z: 10 }),
    valueDisplay(vid(9, 4014), 'well-level-value', 'Nível do poço', T.level, 204, 820, 240, 1),
    text(vid(9, 4015), 'well-inflow-label', 'VAZÃO AFLUENTE', 90, 892, 170, 24, { fontSize: 14, color: C.muted, weight: '700' }),
    valueDisplay(vid(9, 4016), 'well-inflow-value', 'Vazão afluente', T.inflow, 86, 918, 190, 1),
    text(vid(9, 4017), 'well-flow-label', 'RECALQUE TOTAL', 362, 892, 170, 24, { fontSize: 14, color: C.muted, weight: '700' }),
    valueDisplay(vid(9, 4018), 'well-flow-value', 'Vazão de recalque', T.totalFlow, 356, 918, 190, 1),

    text(vid(9, 4020), 'pump01-label', 'P01', 730, 224, 110, 40, { fontSize: 28, weight: '900', color: C.accent }),
    text(vid(9, 4021), 'pump02-label', 'P02', 1190, 224, 110, 40, { fontSize: 28, weight: '900', color: C.accent }),
    pumpInstance(EEE_HMI.elements.p01, 'pump-instance-p01', 'EEE.P01', 650, 270, EEE_HMI.popups.p01.key,
      { running: T.p01Running, fault: T.p01Fault, trip: T.p01Trip, current: T.p01Current, frequency: T.p01Frequency, flow: T.p01Flow, pressure: T.p01Pressure }),
    pumpInstance(EEE_HMI.elements.p02, 'pump-instance-p02', 'EEE.P02', 1110, 270, EEE_HMI.popups.p02.key,
      { running: T.p02Running, fault: T.p02Fault, trip: T.p02Trip, current: T.p02Current, frequency: T.p02Frequency, flow: T.p02Flow, pressure: T.p02Pressure }),

    rect(vid(9, 4030), 'discharge-header', 650, 720, 820, 224, C.panel, { borderColor: '#334155', radius: 18, z: 2 }),
    text(vid(9, 4031), 'discharge-title', 'LINHA DE RECALQUE', 688, 746, 300, 34, { fontSize: 21, weight: '800', z: 8 }),
    { id: vid(9, 4032), key: 'pipe-main', type: 'core.line', properties: { x: 720, y: 826, width: 650, height: 20, x1: 0, y1: 10, x2: 650, y2: 10, strokeColor: C.pipe, strokeWidth: 18, zIndex: 6, visible: true } },
    text(vid(9, 4033), 'pressure-label', 'PRESSÃO', 730, 866, 130, 24, { fontSize: 14, color: C.muted, weight: '700', z: 8 }),
    { ...valueDisplay(EEE_HMI.elements.mainPressure, 'main-pressure', 'Pressão de recalque', T.dischargePressure, 716, 892, 190, 1), properties: { ...valueDisplay(EEE_HMI.elements.mainPressure, 'main-pressure', 'Pressão de recalque', T.dischargePressure, 716, 892, 190, 1).properties, zIndex: 9 } },
    text(vid(9, 4035), 'total-flow-label', 'VAZÃO', 990, 866, 130, 24, { fontSize: 14, color: C.muted, weight: '700', z: 8 }),
    valueDisplay(vid(9, 4036), 'main-flow', 'Vazão total', T.totalFlow, 976, 892, 190, 1),
    text(vid(9, 4037), 'duty-label', 'BOMBA DE VEZ', 1250, 866, 150, 24, { fontSize: 14, color: C.muted, weight: '700', z: 8 }),
    valueDisplay(vid(9, 4038), 'main-duty', 'Bomba de vez', T.dutyPump, 1236, 892, 190, 0),

    rect(vid(9, 4040), 'status-panel', 1512, 224, 354, 720, C.panel, { borderColor: '#334155', radius: 18, z: 2 }),
    text(vid(9, 4041), 'status-title', 'ESTADO DA ESTAÇÃO', 1540, 252, 300, 34, { fontSize: 21, weight: '800', align: 'center', z: 5 }),
    text(vid(9, 4042), 'auto-label', 'MODO AUTOMÁTICO', 1554, 322, 220, 26, { fontSize: 14, color: C.muted, weight: '700', z: 5 }),
    valueDisplay(vid(9, 4043), 'auto-value', 'Modo automático', T.autoMode, 1550, 352, 250, 0),
    text(vid(9, 4044), 'cycle-label', 'CICLOS', 1554, 430, 120, 26, { fontSize: 14, color: C.muted, weight: '700', z: 5 }),
    valueDisplay(vid(9, 4045), 'cycle-value', 'Contador de ciclos', T.cycleCount, 1550, 460, 250, 0),
    button(vid(9, 4046), 'open-p01-popup', 'DETALHES P01', 1550, 560, 250, { kind: 'openPopup', targetKey: EEE_HMI.popups.p01.key }, '#0C4A6E'),
    button(vid(9, 4047), 'open-p02-popup', 'DETALHES P02', 1550, 624, 250, { kind: 'openPopup', targetKey: EEE_HMI.popups.p02.key }, '#0C4A6E'),
    button(vid(9, 4048), 'goto-alarms-events', 'ALARMES / EVENTOS', 1550, 720, 250, { kind: 'navigateScreen', targetKey: EEE_HMI.screens.alarmsEvents.key }, '#7F1D1D'),
    button(vid(9, 4049), 'goto-trends', 'TENDÊNCIAS', 1550, 784, 250, { kind: 'navigateScreen', targetKey: EEE_HMI.screens.trends.key }, '#164E63')
  ];
  return { id: EEE_HMI.screens.main.id, key: EEE_HMI.screens.main.key, name: 'EEE Principal', route: '/eee', elements, properties: {}, context: {}, metadata: { application: 'eee-demo', role: 'startup' } };
}

function kpiPanel(baseId: number, titleValue: string, specs: Array<[string, TagSpec, number]>) {
  const elements: any[] = [rect(vid(9, baseId), `panel-${baseId}`, 80, 210, 1760, 720, C.panel, { radius: 18, z: 1 }),
    text(vid(9, baseId + 1), `title-${baseId}`, titleValue, 120, 244, 900, 48, { fontSize: 30, weight: '900' })];
  specs.forEach(([label, spec, decimals], index) => {
    const col = index % 3;
    const row = Math.floor(index / 3);
    const x = 140 + col * 540;
    const y = 340 + row * 170;
    elements.push(text(vid(9, baseId + 10 + index * 2), `label-${baseId}-${index}`, label, x, y, 360, 30, { fontSize: 16, color: C.muted, weight: '700' }));
    elements.push(valueDisplay(vid(9, baseId + 11 + index * 2), `value-${baseId}-${index}`, label, spec, x, y + 40, 360, decimals));
  });
  return elements;
}

function secondaryScreen(id: string, key: string, name: string, elements: any[]) {
  return {
    id, key, name, route: `/eee/${key.split('.').pop()}`,
    elements: [rect(vid(9, 7000 + navHash(key)), `canvas-${key}`, 0, 82, 1920, 998, C.canvas, { borderWidth: 0, radius: 0, z: 0 }), ...navElements(key), ...elements],
    properties: {}, context: {}, metadata: { application: 'eee-demo' }
  };
}

function instrumentationScreen() {
  return secondaryScreen(EEE_HMI.screens.instrumentation.id, EEE_HMI.screens.instrumentation.key, 'Instrumentação',
    kpiPanel(5000, 'INSTRUMENTAÇÃO E PROCESSO', [
      ['Nível do poço', T.level, 1], ['Vazão afluente', T.inflow, 1], ['Vazão de recalque', T.totalFlow, 1],
      ['Pressão de recalque', T.dischargePressure, 2], ['Pressão P01', T.p01Pressure, 2], ['Pressão P02', T.p02Pressure, 2],
      ['Vazão P01', T.p01Flow, 1], ['Vazão P02', T.p02Flow, 1]
    ]));
}

function electricalScreen() {
  return secondaryScreen(EEE_HMI.screens.electrical.id, EEE_HMI.screens.electrical.key, 'Sistema Elétrico',
    kpiPanel(5200, 'SISTEMA ELÉTRICO', [
      ['Corrente P01', T.p01Current, 1], ['Frequência P01', T.p01Frequency, 1], ['Pressão P01', T.p01Pressure, 2],
      ['Corrente P02', T.p02Current, 1], ['Frequência P02', T.p02Frequency, 1], ['Pressão P02', T.p02Pressure, 2]
    ]));
}

function operationScreen() {
  const e: any[] = [
    rect(vid(9, 5400), 'operation-panel', 80, 210, 1760, 720, C.panel, { radius: 18 }),
    text(vid(9, 5401), 'operation-title', 'OPERAÇÃO E CENÁRIOS DA DEMO', 120, 244, 900, 48, { fontSize: 30, weight: '900' }),
    text(vid(9, 5402), 'operation-info', 'Comandos seguem o caminho autenticado EliteSCADA Command → TAG de solicitação → Server Script Active.', 120, 300, 1500, 36, { fontSize: 17, color: C.muted }),
    button(vid(9, 5410), 'auto-enable', 'HABILITAR AUTO', 150, 390, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.autoEnable }, '#14532D'),
    button(vid(9, 5411), 'auto-disable', 'DESABILITAR AUTO', 480, 390, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.autoDisable }, '#7C2D12'),
    button(vid(9, 5412), 'high-demand-on', 'ALTA DEMANDA ON', 150, 480, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.highDemandEnable }, '#854D0E'),
    button(vid(9, 5413), 'high-demand-off', 'ALTA DEMANDA OFF', 480, 480, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.highDemandDisable }),
    button(vid(9, 5414), 'bad-quality-on', 'QUALIDADE RUIM P01', 150, 570, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.badQualityEnable }, '#7F1D1D'),
    button(vid(9, 5415), 'bad-quality-off', 'RESTAURAR QUALIDADE', 480, 570, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.badQualityDisable }, '#14532D'),
    button(vid(9, 5416), 'inject-p01-fault', 'INJETAR FALHA P01', 970, 390, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.injectP01Fault }, '#7F1D1D'),
    button(vid(9, 5417), 'inject-p02-fault', 'INJETAR FALHA P02', 1300, 390, 300, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.injectP02Fault }, '#7F1D1D'),
    button(vid(9, 5418), 'reset-faults', 'RESETAR FALHAS', 970, 480, 630, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.resetFaults }, '#14532D'),
    text(vid(9, 5420), 'operation-auto-label', 'Modo automático', 1000, 610, 220, 28, { fontSize: 15, color: C.muted, weight: '700' }),
    valueDisplay(vid(9, 5421), 'operation-auto-value', 'Modo automático', T.autoMode, 980, 646, 260, 0),
    text(vid(9, 5422), 'operation-duty-label', 'Bomba de vez', 1320, 610, 220, 28, { fontSize: 15, color: C.muted, weight: '700' }),
    valueDisplay(vid(9, 5423), 'operation-duty-value', 'Bomba de vez', T.dutyPump, 1300, 646, 260, 0)
  ];
  return secondaryScreen(EEE_HMI.screens.operation.id, EEE_HMI.screens.operation.key, 'Operação', e);
}

function trendPen(id: string, spec: TagSpec, label: string, color: string, axis: 'left' | 'right') {
  return { id, tagId: spec.id, tagPath: spec.path, label, visible: true, unit: spec.unit ?? '', color, lineWidth: 2, lineStyle: 'solid', axis, scale: { mode: 'auto' } };
}

function trendsScreen() {
  const pens = [
    trendPen('eee-level', T.level, 'Nível', '#38BDF8', 'left'),
    trendPen('eee-flow', T.totalFlow, 'Vazão', '#22C55E', 'right'),
    trendPen('eee-pressure', T.dischargePressure, 'Pressão', '#F59E0B', 'right'),
    trendPen('eee-p01-current', T.p01Current, 'Corrente P01', '#A78BFA', 'left'),
    trendPen('eee-p02-current', T.p02Current, 'Corrente P02', '#F472B6', 'left')
  ];
  const trend = {
    id: EEE_HMI.elements.trend, key: 'eee-process-trend', type: 'core.trend',
    properties: {
      x: 100, y: 220, width: 1720, height: 760, zIndex: 10, visible: true, opacity: 1,
      trendMode: 'live', trendWindowSeconds: 900, trendRefreshSeconds: 1,
      trendLegendVisible: true, trendGridVisible: true, trendAxesVisible: true, trendQualityVisible: true,
      pens
    }
  };
  return secondaryScreen(EEE_HMI.screens.trends.id, EEE_HMI.screens.trends.key, 'Tendências', [trend]);
}

function alarmsEventsScreen() {
  return secondaryScreen(EEE_HMI.screens.alarmsEvents.id, EEE_HMI.screens.alarmsEvents.key, 'Alarmes e Eventos', [
    text(vid(9, 5601), 'alarms-title', 'ALARMES', 100, 206, 500, 42, { fontSize: 26, weight: '900' }),
    text(vid(9, 5602), 'events-title', 'EVENTOS OPERACIONAIS', 1000, 206, 600, 42, { fontSize: 26, weight: '900' }),
    {
      id: EEE_HMI.elements.alarmBrowser, key: 'eee-alarm-browser', type: 'core.alarmBrowser',
      properties: { x: 100, y: 260, width: 820, height: 700, zIndex: 10, visible: true, browserConfig: { mode: 'current', lifecycle: 'active', area: 'EEE', pageSize: 20 } }
    },
    {
      id: EEE_HMI.elements.eventBrowser, key: 'eee-event-browser', type: 'core.eventBrowser',
      properties: { x: 1000, y: 260, width: 820, height: 700, zIndex: 10, visible: true, browserConfig: { source: 'server-script', area: 'EEE', pageSize: 30 } }
    }
  ]);
}

function pumpPopup(id: string, key: string, name: string, specs: { current: TagSpec; frequency: TagSpec; flow: TagSpec; pressure: TagSpec }, commands: { start: string; stop: string; fault: string }) {
  return {
    id, key, name, templateKey: null, x: 520, y: 210,
    properties: {}, context: {}, metadata: { application: 'eee-demo', role: 'pump-popup' },
    elements: [
      rect(vid(9, key.endsWith('p01') ? 6001 : 6101), `${key}-panel`, 0, 0, 880, 600, '#0F1A2C', { borderColor: '#64748B', borderWidth: 2, radius: 18, z: 1 }),
      text(vid(9, key.endsWith('p01') ? 6002 : 6102), `${key}-title`, name, 40, 28, 520, 44, { fontSize: 30, weight: '900', z: 5 }),
      text(vid(9, key.endsWith('p01') ? 6003 : 6103), `${key}-subtitle`, 'Detalhe operacional · comandos mediados pelo Runtime', 40, 74, 680, 30, { fontSize: 16, color: C.muted, z: 5 }),
      text(vid(9, key.endsWith('p01') ? 6010 : 6110), `${key}-current-label`, 'CORRENTE', 60, 150, 160, 28, { fontSize: 14, color: C.muted, weight: '700' }),
      valueDisplay(vid(9, key.endsWith('p01') ? 6011 : 6111), `${key}-current`, 'Corrente', specs.current, 50, 184, 220, 1),
      text(vid(9, key.endsWith('p01') ? 6012 : 6112), `${key}-freq-label`, 'FREQUÊNCIA', 330, 150, 160, 28, { fontSize: 14, color: C.muted, weight: '700' }),
      valueDisplay(vid(9, key.endsWith('p01') ? 6013 : 6113), `${key}-frequency`, 'Frequência', specs.frequency, 320, 184, 220, 1),
      text(vid(9, key.endsWith('p01') ? 6014 : 6114), `${key}-pressure-label`, 'PRESSÃO', 600, 150, 160, 28, { fontSize: 14, color: C.muted, weight: '700' }),
      valueDisplay(vid(9, key.endsWith('p01') ? 6015 : 6115), `${key}-pressure`, 'Pressão', specs.pressure, 590, 184, 220, 2),
      text(vid(9, key.endsWith('p01') ? 6016 : 6116), `${key}-flow-label`, 'VAZÃO', 60, 280, 160, 28, { fontSize: 14, color: C.muted, weight: '700' }),
      valueDisplay(vid(9, key.endsWith('p01') ? 6017 : 6117), `${key}-flow`, 'Vazão', specs.flow, 50, 314, 220, 1),
      button(vid(9, key.endsWith('p01') ? 6020 : 6120), `${key}-start`, 'PARTIR', 330, 314, 220, { kind: 'executeCommand', targetKey: null, commandId: commands.start }, '#14532D'),
      button(vid(9, key.endsWith('p01') ? 6021 : 6121), `${key}-stop`, 'PARAR', 590, 314, 220, { kind: 'executeCommand', targetKey: null, commandId: commands.stop }, '#7C2D12'),
      button(vid(9, key.endsWith('p01') ? 6022 : 6122), `${key}-fault`, 'INJETAR FALHA (DEMO)', 50, 430, 350, { kind: 'executeCommand', targetKey: null, commandId: commands.fault }, '#7F1D1D'),
      button(vid(9, key.endsWith('p01') ? 6023 : 6123), `${key}-reset`, 'RESETAR FALHAS', 430, 430, 350, { kind: 'executeCommand', targetKey: null, commandId: EEE_IDS.commands.resetFaults }, '#14532D'),
      button(vid(9, key.endsWith('p01') ? 6024 : 6124), `${key}-close`, 'FECHAR', 650, 522, 160, { kind: 'closePopup', targetKey: key }, '#334155')
    ]
  };
}

export function buildEeeDemoPackage(base: any): any {
  const packageData = buildEeeFoundationPackage(base);
  packageData.securityRoles = [...EEE_SECURITY_ROLES];
  packageData.dynamos = [pumpDynamo()];
  packageData.screens = [mainScreen(), instrumentationScreen(), electricalScreen(), operationScreen(), trendsScreen(), alarmsEventsScreen()];
  packageData.popups = [
    pumpPopup(EEE_HMI.popups.p01.id, EEE_HMI.popups.p01.key, 'BOMBA P01', { current: T.p01Current, frequency: T.p01Frequency, flow: T.p01Flow, pressure: T.p01Pressure },
      { start: EEE_IDS.commands.p01Start, stop: EEE_IDS.commands.p01Stop, fault: EEE_IDS.commands.injectP01Fault }),
    pumpPopup(EEE_HMI.popups.p02.id, EEE_HMI.popups.p02.key, 'BOMBA P02', { current: T.p02Current, frequency: T.p02Frequency, flow: T.p02Flow, pressure: T.p02Pressure },
      { start: EEE_IDS.commands.p02Start, stop: EEE_IDS.commands.p02Stop, fault: EEE_IDS.commands.injectP02Fault })
  ];
  packageData.startupScreenId = EEE_HMI.screens.main.id;
  return packageData;
}
