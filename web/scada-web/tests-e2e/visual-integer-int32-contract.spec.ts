import { expect, test } from '@playwright/test';
import {
  COMMON_VISUAL_PROPERTY_REGISTRY,
  VISUAL_PROPERTY_KEYS
} from '../src/visual-runtime';

test('integer visual properties use the same signed Int32 domain as the C# visual model', () => {
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, -2147483648))
    .toMatchObject({ ok: true, value: -2147483648 });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, 2147483647))
    .toMatchObject({ ok: true, value: 2147483647 });

  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, -2147483649))
    .toMatchObject({ ok: false, code: 'number.integer' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, 2147483648))
    .toMatchObject({ ok: false, code: 'number.integer' });
  expect(COMMON_VISUAL_PROPERTY_REGISTRY.validate(VISUAL_PROPERTY_KEYS.zIndex, 1.5))
    .toMatchObject({ ok: false, code: 'number.integer' });
});
