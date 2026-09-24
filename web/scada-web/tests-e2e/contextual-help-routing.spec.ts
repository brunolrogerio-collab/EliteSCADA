import { expect, test } from '@playwright/test';
import { contextualHelpTopic } from '../src/help/contextualHelpTopic';

test('routes resolve to stable contextual Help topics with a safe fallback', () => {
  expect(contextualHelpTopic('/engineering/scripts')).toBe('scripts.server');
  expect(contextualHelpTopic('/engineering/libraries')).toBe('libraries.reusable-resources');
  expect(contextualHelpTopic('/engineering/security')).toBe('security.users-roles-capabilities');
  expect(contextualHelpTopic('/engineering/recovery')).toBe('recovery.backup-system-recovery');
  expect(contextualHelpTopic('/engineering/project')).toBe('recovery.backup-system-recovery');
  expect(contextualHelpTopic('/engineering/screens')).toBe('engineering.overview');
  expect(contextualHelpTopic('/engineering')).toBe('engineering.overview');
  expect(contextualHelpTopic('/audit/events')).toBe('audit.overview');
  expect(contextualHelpTopic('/licensing')).toBe('licensing.overview');
  expect(contextualHelpTopic('/runtime/history')).toBe('runtime.history');
  expect(contextualHelpTopic('/unknown')).toBe('runtime.overview');
});
