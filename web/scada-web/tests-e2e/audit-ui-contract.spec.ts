import { expect, test } from '@playwright/test';
import { AUDIT_NEXT_CURSOR_HEADER, buildAuditQueryPath } from '../src/audit/contract';
import type { AuditFilterState } from '../src/audit/types';

const completeFilters: AuditFilterState = {
  fromLocal: '2026-08-26T18:00',
  toLocal: '2026-08-26T19:00',
  subjectId: 'operator-1',
  action: 'command.execute',
  outcome: 'Denied',
  targetKind: 'command',
  targetId: 'pump.start',
  area: 'Area1',
  correlationId: 'corr-123',
  pageSize: 50
};

test('Audit UI query maps only supported backend filters and transports cursor opaquely', () => {
  const opaqueCursor = 'opaque+/= value.with.parts';
  const path = buildAuditQueryPath(completeFilters, opaqueCursor);
  const url = new URL(path, 'http://localhost');

  expect(url.pathname).toBe('/api/audit');
  expect(url.searchParams.get('limit')).toBe('50');
  expect(url.searchParams.get('subjectId')).toBe('operator-1');
  expect(url.searchParams.get('action')).toBe('command.execute');
  expect(url.searchParams.get('outcome')).toBe('Denied');
  expect(url.searchParams.get('targetKind')).toBe('command');
  expect(url.searchParams.get('targetId')).toBe('pump.start');
  expect(url.searchParams.get('area')).toBe('Area1');
  expect(url.searchParams.get('correlationId')).toBe('corr-123');
  expect(url.searchParams.get('cursor')).toBe(opaqueCursor);
  expect(url.searchParams.has('offset')).toBe(false);
  expect(url.searchParams.get('fromUtc')).toBeTruthy();
  expect(url.searchParams.get('toUtc')).toBeTruthy();
  expect(AUDIT_NEXT_CURSOR_HEADER).toBe('X-EliteSCADA-Audit-Next-Cursor');
});

test('Audit UI query omits empty optional filters', () => {
  const path = buildAuditQueryPath({
    ...completeFilters,
    fromLocal: '',
    toLocal: '',
    subjectId: '  ',
    action: '',
    outcome: '',
    targetKind: '',
    targetId: '',
    area: '',
    correlationId: ''
  });
  const url = new URL(path, 'http://localhost');

  expect([...url.searchParams.keys()]).toEqual(['limit']);
  expect(url.searchParams.get('limit')).toBe('50');
});

test('Audit UI query rejects unbounded or invalid ranges client-side', () => {
  expect(() => buildAuditQueryPath({ ...completeFilters, pageSize: 0 })).toThrow();
  expect(() => buildAuditQueryPath({ ...completeFilters, pageSize: 1001 })).toThrow();
  expect(() => buildAuditQueryPath({
    ...completeFilters,
    fromLocal: '2026-08-26T20:00',
    toLocal: '2026-08-26T19:00'
  })).toThrow();
});
