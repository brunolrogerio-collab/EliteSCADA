import { expect, test } from '@playwright/test';
import {
  classifyAdministrationStatus,
  countAdministrationUsers,
  filterAdministrationUsers,
  sameRoles,
  summarizeUserChanges
} from '../src/engineering/UserAdministration.logic';
import {
  capabilityKey,
  nextRoleKey,
  userGrantPreview
} from '../src/engineering/AuthorityPolicyAdministration';
import type {
  AuthorityPolicyDocument,
  AuthorityRole,
  LocalUser
} from '../src/engineering/userAdministrationApi';

const users: LocalUser[] = [
  {
    id: '1',
    username: 'local-developer',
    displayName: 'Local Developer',
    isEnabled: true,
    roles: ['developer'],
    createdAtUtc: '2026-08-27T12:00:00Z',
    updatedAtUtc: '2026-08-27T12:00:00Z'
  },
  {
    id: '2',
    username: 'shift-operator',
    displayName: 'Shift Operator',
    isEnabled: false,
    roles: ['operator', 'viewer'],
    createdAtUtc: '2026-08-27T12:00:00Z',
    updatedAtUtc: '2026-08-27T12:00:00Z'
  }
];

test('Administration model searches identity and role text and composes status filters', () => {
  expect(filterAdministrationUsers(users, 'shift', 'all').map(user => user.id)).toEqual(['2']);
  expect(filterAdministrationUsers(users, 'viewer', 'all').map(user => user.id)).toEqual(['2']);
  expect(filterAdministrationUsers(users, '', 'enabled').map(user => user.id)).toEqual(['1']);
  expect(filterAdministrationUsers(users, 'developer', 'disabled')).toEqual([]);
});

test('Administration model compares role sets without order or casing authority', () => {
  expect(sameRoles(['Developer', 'Operator'], ['operator', 'developer'])).toBeTruthy();
  expect(sameRoles(['developer'], ['developer', 'viewer'])).toBeFalsy();
});

test('Administration model reports only changed security/profile dimensions', () => {
  expect(summarizeUserChanges(users[0], {
    displayName: 'Local Developer',
    isEnabled: true,
    roles: ['DEVELOPER']
  })).toEqual([]);

  expect(summarizeUserChanges(users[0], {
    displayName: 'Engineering Admin',
    isEnabled: false,
    roles: ['developer', 'operator']
  })).toEqual(['displayName', 'status', 'roles']);
});

test('Administration summary keeps enabled and disabled counts explicit', () => {
  expect(countAdministrationUsers(users)).toEqual({ total: 2, enabled: 1, disabled: 1 });
});

test('Administration classifies authorization, conflict and validation HTTP states without collapsing them', () => {
  expect(classifyAdministrationStatus(400)).toBe('validation');
  expect(classifyAdministrationStatus(422)).toBe('validation');
  expect(classifyAdministrationStatus(401)).toBe('unauthorized');
  expect(classifyAdministrationStatus(403)).toBe('forbidden');
  expect(classifyAdministrationStatus(404)).toBe('not-found');
  expect(classifyAdministrationStatus(409)).toBe('conflict');
  expect(classifyAdministrationStatus(503)).toBe('unknown');
});


test('Authority UX keeps command execution distinct from process-value writing', () => {
  expect(capabilityKey(2)).toBe('CommandExecute');
  expect(capabilityKey(3)).toBe('ProcessValueWrite');
  expect(capabilityKey(2)).not.toBe(capabilityKey(3));
});

test('Authority UX multi-role preview preserves independent scoped grants', () => {
  const roles: AuthorityRole[] = [
    {
      id: '46000000-0000-0000-0000-000000000010',
      key: 'operator-a',
      name: 'Area A Operator',
      grants: [
        {
          capability: 2,
          scope: {
            scopeNodeId: '51000000-0000-0000-0000-000000000001',
            includeDescendants: true
          }
        }
      ]
    },
    {
      id: '46000000-0000-0000-0000-000000000011',
      key: 'alarm-ack',
      name: 'Alarm Acknowledger',
      grants: [
        {
          capability: 4,
          scope: {
            scopeNodeId: '51000000-0000-0000-0000-000000000002',
            includeDescendants: false
          }
        }
      ]
    }
  ];
  const user: LocalUser = {
    ...users[0],
    roles: ['alarm-ack', 'operator-a']
  };

  expect(userGrantPreview(user, roles)).toEqual([
    {
      role: 'alarm-ack',
      capability: 'AlarmAcknowledge',
      scopeNodeId: '51000000-0000-0000-0000-000000000002',
      includeDescendants: false
    },
    {
      role: 'operator-a',
      capability: 'CommandExecute',
      scopeNodeId: '51000000-0000-0000-0000-000000000001',
      includeDescendants: true
    }
  ]);
});

test('Authority UX role-key generation never treats display names as privilege semantics', () => {
  const policy: AuthorityPolicyDocument = {
    schema: 'elitescada.authority-policy',
    schemaVersion: 1,
    version: 7,
    roles: [
      {
        id: '46000000-0000-0000-0000-000000000001',
        key: 'custom-role',
        name: 'Administrator',
        grants: []
      },
      {
        id: '46000000-0000-0000-0000-000000000002',
        key: 'custom-role-2',
        name: 'Viewer',
        grants: []
      }
    ],
    scopes: []
  };

  expect(nextRoleKey(policy)).toBe('custom-role-3');
});
