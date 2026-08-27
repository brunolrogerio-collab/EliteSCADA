import { expect, test } from '@playwright/test';
import {
  filterEngineeringEntities,
  selectAdjacentEngineeringEntityKey,
  type EngineeringEntityBrowserFilter
} from '../src/engineering/EngineeringEntityBrowser.logic';

type Entity = {
  key: string;
  name: string;
  path: string;
  enabled: boolean;
};

const entities: Entity[] = [
  { key: 'pump-speed', name: 'Pump speed', path: 'Plant/Pump/Speed', enabled: true },
  { key: 'pump-status', name: 'Pump status', path: 'Plant/Pump/Status', enabled: true },
  { key: 'legacy-flow', name: 'Legacy flow', path: 'Plant/Legacy/Flow', enabled: false }
];

const enabledFilter: EngineeringEntityBrowserFilter<Entity> = {
  key: 'enabled',
  label: 'Enabled',
  matches: entity => entity.enabled
};

test.describe('EngineeringEntityBrowser logic', () => {
  test('filters by normalized search text without mutating the source collection', () => {
    const visible = filterEngineeringEntities(
      entities,
      '  pump/status ',
      undefined,
      entity => [entity.name, entity.path]
    );

    expect(visible.map(entity => entity.key)).toEqual(['pump-status']);
    expect(entities).toHaveLength(3);
  });

  test('combines an entity filter with search text', () => {
    const visible = filterEngineeringEntities(
      entities,
      'pump',
      enabledFilter,
      entity => [entity.name, entity.path]
    );

    expect(visible.map(entity => entity.key)).toEqual(['pump-speed', 'pump-status']);
  });

  test('navigates a visible list with bounded arrow, Home and End semantics', () => {
    const keys = ['pump-speed', 'pump-status', 'legacy-flow'];

    expect(selectAdjacentEngineeringEntityKey(keys, 'pump-speed', 'next')).toBe('pump-status');
    expect(selectAdjacentEngineeringEntityKey(keys, 'pump-speed', 'previous')).toBe('pump-speed');
    expect(selectAdjacentEngineeringEntityKey(keys, 'pump-status', 'first')).toBe('pump-speed');
    expect(selectAdjacentEngineeringEntityKey(keys, 'pump-status', 'last')).toBe('legacy-flow');
    expect(selectAdjacentEngineeringEntityKey(keys, null, 'next')).toBe('pump-speed');
    expect(selectAdjacentEngineeringEntityKey([], null, 'next')).toBeNull();
  });
});
