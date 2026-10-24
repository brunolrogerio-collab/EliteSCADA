import { expect, test } from '@playwright/test';
import { contextualHelpTopic } from '../src/help/contextualHelpTopic';

test('routes resolve to stable contextual Help topics with a safe fallback', () => {
  expect(contextualHelpTopic('/engineering/scripts')).toBe('scripts.engineering');
  expect(contextualHelpTopic('/engineering/intelligence')).toBe('intelligence.overview');
  expect(contextualHelpTopic('/engineering/highAvailability')).toBe('ha.overview');
  expect(contextualHelpTopic('/engineering/installation')).toBe('installation.switching');
  expect(contextualHelpTopic('/engineering/libraries')).toBe('libraries.reusable-resources');
  expect(contextualHelpTopic('/engineering/security')).toBe('security.users-roles-capabilities');
  expect(contextualHelpTopic('/engineering/recovery')).toBe('recovery.backup-system-recovery');
  expect(contextualHelpTopic('/engineering/project')).toBe('recovery.backup-system-recovery');
  expect(contextualHelpTopic('/engineering/screens')).toBe('screens.overview');
  expect(contextualHelpTopic('/engineering')).toBe('engineering.shell-navigation');
  expect(contextualHelpTopic('/audit/events')).toBe('audit.overview');
  expect(contextualHelpTopic('/licensing')).toBe('licensing.overview');
  expect(contextualHelpTopic('/runtime/history')).toBe('runtime.history');
  expect(contextualHelpTopic('/unknown')).toBe('runtime.overview');
});
