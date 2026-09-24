import { expect, test } from '@playwright/test';
import { contextualHelpTopic } from '../src/AppNavigation';

test('Engineering section routes resolve to stable contextual Help topics with a safe fallback', () => {
  expect(contextualHelpTopic('/engineering/scripts')).toBe('scripts.server');
  expect(contextualHelpTopic('/engineering/libraries')).toBe('libraries.reusable-resources');
  expect(contextualHelpTopic('/engineering/security')).toBe('security.users-roles-capabilities');
  expect(contextualHelpTopic('/engineering/project')).toBe('recovery.backup-system-recovery');
  expect(contextualHelpTopic('/engineering/screens')).toBe('engineering.overview');
  expect(contextualHelpTopic('/engineering')).toBe('engineering.overview');
});
