import { expect, test } from '@playwright/test';
import {
  parsePythonPreviewSample,
  projectPythonPreviewExecution,
  redactPythonPreviewText,
  sourceLineForPreview
} from '../src/engineering/scripts/pythonPreviewModel';

test('Preview/Test accepts bounded JSON and rejects malformed or unsafe sample contexts', () => {
  expect(parsePythonPreviewSample('{"preview":true,"value":42}')).toEqual({
    ok: true,
    value: { preview: true, value: 42 }
  });
  expect(parsePythonPreviewSample('{broken')).toEqual({ ok: false, error: 'invalid-json' });
  expect(parsePythonPreviewSample('{"constructor":{"x":1}}')).toEqual({ ok: false, error: 'unsupported-key' });

  let nested = '0';
  for (let index = 0; index < 10; index++) nested = `[${nested}]`;
  expect(parsePythonPreviewSample(nested)).toEqual({ ok: false, error: 'too-deep' });
});

test('Preview/Test projects success timeout cancellation and runtime fault deterministically', () => {
  expect(projectPythonPreviewExecution('on_click', 'completed', 1.25)).toEqual({
    state: 'success',
    status: 'completed',
    durationMs: 1.25,
    sanitizedError: undefined,
    trace: undefined
  });
  expect(projectPythonPreviewExecution('on_click', 'timed-out', 250, 'Python handler exceeded its execution budget.').state)
    .toBe('timed-out');
  expect(projectPythonPreviewExecution('on_click', 'cancelled').state).toBe('cancelled');

  const fault = projectPythonPreviewExecution(
    'on_click',
    'faulted',
    2.5,
    'Python handler failed (ValueError) at line 7.'
  );
  expect(fault.state).toBe('runtime-error');
  expect(fault.trace).toEqual({
    exceptionType: 'ValueError',
    failingLine: 7,
    frames: [{ functionName: 'on_click', line: 7 }]
  });
});

test('Preview/Test redacts credential-shaped source content before showing the failing line', () => {
  const source = [
    'def on_click(event):',
    '    token = "super-secret-value"',
    '    raise ValueError("safe")'
  ].join('\n');

  expect(sourceLineForPreview(source, 2)).toContain('token=<redacted>');
  expect(sourceLineForPreview(source, 2)).not.toContain('super-secret-value');
  expect(redactPythonPreviewText('Authorization: Bearer abc.def.ghi')).toBe('Authorization: Bearer <redacted>');
});
