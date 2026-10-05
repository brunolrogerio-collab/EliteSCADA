export function contextualHelpTopic(path: string) {
  if (path.startsWith('/engineering/diagnostics/tag-monitor')) return 'diagnostics.overview';
  if (path.startsWith('/engineering/diagnostics')) return 'diagnostics.overview';
  if (path.startsWith('/engineering/scripts')) return 'scripts.engineering';
  if (path.startsWith('/engineering/libraries')) return 'libraries.reusable-resources';
  if (path.startsWith('/engineering/dataSources')) return 'sources.data-sources';
  if (path.startsWith('/engineering/tags')) return 'tags.overview';
  if (path.startsWith('/engineering/gateway')) return 'gateway.overview';
  if (path.startsWith('/engineering/alarms')) return 'alarms.overview';
  if (path.startsWith('/engineering/operationalEvents')) return 'operational-events.overview';
  if (path.startsWith('/engineering/dynamos')) return 'dynamos.overview';
  if (path.startsWith('/engineering/screens')) return 'screens.overview';
  if (path.startsWith('/engineering/popups')) return 'popups.overview';
  if (path.startsWith('/engineering/historian')) return 'historian.overview';
  if (path.startsWith('/engineering/reports')) return 'reports.overview';
  if (path.startsWith('/engineering/security')) return 'security.users-roles-capabilities';
  if (path.startsWith('/engineering/recovery') || path.startsWith('/engineering/project')) return 'recovery.backup-system-recovery';
  if (path.startsWith('/engineering')) return 'engineering.shell-navigation';
  if (path.startsWith('/audit')) return 'audit.overview';
  if (path.startsWith('/licensing')) return 'licensing.overview';
  if (path.startsWith('/runtime/history')) return 'runtime.history';
  return 'runtime.overview';
}
