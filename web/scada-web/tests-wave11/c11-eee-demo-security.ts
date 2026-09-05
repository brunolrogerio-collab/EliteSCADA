const roleId = (value: number) => `c1160000-0000-4000-8000-${String(value).padStart(12, '0')}`;

const grant = (capability: string) => ({ capability, scope: null, metadata: { application: 'eee-demo' } });

export const EEE_SECURITY_ROLES = [
  {
    id: roleId(1),
    key: 'developer',
    name: 'EEE Developer',
    description: 'Engineering/development role for the canonical EEE Demo.',
    grants: [
      'view',
      'tagRead',
      'commandExecute',
      'processValueWrite',
      'alarmAcknowledge',
      'alarmShelve',
      'trendUse',
      'trendSave',
      'engineeringModify',
      'userRoleAdmin',
      'systemAdmin'
    ].map(grant),
    metadata: { application: 'eee-demo' }
  },
  {
    id: roleId(2),
    key: 'operator',
    name: 'EEE Operator',
    description: 'Least-privilege operator role for the canonical EEE Demo Runtime.',
    grants: [
      'view',
      'tagRead',
      'commandExecute',
      'alarmAcknowledge',
      'alarmShelve',
      'trendUse'
    ].map(grant),
    metadata: { application: 'eee-demo' }
  }
] as const;
