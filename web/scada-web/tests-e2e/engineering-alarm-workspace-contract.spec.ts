import { expect, test } from '@playwright/test';
import { filterEngineeringEntities } from '../src/engineering/EngineeringEntityBrowser.logic';
import {
  alarmConditionSummary,
  alarmEngineeringKey,
  alarmSearchText,
  alarmTagReference,
  buildAlarmWorkspaceFilters
} from '../src/engineering/AlarmWorkspace.logic';
import type { AlarmEngineering } from '../src/engineering/types';

const alarms: AlarmEngineering[] = [
  {
    id: 'alarm-high-pressure',
    name: 'High discharge pressure',
    tagId: 'tag-pressure',
    tagPath: 'Plant/Pump01/Pressure',
    type: 'High',
    priority: 'critical',
    setpoint: 8.5,
    alarmClass: 'Process',
    area: 'Pumping',
    message: 'Discharge pressure above operating limit',
    activationDelayMilliseconds: 500,
    requiresAcknowledgement: true,
    shelvingAllowed: true,
    enabled: true,
    metadata: { equipment: 'PUMP-01' }
  },
  {
    id: 'alarm-motor-fault',
    name: 'Motor fault',
    tagId: 'tag-motor-fault',
    tagPath: 'Plant/Pump01/MotorFault',
    type: 'Digital',
    priority: 'high',
    digitalActiveValue: true,
    alarmClass: 'Electrical',
    area: 'Pumping',
    message: 'Motor protection fault',
    requiresAcknowledgement: true,
    enabled: false
  },
  {
    id: 'alarm-low-level',
    name: 'Low wet well level',
    tagPath: 'Plant/WetWell/Level',
    type: 'Low',
    priority: 'medium',
    setpoint: 1.2,
    area: 'Wet Well',
    enabled: true
  }
];

const labels = {
  enabled: 'Enabled',
  disabled: 'Disabled',
  requiresAck: 'Requires ACK',
  priority: 'Priority',
  area: 'Area',
  type: 'Type'
};

test.describe('Engineering Alarm workspace contract', () => {
  test('derives filters only from canonical Alarm fields and keeps values deterministic', () => {
    const filters = buildAlarmWorkspaceFilters(alarms, labels);

    expect(filters.map(filter => filter.key)).toEqual([
      'status:enabled',
      'status:disabled',
      'ack:required',
      'priority:critical',
      'priority:high',
      'priority:medium',
      'area:Pumping',
      'area:Wet Well',
      'type:Digital',
      'type:High',
      'type:Low'
    ]);

    const disabled = filters.find(filter => filter.key === 'status:disabled');
    const pumping = filters.find(filter => filter.key === 'area:Pumping');

    expect(alarms.filter(alarm => disabled?.matches(alarm)).map(alarm => alarm.id)).toEqual(['alarm-motor-fault']);
    expect(alarms.filter(alarm => pumping?.matches(alarm)).map(alarm => alarm.id)).toEqual([
      'alarm-high-pressure',
      'alarm-motor-fault'
    ]);
  });

  test('combines Alarm filter and search projection without mutating canonical source data', () => {
    const filters = buildAlarmWorkspaceFilters(alarms, labels);
    const critical = filters.find(filter => filter.key === 'priority:critical');

    const visible = filterEngineeringEntities(
      alarms,
      'pump-01',
      critical,
      alarmSearchText
    );

    expect(visible.map(alarm => alarm.id)).toEqual(['alarm-high-pressure']);
    expect(alarms).toHaveLength(3);
  });

  test('search projection includes stable identity, TAG reference, message and metadata', () => {
    const searchText = alarmSearchText(alarms[0]);

    expect(searchText).toContain('alarm-high-pressure');
    expect(searchText).toContain('Plant/Pump01/Pressure');
    expect(searchText).toContain('Discharge pressure above operating limit');
    expect(searchText).toContain('equipment');
    expect(searchText).toContain('PUMP-01');
  });

  test('uses persisted stable ID when available and deterministic canonical fallback otherwise', () => {
    expect(alarmEngineeringKey(alarms[0])).toBe('alarm-high-pressure');

    const draft: AlarmEngineering = {
      name: 'Draft alarm',
      tagPath: 'Plant/Draft/Value',
      type: 'High',
      priority: 'medium'
    };

    expect(alarmEngineeringKey(draft)).toBe('alarm::Draft alarm::Plant/Draft/Value::High::medium');
    expect(alarmTagReference(draft)).toBe('Plant/Draft/Value');
  });

  test('formats numeric and digital Alarm condition summaries without inventing semantics', () => {
    expect(alarmConditionSummary(alarms[0], 'Not configured')).toBe('High · 8.5');
    expect(alarmConditionSummary(alarms[1], 'Not configured')).toBe('Digital · true');

    const noCondition: AlarmEngineering = {
      name: 'State only',
      type: 'State',
      priority: 'low'
    };

    expect(alarmConditionSummary(noCondition, 'Not configured')).toBe('State');
  });
});
