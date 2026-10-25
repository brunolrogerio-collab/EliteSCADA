import { expect, test } from '@playwright/test';
import { contextualHelpTopic } from '../src/help/contextualHelpTopic';

test('routes resolve to stable contextual Help topics with a safe fallback', () => {
  expect(contextualHelpTopic('/engineering/database-topology')).toBe('database.topology');
  expect(contextualHelpTopic('/engineering/databaseTopology')).toBe('database.topology');
  expect(contextualHelpTopic('/engineering/templates')).toBe('templates.overview');
  expect(contextualHelpTopic('/engineering/equipment')).toBe('equipment.overview');
  expect(contextualHelpTopic('/engineering/branding')).toBe('application.presentation');
  expect(contextualHelpTopic('/engineering/mobile')).toBe('application.presentation');
  expect(contextualHelpTopic('/engineering/mediaSources')).toBe('sources.media');
  expect(contextualHelpTopic('/engineering/homeBuilding')).toBe('home-building.organization');
  expect(contextualHelpTopic('/engineering/scripts')).toBe('scripts.engineering');
  expect(contextualHelpTopic('/engineering/intelligence')).toBe('intelligence.overview');
  expect(contextualHelpTopic('/engineering/highAvailability')).toBe('ha.configuration-operation');
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
