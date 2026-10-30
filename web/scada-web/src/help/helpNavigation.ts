export type SearchableHelpTopic = Readonly<{
  id: string; category: string; title: string; summary: string;
  sections: readonly Readonly<{ heading: string; body: string; code?: string | null;
    fields?: readonly Readonly<{ label: string; key: string; description: string; example: string }>[] | null }>[];
}>;

export const helpAreas = ['start', 'data', 'drivers', 'assets', 'visual', 'scripts', 'intelligence', 'history', 'events', 'security', 'licensing', 'administration', 'runtime'] as const;
export type HelpArea = typeof helpAreas[number];

export function helpTopicPriority(id: string): number {
  const ordered = ['help.application-map', 'help.find-your-way', 'getting-started.startup-authentication', 'getting-started.neutral-bootstrap',
    'engineering.shell-navigation', 'engineering.lifecycle', 'installation.network-ports', 'visual.first-screen', 'screens.overview', 'popups.overview',
    'visual.object-selection', 'visual.properties', 'visual.dynamics', 'visual.events', 'scripts.choose-scope',
    'scripts.engineering', 'scripts.triggers-dependencies', 'scripts.server-syntax', 'scripts.server',
    'scripts.server-recipes', 'scripts.commands-feedback', 'scripts.client-visual', 'scripts.client-recipes',
    'intelligence.overview', 'intelligence.profiles-equipment', 'intelligence.operating-contexts', 'intelligence.dashboard-workflow'];
  const index = ordered.indexOf(id);
  return index < 0 ? 100 : index;
}

export function helpArea(category: string): HelpArea {
  if (['getting-started', 'engineering', 'application', 'packages', 'recovery'].includes(category)) return 'start';
  if (['sources', 'tags', 'gateway'].includes(category)) return 'data';
  if (category === 'drivers') return 'drivers';
  if (['templates', 'equipment'].includes(category)) return 'assets';
  if (['ha', 'database', 'installation'].includes(category)) return 'administration';
  if (['visual', 'visual-assets', 'visual-objects', 'screens', 'popups', 'dynamos', 'libraries', 'bindings-commands'].includes(category)) return 'visual';
  if (['scripts', 'memory'].includes(category)) return 'scripts';
  if (category === 'intelligence') return 'intelligence';
  if (['historian', 'trends', 'reports'].includes(category)) return 'history';
  if (['alarms', 'operational-events', 'audit'].includes(category)) return 'events';
  if (category === 'security') return 'security';
  if (category === 'licensing') return 'licensing';
  return 'runtime';
}

export const helpSubareas = ['guides', 'driverBasics', 'driverPlcs', 'driverProtocols', 'driverTelecontrol',
  'driverBuildings', 'driverRadios', 'driverReference', 'shapes', 'media', 'controls', 'gauges', 'analysisObjects'] as const;

export function helpSubarea(topic: Pick<SearchableHelpTopic, 'id' | 'category'>): typeof helpSubareas[number] {
  if (topic.category === 'drivers') {
    if (topic.id.startsWith('driver.')) return 'driverReference';
    if (['drivers.siemens-setup', 'drivers.logix-setup', 'drivers.melsec-setup', 'drivers.panasonic-setup'].includes(topic.id)) return 'driverPlcs';
    if (['drivers.modbus-setup', 'drivers.modbus-server-setup', 'drivers.mqtt-setup', 'drivers.opcua-setup'].includes(topic.id)) return 'driverProtocols';
    if (['drivers.dnp3-setup', 'drivers.iec104-setup'].includes(topic.id)) return 'driverTelecontrol';
    if (['home-building.device-workflow', 'drivers.bacnet-setup', 'drivers.knx-setup', 'drivers.homeassistant-setup', 'drivers.esphome-setup', 'drivers.shelly-setup', 'drivers.matter-test-workflow'].includes(topic.id)) return 'driverBuildings';
    if (['drivers.radio-connections', 'drivers.zigbee-native-setup', 'drivers.zigbee2mqtt-setup', 'drivers.zwave-setup'].includes(topic.id)) return 'driverRadios';
    return 'driverBasics';
  }
  if (topic.category !== 'visual-objects') return 'guides';
  if (topic.id.includes('measurement')) return 'gauges';
  if (topic.id.includes('intelligence')) return 'analysisObjects';
  if (/\.(image|svgsymbol|model3d|videoplayer|pdfviewer)$/.test(topic.id)) return 'media';
  if (/\.(button|slider|numericinput|valuedisplay|trend|alarmbrowser|eventbrowser|reportlauncher)$/.test(topic.id)) return 'controls';
  return 'shapes';
}

function searchable(value: string) {
  return value.normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase();
}

export function filterHelpTopics<T extends SearchableHelpTopic>(topics: readonly T[], query: string): readonly T[] {
  const words = searchable(query).trim().split(/\s+/).filter(Boolean);
  if (!words.length) return topics;
  return topics.filter(topic => {
    const text = searchable([topic.title, topic.summary, ...topic.sections.flatMap(section => [
      section.heading, section.body, section.code ?? '',
      ...(section.fields ?? []).flatMap(field => [field.label, field.key, field.description, field.example])
    ])].join(' '));
    return words.every(word => text.includes(word));
  });
}

export function helpBodyBlocks(body: string): readonly Readonly<{ ordered: boolean; lines: readonly string[] }>[] {
  return body.trim().split(/\n\s*\n/).filter(Boolean).map(paragraph => {
    if (/^1\.\s/.test(paragraph)) return {
      ordered: true,
      // A number ending a sentence (gain 5.) is content, not a list marker.
      // Published inline steps start after sentence punctuation or a line break.
      lines: paragraph.split(/(?<=[.!?])\s+(?=\d{1,2}\.\s)|\n\s*(?=\d{1,2}\.\s)/).map(step => step.replace(/^\d{1,2}\.\s+/, ''))
    };
    return { ordered: false, lines: [paragraph] };
  });
}
