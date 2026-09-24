export function contextualHelpTopic(path: string) {
  if (path.startsWith('/engineering/scripts')) return 'scripts.server';
  if (path.startsWith('/engineering/libraries')) return 'libraries.reusable-resources';
  if (path.startsWith('/engineering/security')) return 'security.users-roles-capabilities';
  if (path.startsWith('/engineering/recovery') || path.startsWith('/engineering/project')) return 'recovery.backup-system-recovery';
  if (path.startsWith('/engineering')) return 'engineering.overview';
  if (path.startsWith('/audit')) return 'audit.overview';
  if (path.startsWith('/licensing')) return 'licensing.overview';
  if (path.startsWith('/runtime/history')) return 'runtime.history';
  return 'runtime.overview';
}
