import { expect, test } from '@playwright/test';
import { shouldShowTagAddressEditor } from '../src/engineering/tagAddressPolicy';

test.describe('Wave 14 C17 TAG address authoring policy', () => {
  test('hides Address while a selected Data Source type is still resolving', () => {
    expect(shouldShowTagAddressEditor({
      hasDataSource: true,
      sourceTypeResolved: false,
      sourceKind: null
    })).toBe(false);
  });

  test('hides Address for sourceProvider-backed Data Sources', () => {
    expect(shouldShowTagAddressEditor({
      hasDataSource: true,
      sourceTypeResolved: true,
      sourceKind: 'sourceProvider'
    })).toBe(false);

    expect(shouldShowTagAddressEditor({
      hasDataSource: true,
      sourceTypeResolved: true,
      sourceKind: 'SOURCEPROVIDER'
    })).toBe(false);
  });

  test('keeps Address for normal driver-backed Data Sources', () => {
    expect(shouldShowTagAddressEditor({
      hasDataSource: true,
      sourceTypeResolved: true,
      sourceKind: 'driver'
    })).toBe(true);
  });

  test('fails open for an unresolved catalog kind after resolution completes', () => {
    expect(shouldShowTagAddressEditor({
      hasDataSource: true,
      sourceTypeResolved: true,
      sourceKind: null
    })).toBe(true);
  });

  test('keeps manual Address authoring when no Data Source is selected', () => {
    expect(shouldShowTagAddressEditor({
      hasDataSource: false,
      sourceTypeResolved: true,
      sourceKind: null
    })).toBe(true);
  });
});
